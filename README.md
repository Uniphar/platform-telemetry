# Uniphar.Platform.Telemetry

## Latest Version: [![Nuget](https://img.shields.io/nuget/vpre/Uniphar.Platform.Telemetry.svg)](https://www.nuget.org/packages/Uniphar.Platform.Telemetry/)

Uniphar.Platform.Telemetry is a .NET library for enhanced telemetry, logging, and metrics collection. It is designed to be used as a NuGet package in .NET applications to provide structured logging and custom event telemetry.

## Features

- **Custom Event Telemetry Client**: Send custom events to your telemetry backend with structured data.
- **Ambient Properties Log Enricher**: Automatically enriches log records with ambient properties for better traceability.
- **Non-Alerting Exceptions and Logs**: Mark expected exceptions (or log explicitly with `LogNonAlertingError`/`LogNonAlertingWarning`) so the full log and stack trace stay visible but are tagged `telemetry.non_alerting=true` for exclusion from alerts.
- **Telemetry Extensions**: Extension methods for easier integration with logging and telemetry frameworks.
- **Configurable Dependency Filter**: Filter out specific HTTP error codes (401, 403, 409, etc.) from Azure resource dependency telemetry to prevent them from being recorded as failed dependencies in Application Insights.

## Installation

Install via NuGet:

```powershell
Install-Package Uniphar.Platform.Telemetry
```

Or using the .NET CLI:

```bash
dotnet add package Uniphar.Platform.Telemetry
```

## Usage

### Prerequisities/Exporters

Depending on the telemetry stack you're targetting, following configurations/environment variables are checked to determine which exporters to enable

- **APPLICATIONINSIGHTS_CONNECTION_STRING** - to enable Azure Monitor (or supply directly via `WithAppInsightsConnectionString`)
- **OTEL_EXPORTER_OTLP_ENDPOINT** - to enable OTLP (OpenTelemetry) generic flow

Both can be used at the same time.

### Basic Setup

Register OpenTelemetry in your application's startup/configuration:

```csharp
using Uniphar.Platform.Telemetry;

// Basic registration with default settings (filters out /health paths by default)
builder.RegisterOpenTelemetry("my-application").Build();
```

### Advanced Configuration

Use the fluent API to configure exception filters and path exclusions:

```csharp
using Uniphar.Platform.Telemetry;

// Mark expected exceptions as non-alerting: full log + stack trace are kept, tagged telemetry.non_alerting=true.
// Only the FIRST matching rule applies, so list the most specific rules first (a rule shadowed by an earlier
// unconditional rule for the same or a base exception type throws when registered).
var exceptionRules = new[]
{
    ExceptionHandlingRule.For<IOException>(
        e => e.Message.Contains("being used by another process"),
        new Dictionary<string, object?> { ["Category"] = "Restart" },
        setToLogLevel: LogLevel.Warning), // Error (default) or Warning
    ExceptionHandlingRule.For<ArgumentException>()
};

// Or log directly as non-alerting (supports message templates)
logger.LogNonAlertingError(ex, "Worker {WorkerId} restarting", workerId);
logger.LogNonAlertingWarning(ex, "Slow restart");

// Define paths to exclude from telemetry (e.g., health checks)
var pathsToFilterOutStartingWith = new[] { "/health", "/metrics", "/status" };

// Register with fluent API
builder.RegisterOpenTelemetry("my-application")
    .WithExceptionsFilters(exceptionRules)
    .WithFilterExclusion(pathsToFilterOutStartingWith)
    .Build();
```

### Console Log Suppression

The `Uniphar.Platform.Telemetry` package removes all default logging providers (Console, Debug, EventSource) via `ClearProviders()` when `Build()` is called, regardless of build configuration. This prevents application logs from writing to stdout/stderr, eliminating duplicate data in ContainerLogsV2/ADX — all telemetry is exported directly via the configured OpenTelemetry exporter(s).

Log level filtering should be configured in your application's `appsettings.json`:

```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Warning",
      "Azure": "Warning",
      "Azure.Core": "Warning",
      "Azure.Identity": "Warning"
    }
  }
}
```

### Using the Fluent API

The `RegisterOpenTelemetry` method returns a `TelemetryBuilder` that allows you to chain configuration methods:

- **`.WithExceptionsFilters(IEnumerable<ExceptionHandlingRule>)`**: Mark matching exceptions as non-alerting (logs keep the full stack trace and get `telemetry.non_alerting=true`; alerts should exclude it). Can only be called once. Rule dimensions never override attributes already on the log, including ambient properties from `WithProperties`.
- **`.WithDiagnosticLogging(bool)`**: Write a `[TrackEvent]` line to stderr for every custom event, to confirm events are emitted
- **`.WithFilterExclusion(IEnumerable<string>)`**: Configure paths to exclude from successful request telemetry (defaults to `/health`)
- **`.WithDependencyFilter(...)`**: Configure HTTP dependency error filtering
- **`.WithAppInsightsConnectionString(string)`**: Supply the Application Insights connection string directly, bypassing the default `APPLICATIONINSIGHTS_CONNECTION_STRING` config/environment variable lookup
- **`.Build()`**: Finalize and apply the telemetry configuration. It must be called last and only once; calling it twice throws, and host startup fails if it was never called

You can chain these methods in any order:

```csharp
// With exception filters only
builder.RegisterOpenTelemetry("my-application")
    .WithExceptionsFilters(exceptionRules)
    .Build();

// With path exclusions only
builder.RegisterOpenTelemetry("my-application")
    .WithFilterExclusion(new[] { "/health", "/metrics" })
    .Build();

// Combining multiple configurations
builder.RegisterOpenTelemetry("my-application")
    .WithExceptionsFilters(exceptionRules)
    .WithFilterExclusion(new[] { "/health", "/metrics" })
    .WithDependencyFilter()
    .Build();

// Supply the Application Insights connection string directly
builder.RegisterOpenTelemetry("my-application")
    .WithAppInsightsConnectionString("InstrumentationKey=...;IngestionEndpoint=...")
    .Build();
```

### Configurable HTTP Dependency Error Filtering

`WithDependencyFilter` lets you filter out specific HTTP error codes from Azure resource operations to prevent them from being recorded as failed dependencies in Application Insights. This is useful for scenarios where certain error codes are expected and should not trigger alerts. Calling `WithDependencyFilter()` without arguments uses `DependencyFilterConfiguration.Default` (409 for Storage and Service Bus).

#### Supported Azure Resource Namespaces

Rules target an Azure resource namespace, see `AzureResourceNamespaces`:

- **Storage** (`Microsoft.Storage`) - Azure Storage (Blob, File Share, Queue, Table)
- **ContainerRegistry** (`Microsoft.ContainerRegistry`) - Azure Container Registry
- **ServiceBus** (`Microsoft.ServiceBus`) - Azure Service Bus

#### Configuration Examples

```csharp
var config = new DependencyFilterConfiguration
{
    Rules =
    [
        new DependencyFilterRule
        {
            ResourceNamespace = AzureResourceNamespaces.Storage,
            StatusCodes = [409]
        },
        new DependencyFilterRule
        {
            ResourceNamespace = AzureResourceNamespaces.ContainerRegistry,
            StatusCodes = [401]
        }
    ]
};

builder.RegisterOpenTelemetry("my-application")
    .WithDependencyFilter(config)
    .Build();
```

#### How It Works

When a dependency call is made to a supported Azure resource and results in one of the configured HTTP status codes:

1. The processor reads the Azure resource namespace from the activity's `az.namespace` (or `azure.resource_provider.namespace`) tag; activities without it are left alone
2. It checks if there's a matching filter rule for that namespace
3. If the activity's status code matches a configured code in the rule, the activity is marked as not recorded. The status code is taken from `error.type` when it is numeric (e.g. `"409"`), otherwise from `http.response.status_code`
4. This prevents the failed dependency from appearing in your Application Insights telemetry

This is particularly useful for:

- **409 Conflict**: Expected when creating resources that already exist (Blob containers, File shares, etc.)
- **401 Unauthorized**: Expected during authentication retries or token refresh scenarios
- **403 Forbidden**: Expected when checking permissions or during role-based access control flows

#### Common Scenarios

**Scenario 1: Filter all conflict errors from Azure Storage**

```csharp
.WithDependencyFilter(new DependencyFilterConfiguration
{
    Rules = [new() { ResourceNamespace = AzureResourceNamespaces.Storage, StatusCodes = [409] }]
})
```

**Scenario 2: Filter authentication errors from external services**

```csharp
.WithDependencyFilter(new DependencyFilterConfiguration
{
    Rules =
    [
        new() { ResourceNamespace = AzureResourceNamespaces.ContainerRegistry, StatusCodes = [401, 403] },
        new() { ResourceNamespace = AzureResourceNamespaces.ServiceBus, StatusCodes = [401] }
    ]
})
```

**Scenario 3: Comprehensive filtering for production environments**

```csharp
.WithDependencyFilter(new DependencyFilterConfiguration
{
    Rules =
    [
        new() { ResourceNamespace = AzureResourceNamespaces.Storage, StatusCodes = [409] },
        new() { ResourceNamespace = AzureResourceNamespaces.ContainerRegistry, StatusCodes = [401, 403, 409] },
        new() { ResourceNamespace = AzureResourceNamespaces.ServiceBus, StatusCodes = [401, 403] }
    ]
})
```

### Using Custom Event Telemetry

How a custom event is emitted depends on the configured exporters:

- **Application Insights only**: a log record at `LogLevel.Critical` (so it is not dropped by log level filtering). Make sure your logging configuration does not filter out `Critical`.
- **OTLP configured (alone or with Application Insights)**: both an activity span and a log record at `LogLevel.Information`.

Example usage in a class (properties with a `null` value are not emitted):

```csharp
public class MyClass
{
    private readonly ICustomEventTelemetryClient _telemetry;

    public MyClass(ICustomEventTelemetryClient telemetry)
    {
        _telemetry = telemetry;
    }

    public void DoSomething()
    {
        _telemetry.TrackEvent("DoingSomething", new() { ["Property1"] = "Value" });
    }

    public async Task DoSomethingWithAmbientProperties()
    {
        // Use WithProperties to set ambient properties
        // All telemetry events within the current async context will have these additional properties (which are removed/disposed of after the using block).
        using (_telemetry.WithProperties(new Dictionary<string, string> { ["UserId"] = "12345", ["TenantId"] = "tenant-abc" }))
        {
            // All telemetry tracked within this scope will include UserId and TenantId
            _telemetry.TrackEvent("ProcessStarted", new() { ["Status"] = "Initiated" });
            await ProcessDataAsync();
            _telemetry.TrackEvent("ProcessCompleted", new() { ["Status"] = "Success" });
        }
    }

    private async Task ProcessDataAsync()
    {
        // This event will also include the ambient properties (UserId, TenantId)
        _telemetry.TrackEvent("DataProcessing", new() { ["RecordCount"] = "100" });
        await Task.Delay(100);
    }
}
```

## Building

To build the project:

```powershell
cd src/Uniphar.Platform.Telemetry
dotnet build
```

## Testing

Unit tests are provided in the `Uniphar.Platform.Telemetry.Tests` project:

```powershell
cd src/Uniphar.Platform.Telemetry.Tests
dotnet test
```

## Project Structure

- `Uniphar.Platform.Telemetry/` - Main library source code
- `Uniphar.Platform.Telemetry.Tests/` - Unit tests

## Contributing

Contributions are welcome! Please submit issues or pull requests via GitHub.

## License

This project is licensed under the MIT License.
