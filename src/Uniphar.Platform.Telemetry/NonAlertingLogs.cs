using System.Collections;
using System.Text.RegularExpressions;

namespace Uniphar.Platform.Telemetry;

/// <summary>
///     Marks error/warning logs as non-alerting. The log (with exception and stack trace) is still exported,
///     but carries the <see cref="MarkerAttribute" /> so alert queries can exclude it.
/// </summary>
public static partial class NonAlertingLogs
{
    /// <summary>
    ///     Attribute added to every non-alerting log. Alert queries should exclude records where it is <c>true</c>.
    /// </summary>
    public const string MarkerAttribute = "telemetry.non_alerting";

    private const string OriginalFormatKey = "{OriginalFormat}";

    /// <summary>
    ///     Logs an error that must stay visible but must not trigger alerts. Supports message templates.
    /// </summary>
    public static void LogNonAlertingError(this ILogger logger, Exception? exception, string message, params object?[] args) =>
        Log(logger, LogLevel.Error, exception, message, args);

    /// <summary>
    ///     Logs a warning that must stay visible but must not trigger alerts. Supports message templates.
    /// </summary>
    public static void LogNonAlertingWarning(this ILogger logger, Exception? exception, string message, params object?[] args) =>
        Log(logger, LogLevel.Warning, exception, message, args);

    private static void Log(ILogger logger, LogLevel level, Exception? exception, string message, object?[] args)
    {
        ArgumentNullException.ThrowIfNull(logger);
        ArgumentException.ThrowIfNullOrWhiteSpace(message);

        if (!logger.IsEnabled(level)) return;

        logger.Log(level, default, new NonAlertingState(message, args), exception, static (state, _) => state.ToString());
    }

    internal static List<KeyValuePair<string, object?>> Merge(
        IReadOnlyList<KeyValuePair<string, object?>>? existing,
        IReadOnlyDictionary<string, object?>? dimensions)
    {
        // existing attributes (including ambient properties) always win over rule dimensions
        var merged = new List<KeyValuePair<string, object?>>(existing ?? []);
        if (dimensions is not null)
            foreach (var (key, value) in dimensions)
                if (key != MarkerAttribute && merged.All(a => a.Key != key))
                    merged.Add(new(key, value));

        merged.RemoveAll(a => a.Key == MarkerAttribute);
        merged.Add(new(MarkerAttribute, true));
        return merged;
    }

    [GeneratedRegex(@"(?<!\{)\{(?:[@$])?(?<name>[^{}:,\s]+)(?<rest>[,:][^{}]*)?\}(?!\})")]
    private static partial Regex PlaceholderRegex();

    // Structured state so OpenTelemetry exports each template argument, the marker and the original template as attributes.
    private sealed class NonAlertingState : IReadOnlyList<KeyValuePair<string, object?>>
    {
        private readonly List<KeyValuePair<string, object?>> _items = [];
        private readonly string _formatted;

        public NonAlertingState(string template, object?[] args)
        {
            var index = 0;
            var composite = PlaceholderRegex().Replace(template, match =>
            {
                var argIndex = index++;
                _items.Add(new(match.Groups["name"].Value, argIndex < args.Length ? args[argIndex] : null));
                return $"{{{argIndex}{match.Groups["rest"].Value}}}";
            });

            _items.Add(new(MarkerAttribute, true));
            _items.Add(new(OriginalFormatKey, template));

            try
            {
                _formatted = string.Format(composite, args);
            }
            catch (FormatException)
            {
                _formatted = template;
            }
        }

        public int Count => _items.Count;

        public KeyValuePair<string, object?> this[int index] => _items[index];

        public IEnumerator<KeyValuePair<string, object?>> GetEnumerator() => _items.GetEnumerator();

        IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

        public override string ToString() => _formatted;
    }
}
