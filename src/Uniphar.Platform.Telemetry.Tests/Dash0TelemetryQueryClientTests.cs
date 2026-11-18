using AwesomeAssertions;
using System.Net;
using System.Text;
using System.Text.Json.Nodes;
using Uniphar.Platform.Telemetry.Testing;

namespace Uniphar.Platform.Telemetry.Tests;

[TestClass, TestCategory("Unit")]
public class Dash0TelemetryQueryClientTests
{
    private const string SpansResponse = """
        {"resourceSpans":[
          {"scopeSpans":[{"spans":[{"name":"Job"},{"name":"Job"}]},{"spans":[{"name":"Job"}]}]},
          {"scopeSpans":[{"spans":[{"name":"Job"}]}]}
        ]}
        """;

    private static (Dash0TelemetryQueryClient Client, StubHandler Handler) CreateClient(string responseBody = SpansResponse, HttpStatusCode status = HttpStatusCode.OK)
    {
        var handler = new StubHandler(responseBody, status);
        var httpClient = new HttpClient(handler) { BaseAddress = new Uri("https://api.test.dash0.com/") };
        return (new Dash0TelemetryQueryClient(httpClient, "test-dataset"), handler);
    }

    private static List<(string Key, string Operator, string Value)> Filters(StubHandler handler) =>
        JsonNode.Parse(handler.LastBody!)!["filter"]!.AsArray()
            .Select(f => (f!["key"]!.GetValue<string>(), f["operator"]!.GetValue<string>(), f["value"]!.GetValue<string>()))
            .ToList();

    [TestMethod]
    public async Task CountSpansAsync_ShouldCountAllSpansAcrossResourcesAndScopes()
    {
        var (client, _) = CreateClient();

        var count = await client.CountSpansAsync(new Dash0Query(DateTimeOffset.UtcNow.AddMinutes(-5), "svc"));

        count.Should().Be(4);
    }

    [TestMethod]
    public async Task CountSpansAsync_ShouldQuerySpansApiWithServiceAndFilters()
    {
        var (client, handler) = CreateClient();
        var since = new DateTimeOffset(2026, 11, 2, 10, 0, 0, TimeSpan.Zero);

        await client.CountSpansAsync(new Dash0Query(since, "svc", [Dash0Filter.Is("otel.span.name", "Job"), Dash0Filter.Contains("FilePath", "a.txt")]));

        handler.LastRequest!.Method.Should().Be(HttpMethod.Post);
        handler.LastRequest.RequestUri!.ToString().Should().Be("https://api.test.dash0.com/api/spans");

        var body = JsonNode.Parse(handler.LastBody!)!;
        body["dataset"]!.GetValue<string>().Should().Be("test-dataset");
        body["timeRange"]!["from"]!.GetValue<string>().Should().StartWith("2026-11-02T10:00:00");
        body["timeRange"]!["to"]!.GetValue<string>().Should().Be("now");
        Filters(handler).Should().BeEquivalentTo(new[]
        {
            ("service.name", "is", "svc"),
            ("otel.span.name", "is", "Job"),
            ("FilePath", "contains", "a.txt"),
        });
    }

    [TestMethod]
    public async Task CountSpansAsync_WithoutServiceName_ShouldOnlySendGivenFilters()
    {
        var (client, handler) = CreateClient();

        await client.CountSpansAsync(new Dash0Query(DateTimeOffset.UtcNow, Filters: [Dash0Filter.Contains("service.name", "front")]));

        Filters(handler).Should().BeEquivalentTo(new[] { ("service.name", "contains", "front") });
    }

    [TestMethod]
    public async Task CountLogsAsync_ShouldQueryLogsApiAndCountLogRecords()
    {
        var (client, handler) = CreateClient("""{"resourceLogs":[{"scopeLogs":[{"logRecords":[{},{}]}]}]}""");

        var count = await client.CountLogsAsync(new Dash0Query(DateTimeOffset.UtcNow, "svc"));

        count.Should().Be(2);
        handler.LastRequest!.RequestUri!.AbsolutePath.Should().Be("/api/logs");
    }

    [TestMethod]
    [DataRow("""{"resourceSpans":[]}""")]
    [DataRow("{}")]
    public async Task CountSpansAsync_ShouldReturnZero_WhenNothingReturned(string response)
    {
        var (client, _) = CreateClient(response);

        (await client.CountSpansAsync(new Dash0Query(DateTimeOffset.UtcNow, "svc"))).Should().Be(0);
    }

    [TestMethod]
    public async Task CountSpansAsync_ShouldThrow_WhenApiReturnsError()
    {
        var (client, _) = CreateClient("unauthorized", HttpStatusCode.Unauthorized);

        var act = () => client.CountSpansAsync(new Dash0Query(DateTimeOffset.UtcNow, "svc"));

        await act.Should().ThrowAsync<HttpRequestException>();
    }

    [TestMethod]
    public async Task WaitForCountAsync_ShouldReturnOnceExpectedCountReached()
    {
        var (client, _) = CreateClient();
        var calls = 0;

        var result = await client.WaitForCountAsync(_ => Task.FromResult(++calls), 3, TimeSpan.FromSeconds(10), TimeSpan.FromMilliseconds(10));

        result.Should().Be(3);
    }

    [TestMethod]
    public async Task WaitForCountAsync_ShouldReturnLastCount_WhenTimeoutElapses()
    {
        var (client, _) = CreateClient();

        var result = await client.WaitForCountAsync(_ => Task.FromResult(1), 5, TimeSpan.FromMilliseconds(100), TimeSpan.FromMilliseconds(10));

        result.Should().Be(1);
    }

    private sealed class StubHandler(string responseBody, HttpStatusCode status) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }
        public string? LastBody { get; private set; }

        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            LastBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);
            return new HttpResponseMessage(status) { Content = new StringContent(responseBody, Encoding.UTF8, "application/json") };
        }
    }
}
