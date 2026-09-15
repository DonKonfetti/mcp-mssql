// MIT License

using Alyio.McpMssql.Tests.Infrastructure.Fixtures;
using ModelContextProtocol.Client;

namespace Alyio.McpMssql.Tests.E2E;

public class SnapshotE2ETests(McpServerFixture fixture) : IClassFixture<McpServerFixture>
{
    private const string RunQueryTool = "run_query";
    private readonly McpClient _client = fixture.Client;
    private static CancellationToken CancellationToken => TestContext.Current.CancellationToken;

    [Fact]
    public async Task RunQuery_Snapshot_Returns_Uri_That_Round_Trips_To_Csv()
    {
        var result = await _client.CallToolAsync(
            RunQueryTool,
            new Dictionary<string, object?> { ["sql"] = "SELECT 1 AS Value", ["snapshot"] = true },
            cancellationToken: CancellationToken);

        Assert.True(result.IsError is not true);

        // The rows travel by URI, never inline.
        var root = result.ReadJsonRoot();
        Assert.False(root.TryGetProperty("data", out _));
        Assert.Equal(1, root.GetProperty("row_count").GetInt32());

        var snapshotUri = root.GetProperty("snapshot_uri").GetString()!;
        Assert.StartsWith("mssql://snapshots/", snapshotUri);

        var resource = await _client.ReadResourceAsync(snapshotUri, cancellationToken: CancellationToken);
        var csv = resource.ReadAsText();

        Assert.Equal("Value", TabularAssertions.ParseCsvHeaders(csv)[0]);
        var row = Assert.Single(TabularAssertions.ParseCsvDataRows(csv));
        Assert.Equal("1", row[0]);
    }

    [Fact]
    public async Task Snapshot_Unknown_Id_Throws()
    {
        var ex = await Assert.ThrowsAnyAsync<Exception>(async () =>
            await _client.ReadResourceAsync("mssql://snapshots/nonexistent", cancellationToken: CancellationToken));

        Assert.Contains("not found", ex.Message, StringComparison.OrdinalIgnoreCase);
    }
}
