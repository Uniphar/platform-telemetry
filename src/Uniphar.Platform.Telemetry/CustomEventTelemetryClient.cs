using Microsoft.Extensions.Logging;

namespace Uniphar.Platform.Telemetry;

/// <summary>
///     Service to track custom events in Application Insights via OpenTelemetry
/// </summary>
public interface ICustomEventTelemetryClient
{
    void TrackEvent(string eventName, object? state = null);
}

public class CustomEventTelemetryClient(ILogger<CustomEventTelemetryClient> logger) : ICustomEventTelemetryClient
{
    public const string CustomEventAttribute = "{microsoft.custom_event.name}";

    public void TrackEvent(string eventName, object? state = null)
    {
        var customProperties = (state ?? new object()).ToDictionary();
        using (logger.BeginScope(customProperties))
            //this is how OpenTelemetry tracks custom events in AppInsights
            //Note that it is logged as a critical event on purpose.
            //Otherwise, if you use the LogInformation, but LogLevel is set to Error it will not appear in AppInsights.
            logger.LogCritical(CustomEventAttribute, eventName);
    }
}