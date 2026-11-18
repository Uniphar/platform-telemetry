using AwesomeAssertions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Uniphar.Platform.Telemetry.Testing;

namespace Uniphar.Platform.Telemetry.IntegrationTests;

/// <summary>
///     Sends real telemetry through the library pipeline to the Dash0 dev dataset and asserts it arrives
///     with the expected attributes, using the <c>Uniphar.Platform.Telemetry.Testing</c> query client.
/// </summary>
[TestClass, TestCategory("Integration")]
public class Dash0IntegrationTests
{
    private const string ServiceName = "telemetry-app-integration";
    private static readonly TimeSpan IngestionTimeout = TimeSpan.FromMinutes(2);

    private static Dash0TelemetryQueryClient _client = null!;

    [ClassInitialize]
    public static void ClassInitialize(TestContext _)
    {
        _client = Dash0TelemetryQueryClient.Create();
        ConfigureOtlpExporter();
    }

    [TestMethod]
    public async Task NonAlertingError_ShouldArriveAsErrorLog_WithMarker_AndDimensions()
    {
        var runId = NewRunId();
        var since = DateTimeOffset.UtcNow.AddMinutes(-1);

        await RunAppAsync(
            [ExceptionHandlingRule.For<TimeoutException>(dimensions: new Dictionary<string, object?> { ["Category"] = "Restart" })],
            host =>
            {
                var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("integration");
                logger.LogError(new TimeoutException("restarting"), "Filtered by rule {RunId}", runId);
            });

        var count = await WaitForLogsAsync(since,
            Dash0Filter.Is("RunId", runId),
            Dash0Filter.Is("telemetry.non_alerting", "true"),
            Dash0Filter.Is("Category", "Restart"),
            Dash0Filter.Is("otel.log.severity.range", "ERROR"));

        count.Should().Be(1);
    }

    [TestMethod]
    public async Task LogNonAlertingWarning_ShouldArriveAsWarningLog_WithMarker()
    {
        var runId = NewRunId();
        var since = DateTimeOffset.UtcNow.AddMinutes(-1);

        await RunAppAsync(
            [],
            host =>
            {
                var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("integration");
                logger.LogNonAlertingWarning(new InvalidOperationException("expected"), "Expected failure {RunId}", runId);
            });

        var count = await WaitForLogsAsync(since,
            Dash0Filter.Is("RunId", runId),
            Dash0Filter.Is("telemetry.non_alerting", "true"),
            Dash0Filter.Is("otel.log.severity.range", "WARN"));

        count.Should().Be(1);
    }

    [TestMethod]
    public async Task UnfilteredError_ShouldArriveWithoutMarker()
    {
        var runId = NewRunId();
        var since = DateTimeOffset.UtcNow.AddMinutes(-1);

        await RunAppAsync(
            [],
            host =>
            {
                var logger = host.Services.GetRequiredService<ILoggerFactory>().CreateLogger("integration");
                logger.LogError(new InvalidOperationException("boom"), "Real failure {RunId}", runId);
            });

        var total = await WaitForLogsAsync(since, Dash0Filter.Is("RunId", runId));
        var marked = await _client.CountLogsAsync(
            new Dash0Query(since, ServiceName, [Dash0Filter.Is("RunId", runId), Dash0Filter.Is("telemetry.non_alerting", "true")]));

        total.Should().Be(1);
        marked.Should().Be(0);
    }

    [TestMethod]
    public async Task CustomEvent_ShouldArriveAsSpan_WithProperties_AndSkipNulls()
    {
        var runId = NewRunId();
        var since = DateTimeOffset.UtcNow.AddMinutes(-1);

        await RunAppAsync(
            [],
            host => host.Services.GetRequiredService<ICustomEventTelemetryClient>()
                .TrackEvent("IntegrationEvent", new Dictionary<string, object?> { ["RunId"] = runId, ["Skipped"] = null }));

        var count = await _client.WaitForCountAsync(
            ct => _client.CountSpansAsync(
                new Dash0Query(since, ServiceName, [Dash0Filter.Is("otel.span.name", "IntegrationEvent"), Dash0Filter.Is("RunId", runId)]), ct),
            1,
            IngestionTimeout);

        count.Should().Be(1);
    }

    private static string NewRunId() => Guid.NewGuid().ToString("N");

    private static Task<int> WaitForLogsAsync(DateTimeOffset since, params Dash0Filter[] filters) =>
        _client.WaitForCountAsync(
            ct => _client.CountLogsAsync(new Dash0Query(since, ServiceName, filters), ct),
            1,
            IngestionTimeout);

    // The library reads the standard OTEL_* variables; Dash0 authenticates ingestion with the same token as its API.
    private static void ConfigureOtlpExporter()
    {
        var token = Dash0TelemetryQueryClient.GetRequiredEnvironmentVariable(Dash0TelemetryQueryClient.AuthTokenEnvironmentVariable);
        var dataset = Dash0TelemetryQueryClient.GetRequiredEnvironmentVariable(Dash0TelemetryQueryClient.DatasetEnvironmentVariable);
        var bearer = token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? token : $"Bearer {token}";

        Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT", Dash0TelemetryQueryClient.GetRequiredEnvironmentVariable("DASH0_OTLP_ENDPOINT"));
        Environment.SetEnvironmentVariable("OTEL_EXPORTER_OTLP_HEADERS", $"Authorization={bearer},Dash0-Dataset={dataset}");
    }

    // Stopping the host flushes the exporters, so everything emitted by the action is sent before the test queries Dash0.
    private static async Task RunAppAsync(IEnumerable<ExceptionHandlingRule> rules, Action<IHost> emit)
    {
        var appBuilder = Host.CreateApplicationBuilder();
        appBuilder.RegisterOpenTelemetry(ServiceName)
            .WithExceptionsFilters(rules)
            .Build();
        using var host = appBuilder.Build();

        await host.StartAsync();
        emit(host);
        await host.StopAsync();
    }
}
