// MIT License

using System.Text.Json;
using Alyio.McpMssql.Internal;
using Microsoft.Extensions.Hosting;

namespace Alyio.McpMssql.Tests.Unit;

public sealed class McpConfigurationTests : IDisposable
{
    private readonly string _directory = Path.Combine(
        Path.GetTempPath(),
        $"mcp-mssql-configuration-tests-{Guid.NewGuid():N}");

    public McpConfigurationTests()
    {
        Directory.CreateDirectory(_directory);
    }

    [Fact]
    public void Configure_Ignores_AppSettings_From_Content_Root()
    {
        WriteConfiguration(
            Path.Combine(_directory, "appsettings.json"),
            "from-working-directory");
        var builder = CreateBuilder();

        McpConfiguration.Configure(
            builder,
            Path.Combine(_directory, "missing-user-config.json"),
            []);

        Assert.Null(builder.Configuration["McpMssql:Profiles:default:ConnectionString"]);
    }

    [Fact]
    public void Configure_Loads_Explicit_User_Configuration()
    {
        var userConfigPath = Path.Combine(_directory, "user-config.json");
        WriteConfiguration(userConfigPath, "from-user-config");
        var builder = CreateBuilder();

        McpConfiguration.Configure(builder, userConfigPath, []);

        Assert.Equal(
            "from-user-config",
            builder.Configuration["McpMssql:Profiles:default:ConnectionString"]);
    }

    public void Dispose()
    {
        Directory.Delete(_directory, recursive: true);
    }

    private HostApplicationBuilder CreateBuilder()
    {
        return Host.CreateApplicationBuilder(new HostApplicationBuilderSettings
        {
            ContentRootPath = _directory,
            EnvironmentName = Environments.Production,
        });
    }

    private static void WriteConfiguration(string path, string connectionString)
    {
        var configuration = new
        {
            McpMssql = new
            {
                Profiles = new
                {
                    Default = new
                    {
                        ConnectionString = connectionString,
                    },
                },
            },
        };

        File.WriteAllText(path, JsonSerializer.Serialize(configuration));
    }
}
