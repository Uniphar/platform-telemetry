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
        using var activitySource = new ActivitySource("test-source");
        using var listener = new ActivityListener
        {
            ShouldListenTo = _ => true,
            Sample = (ref ActivityCreationOptions<ActivityContext> _) => ActivitySamplingResult.AllData,
        };
        ActivitySource.AddActivityListener(listener);
        using var activity = activitySource.StartActivity("test-operation");
        Assert.IsNotNull(activity);

        var logRecordExporter = new InMemoryLogRecordExporter();
        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddOpenTelemetry(options =>
            {
                options.AddProcessor(new AmbientPropertiesLogRecordInjector());
                options.AddProcessor(new SimpleLogRecordExportProcessor(logRecordExporter));
            });
        });
        var sut = new CustomEventTelemetryClient(loggerFactory.CreateLogger<CustomEventTelemetryClient>());

        // Act
        sut.TrackEvent("MyCustomEvent", new Dictionary<string, object> { ["reason"] = "error" });

        // Assert
        // the dimension must appear in LogRecord attributes (via Initialize) OR Activity tags (via fallback).
        var exported = logRecordExporter.ExportedLogs.Single();
        var inLogRecord = exported.Attributes?.Any(x => x.Key == "reason" && x.Value?.ToString() == "error") == true;
        var inActivity = activity.Tags.Any(t => t.Key == "reason" && t.Value == "error");

        Assert.IsTrue(inLogRecord || inActivity, $"'reason' must appear in either LogRecord attributes or Activity tags.");
    }
}
