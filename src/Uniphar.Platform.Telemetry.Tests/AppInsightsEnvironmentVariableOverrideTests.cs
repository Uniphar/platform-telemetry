using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Uniphar.Platform.Telemetry;

[TestClass]
[TestCategory("Unit")]
public class AppInsightsConnectionStringOverrideTests
{
    private const string FakeConnectionString = "InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://test.in.applicationinsights.azure.com/";

    [TestMethod]
    public void Build_WithConnectionString_UsesProvidedValue()
    {
        // Arrange – no config/env var set, connection string provided directly
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });

        // Act – should NOT throw because the connection string is supplied explicitly
        var act = () => builder
            .RegisterOpenTelemetry("test-app")
            .WithAppInsightsConnectionString(FakeConnectionString)
            .Build();

        act.Should().NotThrow();
    }

    [TestMethod]
    public void Build_WithoutConnectionString_FallsBackToDefaultConfigKey()
    {
        // Arrange – connection string under the default APPLICATIONINSIGHTS_CONNECTION_STRING key
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["APPLICATIONINSIGHTS_CONNECTION_STRING"] = FakeConnectionString
        });

        // Act – should succeed using the default config key fallback
        var act = () => builder
            .RegisterOpenTelemetry("test-app")
            .Build();

        act.Should().NotThrow();
    }

    [TestMethod]
    public void Build_WithoutAnyExporterConfigured_ThrowsInvalidOperationException()
    {
        // Arrange – no connection string anywhere, no OTLP endpoint
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });

        // Act
        var act = () => builder
            .RegisterOpenTelemetry("test-app")
            .Build();

        // Assert – should throw because no exporter is configured
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*APPLICATIONINSIGHTS_CONNECTION_STRING*");
    }

    [TestMethod]
    public void WithAppInsightsConnectionString_WithNullOrWhitespace_ThrowsArgumentException()
    {
        // Arrange
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });

        // Act
        var act = () => builder
            .RegisterOpenTelemetry("test-app")
            .WithAppInsightsConnectionString("   ");

        // Assert
        act.Should().Throw<ArgumentException>();
    }
}
