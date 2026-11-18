using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Uniphar.Platform.Telemetry.Tests;

[TestClass]
public class NonAlertingExceptionProcessorTests
{
    private static readonly Dictionary<string, object?> RestartDimensions = new() { ["Category"] = "Restart" };

    private List<LogRecord> _exportedLogs = null!;
    private ILogger<NonAlertingExceptionProcessorTests> _logger = null!;

    [TestInitialize]
    public void Setup()
    {
        _exportedLogs = [];
        var processor = CreateProcessorFromRegisteredRules(
        [
            ExceptionHandlingRule.For<IOException>(
                e => e.Message.Contains("being used by another process"),
                RestartDimensions,
                LogLevel.Warning)
        ]);

        var loggerFactory = LoggerFactory.Create(builder => builder.AddOpenTelemetry(options =>
        {
            options.AddProcessor(processor);
            // the in-memory exporter copies pooled LogRecords, so they stay valid after export
            options.AddInMemoryExporter(_exportedLogs);
        }));
        _logger = loggerFactory.CreateLogger<NonAlertingExceptionProcessorTests>();
    }

    [TestMethod]
    public void MatchingException_KeepsFullLog_AndAddsMarkerDimensionsAndLevel()
    {
        var exception = new IOException("The process cannot access the file because it is being used by another process.");

        _logger.LogError(exception, "Error while moving file");

        var log = _exportedLogs.Should().ContainSingle().Subject;
        log.Exception.Should().BeSameAs(exception);
        log.LogLevel.Should().Be(LogLevel.Warning);
        log.Attributes.Should().Contain(new KeyValuePair<string, object?>(NonAlertingLogs.MarkerAttribute, true));
        log.Attributes.Should().Contain(new KeyValuePair<string, object?>("Category", "Restart"));
    }

    [TestMethod]
    public void NonMatchingException_IsLeftUntouched()
    {
        var exception = new Exception("Some other error");

        _logger.LogError(exception, "Error while moving file");

        var log = _exportedLogs.Should().ContainSingle().Subject;
        log.Exception.Should().BeSameAs(exception);
        log.LogLevel.Should().Be(LogLevel.Error);
        log.Attributes?.Select(a => a.Key).Should().NotContain(NonAlertingLogs.MarkerAttribute);
    }

    [TestMethod]
    public void LogNonAlertingError_LogsErrorWithMarkerAndDimensions()
    {
        var exception = new InvalidOperationException("boom");

        _logger.LogNonAlertingError(exception, "Restarting worker", RestartDimensions);

        var log = _exportedLogs.Should().ContainSingle().Subject;
        log.LogLevel.Should().Be(LogLevel.Error);
        log.Exception.Should().BeSameAs(exception);
        log.Body.Should().Be("Restarting worker");
        log.Attributes.Should().Contain(new KeyValuePair<string, object?>(NonAlertingLogs.MarkerAttribute, true));
        log.Attributes.Should().Contain(new KeyValuePair<string, object?>("Category", "Restart"));
    }

    [TestMethod]
    public void LogNonAlertingWarning_LogsWarningWithMarker()
    {
        _logger.LogNonAlertingWarning(null, "Slow restart");

        var log = _exportedLogs.Should().ContainSingle().Subject;
        log.LogLevel.Should().Be(LogLevel.Warning);
        log.Attributes.Should().Contain(new KeyValuePair<string, object?>(NonAlertingLogs.MarkerAttribute, true));
    }

    [TestMethod]
    public void Rule_RejectsUnsupportedLogLevel()
    {
        var act = () => ExceptionHandlingRule.For<Exception>(setToLogLevel: LogLevel.Information);

        act.Should().Throw<ArgumentOutOfRangeException>();
    }

    private static NonAlertingExceptionProcessor CreateProcessorFromRegisteredRules(IEnumerable<ExceptionHandlingRule> rules)
    {
        var appBuilder = Host.CreateApplicationBuilder();
        appBuilder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"] = "InstrumentationKey=00000000-0000-0000-0000-000000000000";
        appBuilder
            .RegisterOpenTelemetry("test-app")
            .WithExceptionsFilters(rules)
            .Build();

        using var host = appBuilder.Build();
        return new(host.Services.GetRequiredService<IEnumerable<ExceptionHandlingRule>>());
    }
}
