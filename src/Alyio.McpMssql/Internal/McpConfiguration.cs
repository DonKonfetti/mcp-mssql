// MIT License

using System.Reflection;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Alyio.McpMssql.Internal;

internal static class McpConfiguration
{
    public static void Configure(
        HostApplicationBuilder builder,
        string userConfigPath,
        string[] args)
    {
        builder.Configuration.Sources.Clear();
        builder.Configuration.AddJsonFile(
            userConfigPath,
            optional: true,
            reloadOnChange: false);

        if (builder.Environment.IsDevelopment())
        {
            builder.Configuration.AddUserSecrets(
                Assembly.GetExecutingAssembly(),
                optional: true,
                reloadOnChange: false);
        }

        builder.Configuration
            .AddEnvironmentVariables()
            .AddCommandLine(args);
    }
}
