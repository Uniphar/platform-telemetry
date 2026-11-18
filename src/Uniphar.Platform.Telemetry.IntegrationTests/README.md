# Integration tests

These tests send real telemetry through the library to Dash0 (dataset `platform-dev`, service `telemetry-app-integration`)
and assert it arrived, using the `Uniphar.Platform.Telemetry.Testing` query client. No application has to be running.

## Run locally

1. Get the Dash0 auth token: Key Vault `uni-core-platform-kv`, secret `dash0-platform-authorization-secret-dev`.
2. Set it in your own environment (never commit it):
   ```powershell
   $env:DASH0_AUTH_TOKEN = '<token>'
   ```
3. Run:
   ```powershell
   dotnet test src/Uniphar.Platform.Telemetry.IntegrationTests -s src/Uniphar.Platform.Telemetry.IntegrationTests/runsettings/dev.runsettings
   ```

`DASH0_API_URL`, `DASH0_OTLP_ENDPOINT` and `DASH0_DATASET` come from the runsettings file.

| Variable | Purpose |
| --- | --- |
| `DASH0_AUTH_TOKEN` | Bearer token for both the Dash0 query API and OTLP ingestion (secret) |
| `DASH0_API_URL` | Dash0 query API base URL |
| `DASH0_OTLP_ENDPOINT` | Dash0 OTLP ingestion endpoint, mapped to `OTEL_EXPORTER_OTLP_ENDPOINT` |
| `DASH0_DATASET` | Dataset to write to and query |

## In CI/CD

The CD workflow runs these tests before publishing the packages. It logs in to Azure, reads the token from Key Vault,
masks it and exports the `DASH0_*` variables, the same approach Frontgate uses. CI only runs the unit tests.

## Writing the same kind of test in your own repository

1. Reference `Uniphar.Platform.Telemetry.Testing`.
2. Provide the four variables above (runsettings for the non-secret ones, Key Vault for the token).
3. Emit telemetry carrying a unique value (e.g. a GUID attribute) so parallel runs and old data never interfere.
4. Query with `Dash0TelemetryQueryClient` and poll with `WaitForCountAsync`, ingestion in Dash0 takes a few seconds.
5. Tag the tests `[TestCategory("Integration")]` so the unit-test run can exclude them.
