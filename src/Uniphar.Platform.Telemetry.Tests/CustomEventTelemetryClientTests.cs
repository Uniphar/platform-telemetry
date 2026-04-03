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

    [TestMethod]
    public void TrackEvent_ShouldTagCurrentActivity_WithCustomDimensions()
    {
        // Arrange
        using var activitySource = new ActivitySource("test-source");
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);

        using var activity = activitySource.StartActivity("test-operation");
        Assert.IsNotNull(activity, "An active Activity must exist for this test");

        var loggerFactory = LoggerFactory.Create(builder => builder.AddOpenTelemetry());
        var logger = loggerFactory.CreateLogger<CustomEventTelemetryClient>();
        var sut = new CustomEventTelemetryClient(logger);
        var state = new Dictionary<string, object> { ["reason"] = "error" };

        // Act
        sut.TrackEvent("MyCustomEvent", state);

        // Assert
        activity.Tags.Should().Contain(t => t.Key == "reason" && t.Value == "error");
    }
}
