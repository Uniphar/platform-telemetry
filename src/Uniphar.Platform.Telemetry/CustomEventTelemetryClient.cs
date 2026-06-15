namespace Uniphar.Platform.Telemetry;

/// <summary>
///     Service to track custom events via OpenTelemetry
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
///     Service to track custom events via OpenTelemetry Activity spans.
/// </summary>
public sealed class CustomEventTelemetryClient(bool diagnosticLogging = false) : ICustomEventTelemetryClient
{
    internal static readonly ActivitySource CustomEventActivitySource = new("Uniphar.Platform.Telemetry.CustomEvents");

    /// <inheritdoc />
    public void TrackEvent(string eventName, Dictionary<string, object>? state = null)
    {
        try
        {
            var properties = NormalizeProperties(state);

            using var _ = AmbientTelemetryProperties.Initialize(properties);
            LogDiagnostics(eventName);
            EmitActivitySpan(eventName, properties);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[TrackEventError] {DateTimeOffset.UtcNow:O} failed to track '{eventName}': {ex}");
            throw;
        }
    }

    private static KeyValuePair<string, string>[] NormalizeProperties(Dictionary<string, object>? state) =>
        (state ?? [])
            .Select(x => new KeyValuePair<string, string>(x.Key, x.Value.ToString() ?? string.Empty))
            .ToArray();

    private void LogDiagnostics(string eventName)
    {
        if (!diagnosticLogging) return;

        // Flatten all ambient scopes, deduplicate by key (first occurrence wins, innermost scope is first).
        var stateString = string.Join(", ", AmbientTelemetryProperties.AmbientProperties
            .SelectMany(x => x.PropertiesToInject)
            .GroupBy(x => x.Key)
            .Select(g => $"{g.Key}={g.First().Value}"));

        // Write directly to stderr so the event is always visible in container logs,
        // independent of the OTLP pipeline.
        Console.Error.WriteLine($"[TrackEvent] {DateTimeOffset.UtcNow:O} {eventName} {{{stateString}}}");
    }

    private static void EmitActivitySpan(string eventName, KeyValuePair<string, string>[] properties)
    {
        using var activity = CustomEventActivitySource.StartActivity(eventName, ActivityKind.Internal);
        if (activity is null) return;

        activity.SetTag("event.name", eventName);

        foreach (var (key, value) in properties)
            activity.SetTag(key, value);

        // Inject ambient properties as span tags
        foreach (var (key, value) in AmbientTelemetryProperties.AmbientProperties.SelectMany(p => p.PropertiesToInject))
            activity.SetTag(key, value);
    }
}
