using AwesomeAssertions;
using Microsoft.Extensions.Logging.Testing;

namespace Uniphar.Platform.Telemetry.Tests;

[TestClass]
[TestCategory("Unit")]
public class CustomEventTelemetryClientTests
{
    [TestMethod]
    public void TrackEvent_ActivitySpanMode_ShouldEmitActivitySpanWithCustomDimensions()
    {
        // Arrange
        var exportedActivities = new List<Activity>();
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("Uniphar.Platform.Telemetry.CustomEvents")
            .AddInMemoryExporter(exportedActivities)
            .Build();

        var sut = new CustomEventTelemetryClient(emitMode: CustomEventEmitMode.ActivitySpan);

        // Act
        sut.TrackEvent("MyCustomEvent", new Dictionary<string, object> { ["reason"] = "error" });

        // Force flush to ensure spans are exported
        tracerProvider.ForceFlush();

        // Assert
        var span = exportedActivities.Single(a => a.DisplayName == "MyCustomEvent");
        span.Tags.Should().Contain(t => t.Key == "event.name" && t.Value == "MyCustomEvent");
        span.Tags.Should().Contain(t => t.Key == "reason" && t.Value == "error");
    }

    [TestMethod]
    public void TrackEvent_LogRecordMode_ShouldEmitStructuredLogWithEventName()
    {
        // Arrange
        var fakeLogger = new FakeLogger<CustomEventTelemetryClient>();
        var sut = new CustomEventTelemetryClient(
            emitMode: CustomEventEmitMode.LogRecord,
            logger: fakeLogger);

        // Act
        sut.TrackEvent("OrderCreated", new Dictionary<string, object> { ["orderId"] = "123" });

        // Assert
        var logEntry = fakeLogger.LatestRecord;
        logEntry.Level.Should().Be(LogLevel.Critical);
        logEntry.Message.Should().Contain("OrderCreated");
    }

    [TestMethod]
    public void TrackEvent_LogRecordMode_ShouldNotEmitActivitySpan()
    {
        // Arrange
        var exportedActivities = new List<Activity>();
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("Uniphar.Platform.Telemetry.CustomEvents")
            .AddInMemoryExporter(exportedActivities)
            .Build();

        var fakeLogger = new FakeLogger<CustomEventTelemetryClient>();
        var sut = new CustomEventTelemetryClient(
            emitMode: CustomEventEmitMode.LogRecord,
            logger: fakeLogger);

        // Act
        sut.TrackEvent("OnlyLog", new Dictionary<string, object> { ["key"] = "val" });
        tracerProvider.ForceFlush();

        // Assert
        exportedActivities.Should().BeEmpty();
        fakeLogger.LatestRecord.Message.Should().Contain("OnlyLog");
    }

    [TestMethod]
    public void TrackEvent_BothMode_ShouldEmitActivitySpanAndLogRecord()
    {
        // Arrange
        var exportedActivities = new List<Activity>();
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("Uniphar.Platform.Telemetry.CustomEvents")
            .AddInMemoryExporter(exportedActivities)
            .Build();

        var fakeLogger = new FakeLogger<CustomEventTelemetryClient>();
        var sut = new CustomEventTelemetryClient(
            emitMode: CustomEventEmitMode.Both,
            logger: fakeLogger);

        // Act
        sut.TrackEvent("DualEvent", new Dictionary<string, object> { ["source"] = "test" });
        tracerProvider.ForceFlush();

        // Assert - activity span emitted
        var span = exportedActivities.Single(a => a.DisplayName == "DualEvent");
        span.Tags.Should().Contain(t => t.Key == "event.name" && t.Value == "DualEvent");
        span.Tags.Should().Contain(t => t.Key == "source" && t.Value == "test");

        // Assert - log record emitted
        var logEntry = fakeLogger.LatestRecord;
        logEntry.Level.Should().Be(LogLevel.Critical);
        logEntry.Message.Should().Contain("DualEvent");
    }
}
