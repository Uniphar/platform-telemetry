# OpenTelemetry Guidance

This library sets up OpenTelemetry for .NET applications running in Azure. It sends traces, logs, metrics and custom events to Application Insights.

---

## Setup

Call `RegisterOpenTelemetry` in `Program.cs` and then call `Build()`.

```csharp
builder
    .RegisterOpenTelemetry(appName)
    .WithDiagnosticLogging(true)         // remove or set false in production
    .WithExceptionsFilters(MyExceptionRules.Rules)
    .WithDependencyFilter(DependencyFilterConfiguration.Default)
    .Build();
```

`appName` becomes the cloud role name in Application Insights. It identifies which service the telemetry comes from.

`WithDiagnosticLogging(true)` turns on extra logging to stderr. It writes a line every time a custom event is sent, so you can confirm events are reaching the pipeline. Use this when setting up the service for the first time or when debugging missing telemetry. Turn it off in production to avoid noise in the logs.

---

## What the Library Does

- Sends traces, logs, metrics and custom events to Application Insights
- Sets the cloud role name and pod name on all telemetry
- Disables adaptive sampling so no telemetry is dropped by the SDK
- Increases the batch queue size and reduces the flush interval so data is sent faster
- Converts known expected exceptions into custom events instead of error logs
- Adds ambient properties (such as `JobId` and `JobType`) to all telemetry automatically
- Logs drop events to stdout so you can see them in Kubernetes logs

---

## NuGet Packages

Only add `Azure.Monitor.OpenTelemetry.AspNetCore`. Do not add anything else from the list below.

| Package | Why not |
|---|---|
| `Azure.Monitor.OpenTelemetry.Exporter` | Already included inside the distro. Adding it directly causes version conflicts. |
| `OpenTelemetry.Instrumentation.AspNetCore` | Conflicts with the bundled version. Request telemetry goes missing. |
| `OpenTelemetry.Instrumentation.Http` | Same. HTTP dependency telemetry goes missing. |

This is documented by Microsoft here: https://learn.microsoft.com/en-us/dotnet/api/overview/azure/monitor.opentelemetry.aspnetcore-readme#troubleshooting

---

## Sampling

Sampling means the SDK only sends a percentage of traces to Application Insights. When sampling is on, most traces are silently dropped. No error is shown. The data just does not appear.

This library turns off all sampling by default:

```csharp
options.SamplingRatio = 1.0f;                // send everything
options.TracesPerSecond = null;              // no rate limit
options.EnableTraceBasedLogsSampler = false; // do not drop logs based on trace decisions
```

If you turn sampling on, accept that Application Insights shows a sample of the data, not all of it.

Also check the Application Insights resource in the Azure portal. Go to Configure, then Usage and estimated costs, then Data Sampling. If the value is less than 100%, ingestion sampling is on. This causes a known bug where the SDK stops sending all telemetry for a period of time after receiving a 206 response from the ingestion endpoint. Disable ingestion sampling to avoid this.

---

## Custom Events

Custom events appear in the `customEvents` table in Application Insights. Use `ICustomEventTelemetryClient` to send them.

```csharp
telemetry.TrackEvent("FileMoved", new() { { "FileSize", size } });
```

Custom events use `LogLevel.Critical` on purpose. If your minimum log level is set higher than the level used for Custom Events, events will not be sent.

The library can also convert specific exceptions into custom events. This means the exception does not appear as an error log. Instead it appears as a custom event with a reason field. Configure this with `WithExceptionsFilters`.

---

## Detecting Dropped Telemetry

OpenTelemetry exporter for Azure Monitor might sometimes silently drop the telementry. You will only notice gaps in Application Insights.

This library adds `OtelDropListener` which watches internal OpenTelemetry channels and writes drop events to stdout. You can see them in container logs with the following KQL:

```
ContainerLogV2 
| where LogMessage contains "OtelDropListener"
| order by TimeGenerated desc
```

If you see output, telemetry was dropped. Match the timestamps to the gaps in Application Insights.

Common messages and what they mean:

| Message | Cause |
|---|---|
| `dropped N item(s) due to buffer full` | The batch queue is full. Increase `OTEL_BSP_MAX_QUEUE_SIZE`. |
| `Transmission failed. StatusCode: 206` | Application Insights returned a partial-success response. Can be caused by ingestion sampling but also by malformed telemetry items or transient backend errors. Check the ingestion sampling setting in the Azure portal, but do not assume it is the only cause. |
| `Field 'message' on type 'MessageData' is required` | A log record with an empty message was sent. Check exception converters. |
| `Failed to export` / `Failed to transmit` | Network or authentication problem reaching the ingestion endpoint. |

---

## Troubleshooting

### Telemetry is missing or there are gaps

1. Check pod logs: `kubectl logs <pod> --since=2h | Select-String OtelDropListener`
2. If you see `StatusCode: 206`, Application Insights returned a partial-success response. Check the ingestion sampling setting in the Azure portal, but note that 206 can also be caused by malformed telemetry items or transient backend errors — not only by sampling.
3. If you see `buffer full`, the queue is too small or the service is under heavy load. Increase `OTEL_BSP_MAX_QUEUE_SIZE` and `OTEL_BLRP_MAX_QUEUE_SIZE`.
4. Check that no conflicting packages are referenced (see NuGet Packages section above).

### Requests or dependencies are missing

Check the NuGet packages in the project. If `OpenTelemetry.Instrumentation.AspNetCore` or `OpenTelemetry.Instrumentation.Http` are referenced, remove them. The distro includes these internally.

### Custom events are missing

1. Set `WithDiagnosticLogging(true)` when calling `RegisterOpenTelemetry`. This writes `[TrackEvent]` lines to stderr. Check the pod logs for these lines to confirm events are being emitted.
2. Check that nothing is filtering out `LogLevel.Critical` logs in your logging configuration.

To see the diagnostic trace lines in Log Analytics, query `ContainerLogV2` for `TrackEvent` entries:

```kql
ContainerLogV2 
| where LogMessage contains "TrackEvent"
| where LogMessage has "Job"
| project TimeGenerated, LogMessage
| order by TimeGenerated desc
```

Each matching row confirms a custom event was emitted by the service. If rows are present here but the event does not appear in the `customEvents` table in Application Insights, the event was emitted but dropped or rejected during export.

### Metrics are missing but traces look fine

Metrics are never sampled. If metrics disappear but traces are present, the cause is likely a transmission failure, not sampling. Check pod logs for `Transmission failed` events.

### All telemetry stops for a period

This is the ingestion sampling bug ([azure-sdk-for-net#48141](https://github.com/Azure/azure-sdk-for-net/issues/48141)). The SDK receives a 206 response from Application Insights and stops sending everything for a backoff period. A 206 does not always mean ingestion sampling is enabled — it can also be triggered by malformed telemetry or transient backend errors. Check the ingestion sampling setting in the Azure portal, but also inspect which items caused the partial failure. Set `options.StorageDirectory = null` to break the retry loop.
