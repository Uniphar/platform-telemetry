# Uniphar.Platform.Telemetry

Uniphar.Platform.Telemetry is a .NET library for enhanced telemetry, logging, and metrics collection. It is designed to be used as a NuGet package in .NET applications to provide structured logging and custom event telemetry.

## Features

- **Custom Event Telemetry Client**: Send custom events to your telemetry backend with structured data.
- **Ambient Properties Log Enricher**: Automatically enriches log records with ambient properties for better traceability.
- **Exception to Custom Event Converter**: Convert exceptions into custom telemetry events for improved error tracking.
- **Telemetry Extensions**: Extension methods for easier integration with logging and telemetry frameworks.

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
