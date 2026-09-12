// MIT License

using Microsoft.SqlServer.TransactSql.ScriptDom;

namespace Alyio.McpMssql.Internal;

/// <summary>
/// Validates that a SQL statement is a single, read-only SELECT query.
/// </summary>
internal static class SqlReadOnlyValidator
{
    /// <summary>
    /// Parses and validates the provided T-SQL text.
    /// </summary>
    /// <exception cref="ArgumentException">
    /// Thrown when the SQL is null or empty.
    /// </exception>
    /// <exception cref="InvalidOperationException">
    /// Thrown when the SQL is invalid or is not a read-only SELECT query.
    /// </exception>
    public static void Validate(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            throw new ArgumentException("SQL query cannot be empty.", nameof(sql));
        }

        // Newest grammar, with SqlEngineType.All left at its default, so
        // validation never rejects syntax the target server would accept.
        // The server stays the authority on what it can actually execute.
        var parser = new TSql180Parser(initialQuotedIdentifiers: true);
        TSqlFragment fragment;

        using (var reader = new StringReader(sql))
        {
            fragment = parser.Parse(reader, out IList<ParseError> errors);

            if (errors.Count > 0)
            {
                ParseError first = errors[0];
                throw new InvalidOperationException(
                    $"Invalid T-SQL at line {first.Line}, column {first.Column}: {first.Message}");
            }
        }

        if (fragment is not TSqlScript { Batches.Count: 1 } script
            || script.Batches[0].Statements is not [SelectStatement select])
        {
            throw new InvalidOperationException(
                "Only one read-only SELECT statement is allowed.");
        }

        var visitor = new ReadOnlyViolationVisitor();
        select.Accept(visitor);

        if (visitor.Violation is not null)
        {
            throw new InvalidOperationException(visitor.Violation);
        }
    }

    private sealed class ReadOnlyViolationVisitor : TSqlFragmentVisitor
    {
        public string? Violation { get; private set; }

        public override void Visit(TSqlFragment node)
        {
            if (node is AdHocTableReference
                or OpenQueryTableReference
                or OpenRowsetTableReference
                or InternalOpenRowset
                or OpenRowsetCosmos
                or BulkOpenRowset)
            {
                Reject("Ad-hoc external data sources are not allowed.");
            }

            base.Visit(node);
        }

        public override void ExplicitVisit(SelectStatement node)
        {
            if (node.Into is not null)
            {
                Reject("SELECT INTO is not allowed.");
            }

            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(SelectSetVariable node)
        {
            Reject("Assigning variables in SELECT is not allowed.");
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(NextValueForExpression node)
        {
            Reject("NEXT VALUE FOR is not allowed because it changes sequence state.");
            base.ExplicitVisit(node);
        }

        public override void ExplicitVisit(TableHint node)
        {
            if (node.HintKind is TableHintKind.HoldLock
                or TableHintKind.TabLockX
                or TableHintKind.UpdLock
                or TableHintKind.XLock)
            {
                Reject($"The {node.HintKind} locking hint is not allowed.");
            }

            base.ExplicitVisit(node);
        }

        private void Reject(string message)
        {
            Violation ??= message;
        }
    }
}
