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

Add a reference to the package in your project. Example usage:

```csharp
using Uniphar.Platform.Telemetry;

// Register OpenTelemetry in your application's startup/configuration
builder.RegisterOpenTelemetry("my-application");

// Or with custom exception handling rules
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

builder.RegisterOpenTelemetry("my-application", exceptionRules);

// Example usage in a class
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
