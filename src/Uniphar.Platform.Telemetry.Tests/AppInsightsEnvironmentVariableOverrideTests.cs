using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Uniphar.Platform.Telemetry;

[TestClass]
[TestCategory("Unit")]
public class AppInsightsEnvironmentVariableOverrideTests
{
    private const string CustomKey = "MY_CUSTOM_APPINSIGHTS_CS";
    private const string FakeConnectionString = "InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://test.in.applicationinsights.azure.com/";

    [TestMethod]
    public void Build_WithOverriddenKey_ReadsConnectionStringFromCustomConfigKey()
    {
        // Arrange – connection string is ONLY under the custom key, not the default
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [CustomKey] = FakeConnectionString
        });

        // Act – should NOT throw because the override tells Build() to look at CustomKey
        var act = () => builder
            .RegisterOpenTelemetry("test-app")
            .WithAppInsightsEnvironmentVariable(CustomKey)
            .Build();

        act.Should().NotThrow();
    }

    [TestMethod]
    public void Build_WithoutOverride_DoesNotReadFromCustomKey()
    {
        // Arrange – connection string exists ONLY under a non-default key
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [CustomKey] = FakeConnectionString
        });

        // Act – should throw because the default key has no value and OTLP is also not set
        var act = () => builder
            .RegisterOpenTelemetry("test-app")
            .Build();

        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{TelemetryBuilder.DefaultAppInsightsEnvironmentVariable}*");
    }

    [TestMethod]
    public void Build_WithDefaultKey_ReadsConnectionStringFromDefaultConfigKey()
    {
        // Arrange – connection string under the default key
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });
        builder.Configuration.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [TelemetryBuilder.DefaultAppInsightsEnvironmentVariable] = FakeConnectionString
        });

        // Act – should succeed without calling WithAppInsightsEnvironmentVariable
        var act = () => builder
            .RegisterOpenTelemetry("test-app")
            .Build();

        act.Should().NotThrow();
    }

    [TestMethod]
    public void Build_WithOverriddenKey_ErrorMessageReflectsCustomKey()
    {
        // Arrange – no connection string anywhere
        var builder = Host.CreateApplicationBuilder(new HostApplicationBuilderSettings { DisableDefaults = true });

        // Act
        var act = () => builder
            .RegisterOpenTelemetry("test-app")
            .WithAppInsightsEnvironmentVariable(CustomKey)
            .Build();

        // Assert – the error message should mention the custom key, not the default
        act.Should().Throw<InvalidOperationException>()
            .WithMessage($"*{CustomKey}*");
    }
}
