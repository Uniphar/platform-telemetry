namespace Uniphar.Platform.Telemetry;

/// <summary>
///     Determines how custom events are emitted by <see cref="CustomEventTelemetryClient" />.
///     The mode is auto-selected based on the configured exporter(s).
/// </summary>
[Flags]
public enum CustomEventEmitMode
{
    /// <summary>
    ///     Emit as an OpenTelemetry Activity span (ideal for OTLP/distributed tracing backends).
    /// </summary>
    ActivitySpan = 1,

    /// <summary>
    ///     Emit as a structured log record (ideal for Azure Monitor customEvents/traces table).
    /// </summary>
    LogRecord = 2,

    /// <summary>
    ///     Emit both an Activity span and a structured log record (used when both exporters are active).
    /// </summary>
    Both = ActivitySpan | LogRecord
}
