using AwesomeAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Uniphar.Platform.Telemetry;

[TestClass]
[TestCategory("Unit")]
public class LogCategoryFilterTests
{
    private static IHostApplicationBuilder CreateTestBuilder()
    {
        var builder = Host.CreateApplicationBuilder();

        // Add required configuration for Application Insights
        var config = new Dictionary<string, string?>
        {
            ["APPLICATIONINSIGHTS:CONNECTIONSTRING"] = "InstrumentationKey=00000000-0000-0000-0000-000000000000;IngestionEndpoint=https://test.applicationinsights.azure.com/"
        };
        builder.Configuration.AddInMemoryCollection(config);

        return builder;
    }

    [TestMethod]
    public void LogCategoryFilter_AzureSdkDefaults_ContainsExpectedCategories()
    {
        var defaults = LogCategoryFilter.AzureSdkDefaults.ToList();

        defaults.Count.Should().Be(3);
        defaults.Any(f => f.CategoryName == "Azure" && f.MinimumLevel == LogLevel.Warning).Should().BeTrue();
        defaults.Any(f => f.CategoryName == "Azure.Core" && f.MinimumLevel == LogLevel.Warning).Should().BeTrue();
        defaults.Any(f => f.CategoryName == "Azure.Identity" && f.MinimumLevel == LogLevel.Warning).Should().BeTrue();
    }

    [TestMethod]
    public void LogCategoryFilter_Record_HasCorrectProperties()
    {
        var filter = new LogCategoryFilter("TestCategory", LogLevel.Debug);

        filter.CategoryName.Should().Be("TestCategory");
        filter.MinimumLevel.Should().Be(LogLevel.Debug);
    }

    [TestMethod]
    public void LogCategoryFilter_RecordEquality_WorksCorrectly()
    {
        var filter1 = new LogCategoryFilter("Azure", LogLevel.Warning);
        var filter2 = new LogCategoryFilter("Azure", LogLevel.Warning);
        var filter3 = new LogCategoryFilter("Azure", LogLevel.Error);
        var filter4 = new LogCategoryFilter("Azure.Core", LogLevel.Warning);

        filter1.Should().Be(filter2);
        filter1.Should().NotBe(filter3);
        filter1.Should().NotBe(filter4);
    }

    [TestMethod]
    public void TelemetryBuilder_DefaultConfiguration_HasAzureSdkFilters()
    {
        var builder = CreateTestBuilder();

        var telemetryBuilder = builder.RegisterOpenTelemetry("test-app");

        // Verify default filters are applied using reflection to access internal property
        var logFilters = telemetryBuilder.GetType()
            .GetProperty("LogCategoryFilters", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(telemetryBuilder) as IEnumerable<LogCategoryFilter>;

        logFilters.Should().NotBeNull();
        var filterList = logFilters!.ToList();
        filterList.Count.Should().Be(3);
        filterList.Any(f => f.CategoryName == "Azure").Should().BeTrue();
    }

    [TestMethod]
    public void WithLogCategoryFilters_OverridesDefaultFilters()
    {
        var builder = CreateTestBuilder();

        var customFilters = new[]
        {
            new LogCategoryFilter("Azure", LogLevel.Error),
            new LogCategoryFilter("Microsoft.EntityFrameworkCore", LogLevel.Warning)
        };

        var telemetryBuilder = builder.RegisterOpenTelemetry("test-app")
            .WithLogCategoryFilters(customFilters);

        // Verify custom filters are applied
        var logFilters = telemetryBuilder.GetType()
            .GetProperty("LogCategoryFilters", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(telemetryBuilder) as IEnumerable<LogCategoryFilter>;

        logFilters.Should().NotBeNull();
        var filterList = logFilters!.ToList();
        filterList.Count.Should().Be(2);
        filterList.Any(f => f.CategoryName == "Azure" && f.MinimumLevel == LogLevel.Error).Should().BeTrue();
        filterList.Any(f => f.CategoryName == "Microsoft.EntityFrameworkCore" && f.MinimumLevel == LogLevel.Warning).Should().BeTrue();
    }

    [TestMethod]
    public void WithLogCategoryFilters_EmptyCollection_ClearsFilters()
    {
        var builder = CreateTestBuilder();

        var telemetryBuilder = builder.RegisterOpenTelemetry("test-app")
            .WithLogCategoryFilters([]);

        // Verify filters are empty
        var logFilters = telemetryBuilder.GetType()
            .GetProperty("LogCategoryFilters", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(telemetryBuilder) as IEnumerable<LogCategoryFilter>;

        logFilters.Should().NotBeNull();
        logFilters!.Should().BeEmpty();
    }

    [TestMethod]
    public void WithLogCategoryFilters_ReturnsBuilderForChaining()
    {
        var builder = CreateTestBuilder();

        var logFilters = new[]
        {
            new LogCategoryFilter("Azure", LogLevel.Error)
        };

        var telemetryBuilder = builder.RegisterOpenTelemetry("test-app")
            .WithLogCategoryFilters(logFilters);

        telemetryBuilder.Should().NotBeNull();
        telemetryBuilder.Should().BeOfType<TelemetryBuilder>();
    }

    [TestMethod]
    public void WithLogCategoryFilters_CanBeChainedWithOtherMethods()
    {
        var builder = CreateTestBuilder();

        var logFilters = new[]
        {
            new LogCategoryFilter("Azure", LogLevel.Error)
        };

        var exceptionRules = new[]
        {
            new ExceptionHandlingRule(
                logRecord => logRecord.Exception is IOException,
                (logRecord, client) => client.TrackEvent("IoError", new() { ["Message"] = logRecord.Exception?.Message ?? "" })
            )
        };

        // Verify fluent API works with multiple configurations
        var telemetryBuilder = builder.RegisterOpenTelemetry("test-app")
            .WithLogCategoryFilters(logFilters)
            .WithExceptionsFilters(exceptionRules)
            .WithFilterExclusion(new[] { "/health" });

        telemetryBuilder.Should().NotBeNull();

        // Can call Build without errors
        telemetryBuilder.Build();
    }

    [TestMethod]
    public void MultipleLogFilters_CanBeApplied()
    {
        var builder = CreateTestBuilder();

        var customFilters = new[]
        {
            new LogCategoryFilter("Azure", LogLevel.Error),
            new LogCategoryFilter("Azure.Identity", LogLevel.Critical),
            new LogCategoryFilter("System.Net.Http", LogLevel.Warning),
            new LogCategoryFilter("Microsoft", LogLevel.Information)
        };

        var telemetryBuilder = builder.RegisterOpenTelemetry("test-app")
            .WithLogCategoryFilters(customFilters);

        var logFilters = telemetryBuilder.GetType()
            .GetProperty("LogCategoryFilters", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)
            ?.GetValue(telemetryBuilder) as IEnumerable<LogCategoryFilter>;

        logFilters.Should().NotBeNull();
        var filterList = logFilters!.ToList();
        filterList.Count.Should().Be(4);
    }

    [TestMethod]
    public void LogCategoryFilter_WithDifferentLogLevels_CreatesCorrectFilters()
    {
        var filters = new[]
        {
            new LogCategoryFilter("Trace", LogLevel.Trace),
            new LogCategoryFilter("Debug", LogLevel.Debug),
            new LogCategoryFilter("Information", LogLevel.Information),
            new LogCategoryFilter("Warning", LogLevel.Warning),
            new LogCategoryFilter("Error", LogLevel.Error),
            new LogCategoryFilter("Critical", LogLevel.Critical),
            new LogCategoryFilter("None", LogLevel.None)
        };

        filters.Length.Should().Be(7);
        filters[0].MinimumLevel.Should().Be(LogLevel.Trace);
        filters[1].MinimumLevel.Should().Be(LogLevel.Debug);
        filters[2].MinimumLevel.Should().Be(LogLevel.Information);
        filters[3].MinimumLevel.Should().Be(LogLevel.Warning);
        filters[4].MinimumLevel.Should().Be(LogLevel.Error);
        filters[5].MinimumLevel.Should().Be(LogLevel.Critical);
        filters[6].MinimumLevel.Should().Be(LogLevel.None);
    }
}
