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
public sealed class CustomEventTelemetryClient : ICustomEventTelemetryClient
{
    private readonly ActivitySource _activitySource;

    /// <summary>
    /// Initializes a new instance of the CustomEventTelemetryClient class.
    /// </summary>
    /// <param name="serviceName">The name of the service for telemetry tracking</param>
    internal CustomEventTelemetryClient(string serviceName)
    {
        _activitySource = new($"{serviceName}.CustomEvents");
    }

    /// <inheritdoc />
    public void TrackEvent(string eventName, Dictionary<string, object>? state = null)
    {
        // Use ActivitySource to create a custom event that goes to Application Insights
        // but does NOT go to console logs or ContainerLogV2
        using var activity = _activitySource.StartActivity(
            name: eventName,
            kind: ActivityKind.Internal
        );

        if (activity is null)
            return;

        // Add custom event marker for Application Insights
        activity.SetTag("microsoft.custom_event.name", eventName);

        // Add all custom properties as tags
        if (state is null) return;
        foreach (var (key, value) in state)
        {
            activity.SetTag(key, value);
        }
    }
}