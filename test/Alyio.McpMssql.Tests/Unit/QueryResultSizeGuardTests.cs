// MIT License

using Alyio.McpMssql.Internal;
using Alyio.McpMssql.Models;

namespace Alyio.McpMssql.Tests.Unit;

public sealed class QueryResultSizeGuardTests
{
    [Fact]
    public void AddValue_Counts_Utf8_Bytes()
    {
        long result = QueryResultSizeGuard.AddValue(
            currentByteCount: 1,
            value: "ä",
            resultByteLimit: 3,
            cellByteLimit: 2);

        Assert.Equal(3, result);
    }

    [Fact]
    public void AddValue_Rejects_Value_Above_Cell_Limit()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            QueryResultSizeGuard.AddValue(
                currentByteCount: 0,
                value: new byte[3],
                resultByteLimit: 10,
                cellByteLimit: 2));

        Assert.Contains("value", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void AddValue_Rejects_Cumulative_Result_Above_Limit()
    {
        var exception = Assert.Throws<InvalidOperationException>(() =>
            QueryResultSizeGuard.AddValue(
                currentByteCount: 2,
                value: "ab",
                resultByteLimit: 3,
                cellByteLimit: 2));

        Assert.Contains("result", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SerializeCsv_Rejects_Serialization_Overhead_Above_Limit()
    {
        var result = new SelectResult(
            ["Name"],
            [new object?[] { "a,b" }],
            Truncated: false,
            RowLimit: 1);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            QueryResultSizeGuard.SerializeCsv(result, resultByteLimit: 3));

        Assert.Contains("serialized", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void SerializeCsv_Returns_Result_Within_Limit()
    {
        var result = new SelectResult(
            ["Name"],
            [new object?[] { "value" }],
            Truncated: false,
            RowLimit: 1);

        var csv = QueryResultSizeGuard.SerializeCsv(result, resultByteLimit: 100);

        Assert.Contains("value", csv, StringComparison.Ordinal);
    }
}
