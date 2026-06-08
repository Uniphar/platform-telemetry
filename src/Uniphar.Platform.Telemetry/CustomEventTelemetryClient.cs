namespace Uniphar.Platform.Telemetry;

/// <summary>
///     Service to track custom events in Application Insights via OpenTelemetry
/// </summary>
public interface ICustomEventTelemetryClient
{
    /// <summary>
    ///     Track a custom event with optional state properties.
    /// </summary>
    /// <param name="eventName"></param>
    /// <param name="state"></param>
    void TrackEvent(string eventName, Dictionary<string, object>? state = null);
}

/// <summary>
///     Service to track custom events in Application Insights via OpenTelemetry
/// </summary>
public sealed class CustomEventTelemetryClient(ILogger<CustomEventTelemetryClient> logger, bool diagnosticLogging = false) : ICustomEventTelemetryClient
{
    private const string CustomEventAttribute = "{microsoft.custom_event.name}";

    /// <summary>
    ///     Service to track custom events in Application Insights via OpenTelemetry
    /// </summary>
    public void TrackEvent(string eventName, Dictionary<string, object>? state = null)
    {
        var customProperties = state ?? new Dictionary<string, object>();
        var normalizedProperties = customProperties
            .Select(x => new KeyValuePair<string, string>(
                x.Key,
                x.Value == null ? "n/a" : x.Value.ToString()!))
            .ToArray();

        using var _ = AmbientTelemetryProperties.Initialize(normalizedProperties);

        // fallback
        foreach (var (key, value) in normalizedProperties)
            Activity.Current?.SetTag(key, value);

        // Write directly to stderr so the event is always visible in container logs,
        // independent of the AppInsights pipeline.
        // This to cross-check: if the line appears in container logs but not in AppInsights
        if (diagnosticLogging)
        {
            // Flatten all ambient scopes, deduplicate by key (first occurrence wins, innermost scope is first).
            var stateString = string.Join(", ", AmbientTelemetryProperties.AmbientProperties
                .SelectMany(x => x.PropertiesToInject)
                .GroupBy(x => x.Key)
                .Select(g => $"{g.Key}={g.First().Value}"));
            Console.Error.WriteLine($"[TrackEvent] {DateTimeOffset.UtcNow:O} {eventName} {{{stateString}}}");
        }

        //this is how OpenTelemetry tracks custom events in AppInsights
        //Note that it is logged as a critical event on purpose.
        //Otherwise, if you use the LogInformation, but LogLevel is set to Error it will not appear in AppInsights.
        logger.LogCritical(CustomEventAttribute, eventName);
    }
}
