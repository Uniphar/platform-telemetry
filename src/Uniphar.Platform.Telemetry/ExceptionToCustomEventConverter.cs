using Microsoft.Extensions.Logging;
using OpenTelemetry;
using OpenTelemetry.Logs;
using Uniphar.Platform.Telemetry;

/// <summary>
///     Service to preprocess Exception telemetry and override them to send as Custom Events for specific exceptions.
///     Now supports dynamic exception filtering and handling via lambdas.
/// </summary>
public sealed record ExceptionHandlingRule(
    Func<LogRecord, bool> Predicate,
    Action<LogRecord, ICustomEventTelemetryClient> Handler
);

public class ExceptionToCustomEventConverter(
    IEnumerable<ExceptionHandlingRule> rules,
    ICustomEventTelemetryClient eventTelemetryClient
) : BaseProcessor<LogRecord>
{
    public override void OnEnd(LogRecord logRecord)
    {
        if (logRecord.LogLevel < LogLevel.Error) return;

        foreach (var rule in rules)
        {
            if (rule.Predicate(logRecord))
            {
                rule.Handler(logRecord, eventTelemetryClient);
                logRecord.Attributes = [];
                logRecord.Body = string.Empty;
                logRecord.FormattedMessage = string.Empty;
                logRecord.CategoryName = string.Empty;
                logRecord.Exception = null;
                logRecord.LogLevel = LogLevel.None;
                return;
            }
        }

        base.OnEnd(logRecord);
    }
}