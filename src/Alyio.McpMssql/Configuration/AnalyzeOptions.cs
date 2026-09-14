// MIT License

namespace Alyio.McpMssql.Configuration;

/// <summary>
/// Server-enforced execution defaults for query plan analysis.
/// Analysis runs the full query with <c>SET STATISTICS XML ON</c>
/// to capture the actual execution plan, which can be significantly
/// slower than a bounded interactive query.
/// </summary>
public sealed class AnalyzeOptions
{
    /// <summary>
    /// Maximum execution time for an analysis command, in seconds.
    /// Defaults to 300 (5 minutes) to accommodate complex queries
    /// that need to run to completion for accurate runtime statistics.
    /// Clamped to <see cref="HardCommandTimeoutSeconds"/>.
    /// </summary>
    public int CommandTimeoutSeconds { get; set; } = 300;

    /// <summary>
    /// Absolute, non-configurable hard limit for analysis execution time.
    /// </summary>
    internal const int HardCommandTimeoutSeconds = 600;
}
