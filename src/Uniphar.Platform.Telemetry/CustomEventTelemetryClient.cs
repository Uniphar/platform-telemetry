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
    private const string CustomEventAttribute = "microsoft.custom_event.name";

    /// <inheritdoc />
    public void TrackEvent(string eventName, Dictionary<string, object>? state = null)
    {
        try
        {
            var properties = NormalizeProperties(state);

            using var _ = AmbientTelemetryProperties.Initialize(properties);
            SetActivityTagFallbacks(properties);
            LogDiagnostics(eventName);
            EmitLogRecord(eventName, properties);
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

    private static void SetActivityTagFallbacks(KeyValuePair<string, string>[] properties)
    {
        foreach (var (key, value) in properties)
            Activity.Current?.SetTag(key, value);
    }

    private void LogDiagnostics(string eventName)
    {
        if (!diagnosticLogging) return;

        // Flatten all ambient scopes, deduplicate by key (first occurrence wins, innermost scope is first).
        var stateString = string.Join(", ", AmbientTelemetryProperties.AmbientProperties
            .SelectMany(x => x.PropertiesToInject)
            .GroupBy(x => x.Key)
            .Select(g => $"{g.Key}={g.First().Value}"));

        // Write directly to stderr so the event is always visible in container logs,
        // independent of the AppInsights pipeline.
        Console.Error.WriteLine($"[TrackEvent] {DateTimeOffset.UtcNow:O} {eventName} {{{stateString}}}");
    }

    private void EmitLogRecord(string eventName, KeyValuePair<string, string>[] properties)
    {
        // Azure Monitor CustomEvent uses structured log placeholders:
        // "{microsoft.custom_event.name} {key1} {key2} ..." — each becomes a CustomEvent property.
        var template = string.Join(" ", new[] { $"{{{CustomEventAttribute}}}" }.Concat(properties.Select(x => $"{{{x.Key}}}")));
        var args = new object?[] { eventName }.Concat(properties.Select(x => (object?)x.Value)).ToArray();

        // LogCritical is intentional — lower severities may be filtered out before reaching AppInsights.
        logger.LogCritical(template, args);
    }
}
