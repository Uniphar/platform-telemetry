# Uniphar.Platform.Telemetry

Uniphar.Platform.Telemetry is a .NET library for enhanced telemetry, logging, and metrics collection. It is designed to be used as a NuGet package in .NET applications to provide structured logging, custom event telemetry, and job metrics.

## Features

- **Ambient Properties Log Enricher**: Automatically enriches log records with ambient properties for better traceability.
- **Custom Event Telemetry Client**: Send custom events to your telemetry backend with structured data.
- **Exception to Custom Event Converter**: Convert exceptions into custom telemetry events for improved error tracking.
- **Job Metrics**: Collect and report metrics for background jobs and processes.
- **Reflection Extensions**: Utilities for working with .NET reflection in telemetry scenarios.
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

// Example usage in a class
public class MyClass
{
    private readonly ICustomEventTelemetryClient _telemetry;
    private readonly JobMetrics _metrics;

    public MyClass(ICustomEventTelemetryClient telemetry, JobMetrics metrics)
    {
        _telemetry = telemetry;
        _metrics = metrics;
    }

    public void DoSomething()
    {
        _telemetry.TrackEvent("DoingSomething", new() { ["Property1"] = "Value" });
        _metrics.TrackExecutionCount(Guid.NewGuid().ToString(), "Example");
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
- `runsettings/` - Test run settings

## Contributing

Contributions are welcome! Please submit issues or pull requests via GitHub.

## License

This project is licensed under the MIT License.
