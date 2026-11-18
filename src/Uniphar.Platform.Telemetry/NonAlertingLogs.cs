namespace Uniphar.Platform.Telemetry;

/// <summary>
///     Marks error/warning logs as non-alerting. The log (with exception and stack trace) is still exported,
///     but carries the <see cref="MarkerAttribute" /> so alert queries can exclude it.
/// </summary>
public static class NonAlertingLogs
{
    /// <summary>
    ///     Attribute added to every non-alerting log. Alert queries should exclude records where it is <c>true</c>.
    /// </summary>
    public const string MarkerAttribute = "telemetry.non_alerting";

    /// <summary>
    ///     Logs an error that must stay visible but must not trigger alerts.
    /// </summary>
    public static void LogNonAlertingError(
        this ILogger logger,
        Exception? exception,
        string message,
        IReadOnlyDictionary<string, object?>? dimensions = null) =>
        Log(logger, LogLevel.Error, exception, message, dimensions);

    /// <summary>
    ///     Logs a warning that must stay visible but must not trigger alerts.
    /// </summary>
    public static void LogNonAlertingWarning(
        this ILogger logger,
        Exception? exception,
        string message,
        IReadOnlyDictionary<string, object?>? dimensions = null) =>
        Log(logger, LogLevel.Warning, exception, message, dimensions);

    private static void Log(
        ILogger logger,
        LogLevel level,
        Exception? exception,
        string message,
        IReadOnlyDictionary<string, object?>? dimensions)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        if (!logger.IsEnabled(level)) return;

        logger.Log(level, default, WithMarker(dimensions, message), exception, static (state, _) => (string)state[^1].Value!);
    }

    // The message is kept last so the formatter can read it; "{OriginalFormat}" makes OpenTelemetry use it as the log body.
    private static List<KeyValuePair<string, object?>> WithMarker(IReadOnlyDictionary<string, object?>? dimensions, string message)
    {
        var state = new List<KeyValuePair<string, object?>>();
        if (dimensions is not null)
            state.AddRange(dimensions.Where(d => d.Key != MarkerAttribute && d.Key != "{OriginalFormat}"));
        state.Add(new(MarkerAttribute, true));
        state.Add(new("{OriginalFormat}", message));
        return state;
    }

    internal static List<KeyValuePair<string, object?>> Merge(
        IReadOnlyList<KeyValuePair<string, object?>>? existing,
        IReadOnlyDictionary<string, object?>? dimensions)
    {
        var merged = new List<KeyValuePair<string, object?>>(existing ?? []);
        if (dimensions is not null)
            foreach (var (key, value) in dimensions)
                if (key != MarkerAttribute && merged.All(a => a.Key != key))
                    merged.Add(new(key, value));

        merged.RemoveAll(a => a.Key == MarkerAttribute);
        merged.Add(new(MarkerAttribute, true));
        return merged;
    }
}
