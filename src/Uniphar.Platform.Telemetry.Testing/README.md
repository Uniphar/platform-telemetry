# Uniphar.Platform.Telemetry.Testing

Helpers to verify in integration tests that telemetry really arrived in Dash0.

```csharp
var client = Dash0TelemetryQueryClient.Create(); // reads DASH0_AUTH_TOKEN, DASH0_API_URL, DASH0_DATASET

var count = await client.WaitForCountAsync(
    ct => client.CountLogsAsync(new Dash0Query(since, "my-service", [Dash0Filter.Is("telemetry.non_alerting", "true")]), ct),
    expectedCount: 1,
    timeout: TimeSpan.FromMinutes(2));
```

- `CountSpansAsync` / `CountLogsAsync` count matching spans (custom events are spans when OTLP is configured) or log records.
- `WaitForCountAsync` polls, because Dash0 ingestion is asynchronous.
- See the integration test README in the platform-telemetry repository for how to run and set up such tests.
