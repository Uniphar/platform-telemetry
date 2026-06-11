namespace Uniphar.Platform.Telemetry;

public sealed class OtelDropListener : EventListener
{
    // https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry/Internal/OpenTelemetrySdkEventSource.cs
    private static readonly HashSet<int> SdkEventIds =
    [
        4,  // SpanProcessorException
        28, // TracerProviderException
        32, // ExistsDroppedExportProcessorItems
        34, // MetricReaderException
        35, // MeterProviderException
        36, // MeasurementDropped
        50, // LoggerProviderException
    ];

    // https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/monitor/Azure.Monitor.OpenTelemetry.Exporter/src/Internals/Diagnostics/AzureMonitorExporterEventSource.cs
    private static readonly HashSet<int> ExporterEventIds =
    [
        6,  // ExportFailed
        7,  // FailedToTransmit
        8,  // TransmissionFailed
        14, // FailedToConvertLogRecord
        15, // FailedToConvertMetricPoint
        16, // FailedToConvertActivity
        17, // FailedToExtractActivityEvent
        18, // MaxActivityLinksExceeded
        21, // FailedToCreateStorageDirectory
        25, // FailedToTransmitFromStorage
        29, // FailedToInitializePersistentStorage
        33, // TransmitterFailed
        39, // PartialSuccessUnhandledStatusCode
    ];

    private static readonly Dictionary<string, HashSet<int>> WatchedSourceEvents = new()
    {
        ["OpenTelemetry-Sdk"] = SdkEventIds,
        ["OpenTelemetry-AzureMonitor-Exporter"] = ExporterEventIds,
    };

    protected override void OnEventSourceCreated(EventSource source)
    {
        if (WatchedSourceEvents.ContainsKey(source.Name))
        {
            EnableEvents(source, EventLevel.Warning);
        }
    }

    protected override void OnEventWritten(EventWrittenEventArgs e)
    {
        if (!WatchedSourceEvents.TryGetValue(e.EventSource.Name, out var eventIds))
            return;

        if (!eventIds.Contains(e.EventId))
            return;

        var msg = e.Message is not null && e.Payload?.Count > 0
            ? string.Format(e.Message, [.. e.Payload])
            : e.EventName;

        Console.WriteLine($"{nameof(OtelDropListener)} [{e.Level}] {e.EventSource.Name}: {msg}");
    }
}
