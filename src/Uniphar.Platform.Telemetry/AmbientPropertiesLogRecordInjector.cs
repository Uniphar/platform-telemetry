namespace Uniphar.Platform.Telemetry;

/// <summary>
///     Add ambient properties into CustomEvents and Trace/Exception entries
/// </summary>
internal sealed class AmbientPropertiesLogRecordInjector : BaseProcessor<LogRecord>
{
    public override void OnEnd(LogRecord logRecord)
    {
        //inject properties into telemetry
        var activityTags = AmbientTelemetryProperties
            .AmbientProperties
            .SelectMany(x => x.PropertiesToInject)
            .ToArray();

        //merge the existing attributes with ambient properties
        var newAttributes = (logRecord.Attributes ?? [])
            .Concat(activityTags.Select(x => new KeyValuePair<string, object?>(x.Key, x.Value)))
            .GroupBy(x => x.Key)
            .Select(x => x.First())
            .ToList();

        logRecord.Attributes = newAttributes;
        base.OnEnd(logRecord);
    }
}