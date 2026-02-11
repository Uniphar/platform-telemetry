# Uniphar.Platform.Telemetry

Uniphar.Platform.Telemetry is a .NET library for enhanced telemetry, logging, and metrics collection. It is designed to be used as a NuGet package in .NET applications to provide structured logging and custom event telemetry.

## Features

- **Custom Event Telemetry Client**: Send custom events to your telemetry backend with structured data.
- **Ambient Properties Log Enricher**: Automatically enriches log records with ambient properties for better traceability.
- **Exception to Custom Event Converter**: Convert exceptions into custom telemetry events for improved error tracking.
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

// Define custom exception handling rules
var exceptionRules = new[]
{
    new ExceptionHandlingRule(
        logRecord => logRecord.Exception is IOException &&
                     logRecord.Exception.Message.Contains("being used by another process"),
        (logRecord, client) => client.TrackEvent("IoLock", new() { ["Exception"] = logRecord.Exception?.Message })
    ),
    new ExceptionHandlingRule(
        logRecord => logRecord.Exception is ArgumentException,
        (logRecord, client) => client.TrackEvent("ArgumentError", new() { ["Exception"] = logRecord.Exception?.Message })
    )
};

// Define paths to exclude from telemetry (e.g., health checks)
var pathsToFilterOutStartingWith = new[] { "/health", "/metrics", "/status" };

// Register with fluent API
builder.RegisterOpenTelemetry("my-application")
    .WithExceptionsFilters(exceptionRules)
    .WithFilterExclusion(pathsToFilterOutStartingWith)
    .Build();
```

### Using the Fluent API

The `RegisterOpenTelemetry` method returns a `TelemetryBuilder` that allows you to chain configuration methods:

- **`.WithExceptionsFilters(IEnumerable<ExceptionHandlingRule>)`**: Configure exception handling rules
- **`.WithFilterExclusion(IEnumerable<string>)`**: Configure paths to exclude from telemetry
- **`.WithDependencyFilter(...)`**: Configure HTTP dependency error filtering.
- **`.Build()`**: Finalize and apply the telemetry configuration (must be called last)

You can chain these methods in any order, and you can use one, both, or neither:

```csharp
// With exception filters only
builder.RegisterOpenTelemetry("my-application")
    .WithExceptionsFilters(exceptionRules)
    .Build();

// With path exclusions only
builder.RegisterOpenTelemetry("my-application")
    .WithFilterExclusion(new[] { "/health", "/metrics" })
    .Build();

// With both
builder.RegisterOpenTelemetry("my-application")
    .WithExceptionsFilters(exceptionRules)
    .WithFilterExclusion(new[] { "/health", "/metrics" })
    .Build();
```

### Configurable HTTP Dependency Error Filtering

The `ConfigurableDependencyTelemetryProcessor` allows you to filter out specific HTTP error codes from Azure resource operations to prevent them from being recorded as failed dependencies in Application Insights. This is useful for scenarios where certain error codes are expected and should not trigger alerts.

#### Supported Azure Resource Types

- **Storage** - Azure Storage (Azure Blob, File Share etc.)
- **ContainerRegistry** - Azure Container Registry
- **ServiceBus** - Azure Service Bus
#### Configuration Examples

```csharp
var config = new DependencyFilterConfiguration
{
    Rules = 
    [
        new DependencyFilterRule
        {
            ResourceType = AzureResourceType.BlobStorage,
            StatusCodesToFilter = [409]
        },
        new DependencyFilterRule
        {
            ResourceType = AzureResourceType.ContainerRegistry,
            StatusCodesToFilter = [401]
        }
    ]
};

builder.RegisterOpenTelemetry("my-application")
    .WithDependencyFilter(config)
    .Build();
```

#### How It Works

When a dependency call is made to a supported Azure resource and results in one of the configured HTTP status codes:

1. The processor identifies the Azure resource type based on the activity's display name or URL
2. It checks if there's a matching filter rule for that resource type
3. If the HTTP status code matches a configured code in the rule, the activity is marked as unrecorded
4. This prevents the failed dependency from appearing in your Application Insights telemetry

This is particularly useful for:
- **409 Conflict**: Expected when creating resources that already exist (Blob containers, File shares, etc.)
- **401 Unauthorized**: Expected during authentication retries or token refresh scenarios
- **403 Forbidden**: Expected when checking permissions or during role-based access control flows

#### Common Scenarios

**Scenario 1: Filter all conflict errors from Azure Storage**
```csharp
.WithDependencyFilter(filter => filter
    .FilterBlobStorage(409)
    .FilterFileShare(409)
    .FilterQueueStorage(409)
    .FilterTableStorage(409)
)
```

**Scenario 2: Filter authentication errors from external services**
```csharp
.WithDependencyFilter(filter => filter
    .FilterContainerRegistry(401, 403)
    .FilterServiceBus(401)
)
```

**Scenario 3: Comprehensive filtering for production environments**
```csharp
.WithDependencyFilter(filter => filter
    .FilterBlobStorage(409)
    .FilterFileShare(409)
    .FilterContainerRegistry(401, 403, 409)
    .FilterServiceBus(401, 403)
    .FilterQueueStorage(409)
)
```

### Using Custom Event Telemetry

Example usage in a class:

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
        using (_telemetry.WithProperties(new() {["UserId"] = "12345", ["TenantId"] = "tenant-abc" }))
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
