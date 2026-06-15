using AwesomeAssertions;

namespace Uniphar.Platform.Telemetry.Tests;

[TestClass]
[TestCategory("Unit")]
public class CustomEventTelemetryClientTests
{
    [TestMethod]
    public void TrackEvent_ShouldEmitActivitySpanWithCustomDimensions()
    {
        // Arrange
        var exportedActivities = new List<Activity>();
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("Uniphar.Platform.Telemetry.CustomEvents")
            .AddInMemoryExporter(exportedActivities)
            .Build();

        var sut = new CustomEventTelemetryClient();

        // Act
        sut.TrackEvent("MyCustomEvent", new Dictionary<string, object> { ["reason"] = "error" });

        // Force flush to ensure spans are exported
        tracerProvider.ForceFlush();

        // Assert
        var span = exportedActivities.Single();
        span.DisplayName.Should().Be("MyCustomEvent");
        span.Tags.Should().Contain(t => t.Key == "event.name" && t.Value == "MyCustomEvent");
        span.Tags.Should().Contain(t => t.Key == "reason" && t.Value == "error");
    }
}
