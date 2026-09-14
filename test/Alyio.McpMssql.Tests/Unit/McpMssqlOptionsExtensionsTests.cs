// MIT License

using Alyio.McpMssql.Configuration;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

namespace Alyio.McpMssql.Tests.Unit;

public sealed class McpMssqlOptionsExtensionsTests
{
    [Theory]
    [InlineData("0", 1)]
    [InlineData("3600", AnalyzeOptions.HardCommandTimeoutSeconds)]
    public void AddMcpMssqlOptions_Clamps_Analyze_Timeout_To_Hard_Limits(
        string configuredTimeout,
        int expectedTimeout)
    {
        var values = new Dictionary<string, string?>
        {
            ["MCPMSSQL_CONNECTION_STRING"] =
                "Server=.;Database=DefaultDb;TrustServerCertificate=True;",
            ["MCPMSSQL_ANALYZE_COMMAND_TIMEOUT_SECONDS"] = configuredTimeout,
        };
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();
        var services = new ServiceCollection();
        services.AddMcpMssqlOptions(configuration);
        using var provider = services.BuildServiceProvider();

        var options = provider.GetRequiredService<IOptions<McpMssqlOptions>>().Value;

        Assert.Equal(
            expectedTimeout,
            options.Profiles[McpMssqlOptions.DefaultProfileName].Analyze.CommandTimeoutSeconds);
    }
}
