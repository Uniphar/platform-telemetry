using AwesomeAssertions;

namespace Uniphar.Platform.Telemetry.Tests;

[TestClass]
[TestCategory("Unit")]
public class CustomEventTelemetryClientTests
{
    [TestMethod]
    public void TrackEvent_ShouldAppendCustomDimensions()
    {
        // Arrange
        var logRecordExporter = new InMemoryLogRecordExporter();
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddOpenTelemetry(options =>
            {
                options.AddProcessor(new AmbientPropertiesLogRecordInjector());
                options.AddProcessor(new SimpleLogRecordExportProcessor(logRecordExporter));
            });
        });
        var logger = loggerFactory.CreateLogger<CustomEventTelemetryClient>();
        var sut = new CustomEventTelemetryClient(logger);
        var state = new Dictionary<string, object>
        {
            ["reason"] = "error",
        };

        // Act
        sut.TrackEvent("MyCustomEvent", state);

        // Assert
        var exported = logRecordExporter.ExportedLogs.Single();
        exported.LogLevel.Should().Be(LogLevel.Critical);
        exported.Attributes.Should().Contain(x => x.Key == "microsoft.custom_event.name" && x.Value != null && x.Value.ToString() == "MyCustomEvent");
        exported.Attributes.Should().Contain(x => x.Key == "reason" && x.Value != null && x.Value.ToString() == "error");
    }
}
