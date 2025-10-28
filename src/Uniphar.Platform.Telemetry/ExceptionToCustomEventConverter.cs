using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;

namespace Uniphar.Platform.Telemetry;

/// <summary>
///     Service to preprocess Exception telemetry and override them to send as Custom Events for specific exceptions.
/// </summary>
public class ExceptionToCustomEventConverter(ICustomEventTelemetryClient eventTelemetryClient) : BaseProcessor<LogRecord>
{
    public override void OnEnd(LogRecord logRecord)
    {
        if (logRecord.LogLevel < LogLevel.Error) return;

        var exception = logRecord.Exception;

        // Filter out file locked exception and send them as custom event, not an exception telemetry
        if (exception is IOException && exception.Message.Contains("being used by another process"))
        {
            eventTelemetryClient.TrackEvent("IoLock", new() { ["Exception"] = exception.Message });

            // Suppress the original log
            logRecord.Attributes = [];
            logRecord.Body = string.Empty;
            logRecord.FormattedMessage = string.Empty;
            logRecord.CategoryName = string.Empty;
            logRecord.Exception = null;
            logRecord.LogLevel = LogLevel.None; // Set to None to prevent further processing
            return;
        }

        // Otherwise, let the log go through
        base.OnEnd(logRecord);
    }
}