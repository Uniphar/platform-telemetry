namespace Uniphar.Platform.Telemetry;

/// <summary>
///     Describes an exception that is expected/benign: matching logs are still exported in full (exception, stack trace),
///     but are tagged with <see cref="NonAlertingLogs.MarkerAttribute" /> and optional custom dimensions so alerts can ignore them.
/// </summary>
public sealed class ExceptionHandlingRule
{
    /// <param name="predicate">Selects the log records to treat as non-alerting.</param>
    /// <param name="dimensions">Optional custom dimensions added to matching logs, e.g. <c>Category = "Restart"</c>.</param>
    /// <param name="setToLogLevel">Level matching logs are exported at; defaults to <see cref="LogLevel.Error" />, or set <see cref="LogLevel.Warning" />.</param>
    public ExceptionHandlingRule(
        Func<LogRecord, bool> predicate,
        IReadOnlyDictionary<string, object?>? dimensions = null,
        LogLevel setToLogLevel = LogLevel.Error)
    {
        ArgumentNullException.ThrowIfNull(predicate);
        if (setToLogLevel is not (Microsoft.Extensions.Logging.LogLevel.Error or Microsoft.Extensions.Logging.LogLevel.Warning))
            throw new ArgumentOutOfRangeException(nameof(setToLogLevel), setToLogLevel, "Only Error or Warning is supported.");

        Predicate = predicate;
        Dimensions = dimensions;
        SetToLogLevel = setToLogLevel;
    }

    /// <summary>Selects the log records to treat as non-alerting.</summary>
    public Func<LogRecord, bool> Predicate { get; }

    /// <summary>Custom dimensions added to matching logs.</summary>
    public IReadOnlyDictionary<string, object?>? Dimensions { get; }

    /// <summary>Level matching logs are exported at (Error or Warning).</summary>
    public LogLevel SetToLogLevel { get; }

    /// <summary>
    ///     Creates a rule matching logs whose exception is a <typeparamref name="TException" />, optionally narrowed by <paramref name="when" />.
    ///     Only the first matching rule applies, so register the most specific rules first.
    /// </summary>
    public static ExceptionHandlingRule For<TException>(
        Func<TException, bool>? when = null,
        IReadOnlyDictionary<string, object?>? dimensions = null,
        LogLevel setToLogLevel = LogLevel.Error) where TException : Exception =>
        new(
            record => record.Exception is TException exception && (when?.Invoke(exception) ?? true),
            dimensions,
            setToLogLevel)
        {
            ExceptionType = typeof(TException),
            IsUnconditional = when is null
        };

    // Only set for rules created via For<TException>, used to detect rules that can never match.
    internal Type? ExceptionType { get; private init; }

    internal bool IsUnconditional { get; private init; }

    /// <summary>
    ///     Throws when a rule can never be reached because an earlier unconditional rule already covers its exception type.
    /// </summary>
    internal static void EnsureReachable(IReadOnlyList<ExceptionHandlingRule> rules)
    {
        for (var i = 0; i < rules.Count; i++)
        {
            if (rules[i].ExceptionType is not { } type) continue;

            var shadowing = rules.Take(i).FirstOrDefault(r => r.IsUnconditional && r.ExceptionType is not null && r.ExceptionType.IsAssignableFrom(type));
            if (shadowing is not null)
                throw new ArgumentException(
                    $"Rule for {type.Name} is unreachable: an earlier unconditional rule for {shadowing.ExceptionType!.Name} already matches it. Register the most specific rules first.",
                    nameof(rules));
        }
    }
}

/// <summary>
///     Tags logs matching an <see cref="ExceptionHandlingRule" /> as non-alerting, keeping the full log record.
/// </summary>
internal sealed class NonAlertingExceptionProcessor(IEnumerable<ExceptionHandlingRule> rules) : BaseProcessor<LogRecord>
{
    public override void OnEnd(LogRecord logRecord)
    {
        if (logRecord.LogLevel >= LogLevel.Error)
        {
            var rule = rules.FirstOrDefault(r => r.Predicate(logRecord));
            if (rule is not null)
            {
                logRecord.Attributes = NonAlertingLogs.Merge(logRecord.Attributes, rule.Dimensions);

                logRecord.LogLevel = rule.SetToLogLevel;
            }
        }

        base.OnEnd(logRecord);
    }
}
