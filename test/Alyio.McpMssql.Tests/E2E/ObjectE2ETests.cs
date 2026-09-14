// MIT License

using Alyio.McpMssql.Tests.Infrastructure.Fixtures;
using ModelContextProtocol.Client;

namespace Alyio.McpMssql.Tests.E2E;

public sealed class ObjectE2ETests(McpServerFixture fixture) : IClassFixture<McpServerFixture>
{
    private readonly McpClient _client = fixture.Client;
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    private const string ObjectsToolName = "list_objects";
    private const string ObjectToolName = "get_object";
    private static readonly string[] s_includeColumns = ["columns"];
    private static readonly string[] s_includeIndexes = ["indexes"];
    private static readonly string[] s_includeConstraints = ["constraints"];
    private static readonly string[] s_includeDefinition = ["definition"];

    // ── Tool discovery ──

    [Fact]
    public async Task Object_Tools_Are_Discoverable()
    {
        Assert.True(await _client.IsToolRegisteredAsync(ObjectsToolName, ObjectToolName));
    }

    // ── list_objects ──

    [Fact]
    public async Task ListCatalogs_Tool_Returns_Expected_Columns()
    {
        var result = await _client.CallToolAsync(
            ObjectsToolName,
            new Dictionary<string, object?> { ["kind"] = "catalog" },
            cancellationToken: CancellationToken);

        var root = result.ReadJsonRoot();
        var (columns, _) = root.ReadColumnRows();

        columns.AssertHasColumns("name", "state_desc", "is_read_only", "is_system_db");
    }

    [Fact]
    public async Task ListSchemas_Tool_Returns_Expected_Columns()
    {
        var result = await _client.CallToolAsync(
            ObjectsToolName,
            new Dictionary<string, object?>
            {
                ["kind"] = "schema",
                ["catalog"] = "master"
            },
            cancellationToken: CancellationToken);

        var root = result.ReadJsonRoot();
        var (columns, _) = root.ReadColumnRows();

        columns.AssertHasColumns("name");
    }

    [Fact]
    public async Task ListRelations_Tool_Returns_Expected_Columns()
    {
        var result = await _client.CallToolAsync(
            ObjectsToolName,
            new Dictionary<string, object?>
            {
                ["kind"] = "relation",
                ["catalog"] = "master",
                ["schema"] = "dbo"
            },
            cancellationToken: CancellationToken);
        var root = result.ReadJsonRoot();
        var (columns, _) = root.ReadColumnRows();
        columns.AssertHasColumns("name", "type");
    }

    [Fact]
    public async Task ListRoutines_Tool_Returns_Expected_Columns()
    {
        var result = await _client.CallToolAsync(
            ObjectsToolName,
            new Dictionary<string, object?>
            {
                ["kind"] = "routine",
                ["catalog"] = "master",
                ["schema"] = "dbo"
            },
            cancellationToken: CancellationToken);

        var root = result.ReadJsonRoot();
        var (columns, _) = root.ReadColumnRows();

        columns.AssertHasColumns("name", "type");
    }

    // ── get_object ──

    [Fact]
    public async Task DescribeColumns_Tool_Returns_Expected_Columns()
    {
        var result = await _client.CallToolAsync(
            ObjectToolName,
            new Dictionary<string, object?>
            {
                ["kind"] = "relation",
                ["catalog"] = "master",
                ["schema"] = "dbo",
                ["name"] = "sysobjects",
                ["includes"] = s_includeColumns
            },
            cancellationToken: CancellationToken);

        var root = result.ReadJsonRoot();
        var (columns, _) = root.ReadColumnRowsFrom("columns");

        columns.AssertHasColumns("name", "type", "is_nullable", "column_id");
    }

    [Fact]
    public async Task DescribeIndexes_Tool_Returns_Expected_Columns()
    {
        var result = await _client.CallToolAsync(
            ObjectToolName,
            new Dictionary<string, object?>
            {
                ["kind"] = "relation",
                ["catalog"] = "master",
                ["schema"] = "dbo",
                ["name"] = "sysobjects",
                ["includes"] = s_includeIndexes
            },
            cancellationToken: CancellationToken);

        var root = result.ReadJsonRoot();
        var (columns, _) = root.ReadColumnRowsFrom("indexes");

        columns.AssertHasColumns(
            "index_name", "index_type", "is_unique", "is_disabled", "has_filter",
            "filter_definition", "key_ordinal", "is_descending", "column_name", "is_included_column");
    }

    [Fact]
    public async Task DescribeConstraints_Tool_Returns_Expected_Structure()
    {
        var result = await _client.CallToolAsync(
            ObjectToolName,
            new Dictionary<string, object?>
            {
                ["kind"] = "relation",
                ["catalog"] = "master",
                ["schema"] = "dbo",
                ["name"] = "sysobjects",
                ["includes"] = s_includeConstraints
            },
            cancellationToken: CancellationToken);

        var root = result.ReadJsonRoot();

        Assert.True(root.TryGetProperty("constraints", out var constraints));
        Assert.True(constraints.TryGetProperty("primary_keys", out var pk));
        Assert.True(pk.TryGetProperty("columns", out _));
        Assert.True(pk.TryGetProperty("rows", out _));
        Assert.True(constraints.TryGetProperty("unique_constraints", out _));
        Assert.True(constraints.TryGetProperty("foreign_keys", out _));
        Assert.True(constraints.TryGetProperty("check_constraints", out _));
        Assert.True(constraints.TryGetProperty("default_constraints", out _));
    }

    [Fact]
    public async Task GetRoutineDefinition_Tool_Returns_Expected_Columns()
    {
        var result = await _client.CallToolAsync(
            ObjectToolName,
            new Dictionary<string, object?>
            {
                ["kind"] = "routine",
                ["catalog"] = "master",
                ["schema"] = "dbo",
                ["name"] = "sp_who",
                ["includes"] = s_includeDefinition
            },
            cancellationToken: CancellationToken);

        var root = result.ReadJsonRoot();
        var (columns, _) = root.ReadColumnRowsFrom("definition");

        columns.AssertHasColumns("definition");
    }
}
