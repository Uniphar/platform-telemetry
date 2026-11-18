using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Uniphar.Platform.Telemetry.Tests;

[TestClass]
public class TelemetryBuilderGuardTests
{
    private const string ConnectionString = "InstrumentationKey=00000000-0000-0000-0000-000000000000";

    [TestMethod]
    public void Build_ThrowsWhenCalledTwice()
    {
        var appBuilder = Host.CreateApplicationBuilder();
        appBuilder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"] = ConnectionString;
        var telemetry = appBuilder.RegisterOpenTelemetry("test-app");
        telemetry.Build();

        var act = () => telemetry.Build();

        act.Should().Throw<InvalidOperationException>();
    }

    [TestMethod]
    public async Task HostStart_ThrowsWhenBuildWasNeverCalled()
    {
        var appBuilder = Host.CreateApplicationBuilder();
        appBuilder.RegisterOpenTelemetry("test-app");
        using var host = appBuilder.Build();

        var act = () => host.StartAsync();

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*Build()*");
    }
}
