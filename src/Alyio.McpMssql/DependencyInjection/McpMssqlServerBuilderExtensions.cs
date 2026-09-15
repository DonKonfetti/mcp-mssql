// MIT License

using System.Text.Json;
using Alyio.McpMssql.Configuration;
using Alyio.McpMssql.Features;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ModelContextProtocol.Server;

#pragma warning disable IDE0130 // Intentional: extension methods for IMcpServerBuilder
namespace Microsoft.Extensions.DependencyInjection;
#pragma warning restore IDE0130

/// <summary>
/// MCP server builder helpers for the MCP SQL Server integration.
/// </summary>
public static partial class McpMssqlServerBuilderExtensions
{
    /// <summary>
    /// Registers the MCP MSSQL tools, advertising the write tool only when at
    /// least one configured profile opts in via
    /// <see cref="McpMssqlProfileOptions.AllowWrite"/>.
    /// </summary>
    /// <param name="builder">The MCP server builder to add the tools to.</param>
    /// <param name="serializerOptions">
    /// The JSON options used to marshal tool arguments and results.
    /// </param>
    /// <returns>The <paramref name="builder"/>, for chaining.</returns>
    /// <remarks>
    /// Visibility is a server-wide decision; authorization stays per-profile, so
    /// <c>run_command</c> is still rejected at call time on profiles that remain
    /// locked. Call after <see cref="McpMssqlOptionsExtensions.AddMcpMssqlOptions"/>
    /// has bound the configuration, which <c>AddMcpMssql</c> does.
    /// </remarks>
    public static IMcpServerBuilder WithMcpMssqlTools(
        this IMcpServerBuilder builder,
        JsonSerializerOptions? serializerOptions = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.WithToolsFromAssembly(typeof(ServerTools).Assembly, serializerOptions);

        // The SDK copies the discovered tools into ToolCollection from an
        // IConfigureOptions<McpServerOptions>, and the options pattern runs every
        // Configure step before any PostConfigure one, so the collection is fully
        // populated here regardless of registration order.
        builder.Services
            .AddOptions<McpServerOptions>()
            .PostConfigure<IOptions<McpMssqlOptions>, ILoggerFactory>(HideWriteToolWhenLocked);

        return builder;
    }

    private static void HideWriteToolWhenLocked(
        McpServerOptions serverOptions,
        IOptions<McpMssqlOptions> mssqlOptions,
        ILoggerFactory loggerFactory)
    {
        if (mssqlOptions.Value.Profiles.Values.Any(profile => profile.AllowWrite))
        {
            return;
        }

        if (serverOptions.ToolCollection is not { } tools
            || !tools.TryGetPrimitive(WriteTools.ToolName, out McpServerTool? tool)
            || tool is null)
        {
            return;
        }

        if (tools.Remove(tool))
        {
            ILogger logger = loggerFactory.CreateLogger(typeof(McpMssqlServerBuilderExtensions));

            LogWriteToolHidden(logger, WriteTools.ToolName);
        }
    }

    [LoggerMessage(
        Level = LogLevel.Information,
        Message = "Tool '{Tool}' is not advertised: no MCP MSSQL profile sets AllowWrite=true.")]
    private static partial void LogWriteToolHidden(ILogger logger, string tool);
}
