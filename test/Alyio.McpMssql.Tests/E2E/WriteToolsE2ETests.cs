// MIT License

using Alyio.McpMssql.Tests.Infrastructure.Fixtures;
using ModelContextProtocol.Client;

namespace Alyio.McpMssql.Tests.E2E;

public sealed class WriteToolsE2ETests(McpServerFixture fixture) : IClassFixture<McpServerFixture>
{
    private readonly McpClient _client = fixture.Client;

    private const string WriteToolName = "run_command";

    // ── Tool discovery ──

    [Fact]
    public async Task RunCommand_Tool_Is_Not_Registered_Without_A_Write_Enabled_Profile()
    {
        // The fixture profile leaves AllowWrite unset, so the write tool should
        // never reach tools/list.
        Assert.False(await _client.IsToolRegisteredAsync(WriteToolName));
    }
}
