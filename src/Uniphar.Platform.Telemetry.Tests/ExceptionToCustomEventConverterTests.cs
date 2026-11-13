using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace Uniphar.Platform.Telemetry.Tests;

[TestClass]
[TestCategory("Unit")]
public class ExceptionToCustomEventConverterTests
{
    private Mock<ICustomEventTelemetryClient> _mockTelemetryClient = null!;
    private List<LogRecord> _exportedLogs = null!;
    private ILogger<ExceptionToCustomEventConverterTests> _logger = null!;

    [TestInitialize]
    public void Setup()
    {
        _mockTelemetryClient = new Mock<ICustomEventTelemetryClient>();
        _exportedLogs = [];
        var processor = CreateProcessorFromRegisteredRules(_mockTelemetryClient.Object);

        var loggerFactory = LoggerFactory.Create(builder =>
        {
            builder.AddOpenTelemetry(options =>
            {
                options.AddProcessor(processor);
                // the in-memory exporter copies pooled LogRecords, so they stay valid after export
                options.AddInMemoryExporter(_exportedLogs);
            });
        });
        _logger = loggerFactory.CreateLogger<ExceptionToCustomEventConverterTests>();
    }

    [TestMethod]
    public void OnEnd_ShouldSendCustomEvent_ForFileLockedIOException()
    {
        // Arrange
        var exception = new IOException("The process cannot access the file 'C:\\Temp\\test.txt' because it is being used by another process.");

        Dictionary<string, object>? capturedState = null;
        _mockTelemetryClient
            .Setup(x => x.TrackEvent(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()))
            .Callback<string, Dictionary<string, object>>((_, state) => capturedState = state);

        // Act
        _logger.LogError(exception, "Error while moving file");

        // Assert
        _mockTelemetryClient.Verify(x => x.TrackEvent("IoLock", It.IsAny<Dictionary<string, object>>()), Times.Once);

        Assert.IsNotNull(capturedState);
        Assert.IsTrue(capturedState["Exception"].ToString()!.Contains(exception.Message));
        // make sure it is only one
        Assert.AreEqual(1, _exportedLogs.Count);
        var exportedLog = _exportedLogs.Single();
        // no log level defined as the processor will convert the log to custom event
        Assert.AreEqual(LogLevel.None, exportedLog.LogLevel);
        Assert.IsNull(exportedLog.Exception);
        Assert.AreEqual("[converted to custom event]", exportedLog.FormattedMessage);
        Assert.IsNull(exportedLog.Body);
    }

    [TestMethod]
    public void OnEnd_ShouldNotSendCustomEvent_ForGenericException()
    {
        // Arrange
        var exception = new Exception("Some other error");

        // Act
        _logger.LogError(exception, "Error while moving file");

        // Assert
        _mockTelemetryClient.Verify(x => x.TrackEvent(It.IsAny<string>(), It.IsAny<Dictionary<string, object>>()), Times.Never);
        Assert.IsTrue(_exportedLogs.Any(log => log.Exception == exception));
    }

    [TestMethod]
    public void OnEnd_ShouldEmitCustomEventWithExceptionProperty_WhenUsingRealClient()
    {
        // Arrange – real CustomEventTelemetryClient so bugs in TrackEvent break this test
        var exportedActivities = new List<Activity>();
        using var tracerProvider = Sdk.CreateTracerProviderBuilder()
            .AddSource("Uniphar.Platform.Telemetry.CustomEvents")
            .AddInMemoryExporter(exportedActivities)
            .Build();

        var realClient = new CustomEventTelemetryClient();

        var processor = CreateProcessorFromRegisteredRules(realClient);
        var appLogger = LoggerFactory.Create(builder =>
        {
            builder.AddOpenTelemetry(options => options.AddProcessor(processor));
        }).CreateLogger<ExceptionToCustomEventConverterTests>();

        var exception = new IOException("The process cannot access the file 'C:\\Temp\\test.txt' because it is being used by another process.");

        // Act
        appLogger.LogError(exception, "Error while moving file");

        // Force flush to ensure spans are exported
        tracerProvider.ForceFlush();

        // Assert – custom event must be an Activity span with the exception message as a tag
        var span = exportedActivities.Single(a => a.Source.Name == "Uniphar.Platform.Telemetry.CustomEvents" && a.DisplayName == "IoLock");
        Assert.AreEqual("IoLock", span.DisplayName);
        Assert.IsTrue(
            span.Tags.Any(t => t.Key == "Exception" && t.Value!.Contains(exception.Message)),
            $"Expected 'Exception' tag containing the exception message. Actual tags: {string.Join(", ", span.Tags.Select(t => $"{t.Key}={t.Value}"))}");
    }

    private static ExceptionToCustomEventConverter CreateProcessorFromRegisteredRules(ICustomEventTelemetryClient client)
    {
        var appBuilder = Host.CreateApplicationBuilder();
        appBuilder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"] = "InstrumentationKey=00000000-0000-0000-0000-000000000000";
        appBuilder
            .RegisterOpenTelemetry("test-app")
            .WithExceptionsFilters(TelemetryExceptionHandlingRulesFilter.Rules)
            .WithDependencyFilter(DependencyFilterConfiguration.Default)
            .Build();

        using var host = appBuilder.Build();
        var registeredRules = host.Services.GetRequiredService<IEnumerable<ExceptionHandlingRule>>();

        return new ExceptionToCustomEventConverter(registeredRules, client);
    }
}

internal static class TelemetryExceptionHandlingRulesFilter
{
    internal static IEnumerable<ExceptionHandlingRule> Rules =>
    [
        new(
            logRecord => logRecord.Exception is IOException &&
                         logRecord.Exception.Message.Contains("being used by another process"),
            (logRecord, client) => client.TrackEvent("IoLock", new() { ["Exception"] = logRecord.Exception?.Message ?? string.Empty })
        )
    ];
}
