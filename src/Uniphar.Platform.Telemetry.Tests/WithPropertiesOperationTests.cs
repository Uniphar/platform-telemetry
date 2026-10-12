using AwesomeAssertions;

namespace Uniphar.Platform.Telemetry.Tests;

[TestClass]
[TestCategory("Unit")]
public class WithPropertiesOperationTests
{
    private const string Source = "Uniphar.Platform.Telemetry.Operations";

    [TestMethod]
    public void WithProperties_WithoutCurrentActivity_ShouldStartOperationSharedByEventsAndEndItOnDispose()
    {
        var exported = new List<Activity>();
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(Source)
            .AddSource("Uniphar.Platform.Telemetry.CustomEvents")
            .AddInMemoryExporter(exported)
            .Build();
        var client = new CustomEventTelemetryClient(emitMode: CustomEventEmitMode.ActivitySpan);

        using (client.WithProperties([new("Any", "value")]))
        {
            Activity.Current.Should().NotBeNull();
            client.TrackEvent("WithProps.Step");
        }
        tracerProvider.ForceFlush();

        Activity.Current.Should().BeNull();
        var root = exported.Single(a => a.DisplayName == $"{nameof(WithPropertiesOperationTests)}.{nameof(WithProperties_WithoutCurrentActivity_ShouldStartOperationSharedByEventsAndEndItOnDispose)}");
        exported.Single(a => a.DisplayName == "WithProps.Step").TraceId.Should().Be(root.TraceId);
    }

    [TestMethod]
    public void WithProperties_WithCurrentActivity_ShouldNotStartAnotherOperation()
    {
        var exported = new List<Activity>();
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource(Source)
            .AddSource("WithProps.Outer")
            .AddInMemoryExporter(exported)
            .Build();
        var client = new CustomEventTelemetryClient(emitMode: CustomEventEmitMode.ActivitySpan);
        using var outerSource = new ActivitySource("WithProps.Outer");

        using (var outer = outerSource.StartActivity("WithProps.OuterSpan"))
        using (client.WithProperties([new("Any", "value")], "WithProps.Ignored"))
        {
            Activity.Current.Should().BeSameAs(outer);
        }
        tracerProvider.ForceFlush();

        exported.Should().NotContain(a => a.DisplayName == "WithProps.Ignored");
    }
}
