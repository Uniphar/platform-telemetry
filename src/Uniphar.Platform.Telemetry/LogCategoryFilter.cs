namespace Uniphar.Platform.Telemetry;

/// <summary>
///     Configuration for filtering log categories by minimum log level.
/// </summary>
/// <param name="CategoryName">The log category name to filter (e.g., "Azure", "Azure.Identity")</param>
/// <param name="MinimumLevel">The minimum log level to record for this category</param>
public sealed record LogCategoryFilter(string CategoryName, LogLevel MinimumLevel)
{
    /// <summary>
    ///     Default log category filters that suppress Azure SDK informational logs
    ///     to prevent duplicate telemetry (Azure SDK operations are tracked as dependencies).
    /// </summary>
    public static IEnumerable<LogCategoryFilter> AzureSdkDefaults =>
    [
        new LogCategoryFilter("Azure", LogLevel.Warning),
        new LogCategoryFilter("Azure.Core", LogLevel.Warning),
        new LogCategoryFilter("Azure.Identity", LogLevel.Warning)
    ];
}
