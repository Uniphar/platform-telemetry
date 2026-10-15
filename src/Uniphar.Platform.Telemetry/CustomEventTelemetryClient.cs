namespace Uniphar.Platform.Telemetry;

/// <summary>
///     Service to track custom events via OpenTelemetry/Azure Monitor or both as per emit mode configuration.
///     This handles custom events differentation so that they are ingested as spans under OpenTelemetry or custom events
///     in Azure Monitor.
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
///     Service to track custom events via OpenTelemetry.
///     Emits events as activity spans, structured log records, or both depending on <see cref="CustomEventEmitMode" />.
/// </summary>
public sealed class CustomEventTelemetryClient : ICustomEventTelemetryClient
{
    private const string CustomEventAttribute = "microsoft.custom_event.name";

    /// <summary>
    // tells otel it is a custom event not en ERROR, filter on dash0
    /// </summary>
    public const string CustomEventMarkerAttribute = "uniphar.custom.event";

    private const string EventNameAttribute = "event.name";

    /// <summary>
    ///     Default critical to make sure app insights catches them, can be changed later
    /// </summary>
    public const LogLevel DefaultLogLevel = LogLevel.Critical;

    internal static readonly ActivitySource CustomEventActivitySource = new("Uniphar.Platform.Telemetry.CustomEvents");

    private readonly bool _diagnosticLogging;
    private readonly CustomEventEmitMode _emitMode;
    private readonly LogLevel _logLevel;
    private readonly ILogger<CustomEventTelemetryClient>? _logger;

    /// <summary>
    ///     Creates a new instance of <see cref="CustomEventTelemetryClient" />.
    /// </summary>
    /// <param name="diagnosticLogging">Write diagnostic messages to stderr.</param>
    /// <param name="emitMode">Determines which telemetry signals are emitted.</param>
    /// <param name="logger">Logger used for the <see cref="CustomEventEmitMode.LogRecord" /> path.</param>
    /// <param name="logLevel">Severity of the emitted log record, see <see cref="DefaultLogLevel" />.</param>
    public CustomEventTelemetryClient(
        bool diagnosticLogging = false,
        CustomEventEmitMode emitMode = CustomEventEmitMode.ActivitySpan,
        ILogger<CustomEventTelemetryClient>? logger = null,
        LogLevel logLevel = DefaultLogLevel
    )
    {
        _diagnosticLogging = diagnosticLogging;
        _emitMode = emitMode;
        _logger = logger;
        _logLevel = logLevel;
    }

    /// <inheritdoc />
    public void TrackEvent(string eventName, Dictionary<string, object>? state = null)
    {
        try
        {
            var properties = NormalizeProperties(state);

            using var _ = AmbientTelemetryProperties.Initialize(properties);
            LogDiagnostics(eventName);

            if (_emitMode.HasFlag(CustomEventEmitMode.ActivitySpan)) EmitActivitySpan(eventName, properties);

            if (_emitMode.HasFlag(CustomEventEmitMode.LogRecord)) EmitLogRecord(eventName, properties);
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
        if (!_diagnosticLogging) return;

        // Flatten all ambient scopes, deduplicate by key (first occurrence wins, innermost scope is first).
        var stateString = string.Join(", ",
            AmbientTelemetryProperties
                .AmbientProperties
                .SelectMany(x => x.PropertiesToInject)
                .GroupBy(x => x.Key)
                .Select(g => $"{g.Key}={g.First().Value}"));

        // Write directly to stderr so the event is always visible in container logs,
        // independent of the OTLP pipeline.
        Console.Error.WriteLine($"[TrackEvent] {DateTimeOffset.UtcNow:O} {eventName} {{{stateString}}}");
    }

    private static void EmitActivitySpan(string eventName, KeyValuePair<string, string>[] properties)
    {
        using var activity = CustomEventActivitySource.StartActivity(eventName);
        if (activity is null) return;

        activity.SetTag("event.name", eventName);

        foreach (var (key, value) in properties) activity.SetTag(key, value);

        // Inject ambient properties as span tags
        foreach (var (key, value) in AmbientTelemetryProperties.AmbientProperties.SelectMany(p => p.PropertiesToInject)) activity.SetTag(key, value);

        activity.SetTag(CustomEventMarkerAttribute, true);

        // A custom event is a deliberate signal, never a failed operation, so it must not count as a span error.
        activity.SetStatus(ActivityStatusCode.Ok);
    }

    private void EmitLogRecord(string eventName, KeyValuePair<string, string>[] properties)
    {
        // Azure Monitor CustomEvent uses structured log placeholders:
        // "{microsoft.custom_event.name} {key1} {key2} ..." — each becomes a CustomEvent property.
        // event.name and the marker are appended last (typed values) this is for OTEL/dash0
        var custom = properties.Where(x => x.Key is not (EventNameAttribute or CustomEventMarkerAttribute)).ToArray();
        var template = string.Join(" ",
            new[] { $"{{{CustomEventAttribute}}}" }
                .Concat(custom.Select(x => $"{{{x.Key}}}"))
                .Append($"{{{EventNameAttribute}}}")
                .Append($"{{{CustomEventMarkerAttribute}}}"));
        var args = new object?[] { eventName }
            .Concat(custom.Select(x => (object?)x.Value))
            .Append(eventName)
            .Append(true)
            .ToArray();

        _logger?.Log(_logLevel, template, args);
    }
}