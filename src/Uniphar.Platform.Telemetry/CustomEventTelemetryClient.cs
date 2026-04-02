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
public sealed class CustomEventTelemetryClient(ILogger<CustomEventTelemetryClient> logger) : ICustomEventTelemetryClient
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
                string.IsNullOrWhiteSpace(x.Value.ToString()) ? "n/a" : x.Value.ToString()!))
            .ToArray();

        using var _ = AmbientTelemetryProperties.Initialize(normalizedProperties);

        //this is how OpenTelemetry tracks custom events in AppInsights
        //Note that it is logged as a critical event on purpose.
        //Otherwise, if you use the LogInformation, but LogLevel is set to Error it will not appear in AppInsights.
        logger.LogCritical(CustomEventAttribute, eventName);
    }
}
