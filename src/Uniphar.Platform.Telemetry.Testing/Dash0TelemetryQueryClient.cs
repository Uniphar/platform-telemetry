using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json.Nodes;

namespace Uniphar.Platform.Telemetry.Testing;

/// <summary>A single Dash0 attribute filter, e.g. <c>JobId is abc</c>.</summary>
public sealed record Dash0Filter(string Key, string Operator, string Value)
{
    public static Dash0Filter Is(string key, string value) => new(key, "is", value);

    public static Dash0Filter Contains(string key, string value) => new(key, "contains", value);
}

/// <summary>Describes what to look for in Dash0.</summary>
/// <param name="Since">Only telemetry newer than this moment is considered.</param>
/// <param name="ServiceName">Exact <c>service.name</c> to match; leave null to filter on it yourself via <paramref name="Filters" />.</param>
/// <param name="Filters">Additional attribute filters, all must match.</param>
public sealed record Dash0Query(DateTimeOffset Since, string? ServiceName = null, IReadOnlyList<Dash0Filter>? Filters = null)
{
    internal object[] ToFilterBody() =>
    [
        .. (ServiceName is null ? [] : new[] { Dash0Filter.Is("service.name", ServiceName) })
            .Concat(Filters ?? [])
            .Select(f => new { key = f.Key, @operator = f.Operator, value = f.Value })
    ];
}

/// <summary>
///     Queries telemetry that reached Dash0 (spans and logs) through the Dash0 REST API, so integration tests can
///     assert that telemetry really arrived with the expected attributes.
/// </summary>
public sealed class Dash0TelemetryQueryClient(HttpClient httpClient, string dataset)
{
    public const string AuthTokenEnvironmentVariable = "DASH0_AUTH_TOKEN";
    public const string ApiUrlEnvironmentVariable = "DASH0_API_URL";
    public const string DatasetEnvironmentVariable = "DASH0_DATASET";

    public static Dash0TelemetryQueryClient Create() =>
        new(CreateHttpClient(), GetRequiredEnvironmentVariable(DatasetEnvironmentVariable));

    /// <summary>
    ///     Creates an authenticated client for the Dash0 API, configured from the DASH0_* environment variables
    ///     (supplied by the workflow in CI, and by the developer's own environment locally).
    /// </summary>
    public static HttpClient CreateHttpClient()
    {
        var token = GetRequiredEnvironmentVariable(AuthTokenEnvironmentVariable);
        var client = new HttpClient
        {
            BaseAddress = new Uri(GetRequiredEnvironmentVariable(ApiUrlEnvironmentVariable).TrimEnd('/') + "/")
        };
        var authorization = token.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? token : $"Bearer {token}";
        client.DefaultRequestHeaders.Authorization = AuthenticationHeaderValue.Parse(authorization);

        return client;
    }

    public static string GetRequiredEnvironmentVariable(string key) =>
        Environment.GetEnvironmentVariable(key) is { } value && !string.IsNullOrWhiteSpace(value)
            ? value.Trim()
            : throw new InvalidOperationException($"Required environment variable `{key}` was not provided");

    public Task<int> CountSpansAsync(Dash0Query query, CancellationToken cancellationToken = default) =>
        CountAsync("api/spans", query, "resourceSpans", "scopeSpans", "spans", cancellationToken);

    public Task<int> CountLogsAsync(Dash0Query query, CancellationToken cancellationToken = default) =>
        CountAsync("api/logs", query, "resourceLogs", "scopeLogs", "logRecords", cancellationToken);

    /// <summary>
    ///     Polls until the count reaches <paramref name="expectedCount" /> (telemetry ingestion in Dash0 is asynchronous) or
    ///     the timeout elapses; returns the last observed count.
    /// </summary>
    public async Task<int> WaitForCountAsync(
        Func<CancellationToken, Task<int>> count,
        int expectedCount,
        TimeSpan timeout,
        TimeSpan? pollInterval = null,
        CancellationToken cancellationToken = default)
    {
        var interval = pollInterval ?? TimeSpan.FromSeconds(5);
        using var timeoutSource = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutSource.CancelAfter(timeout);
        var last = 0;

        try
        {
            while (true)
            {
                last = await count(timeoutSource.Token);
                if (last >= expectedCount) return last;

                await Task.Delay(interval, timeoutSource.Token);
            }
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return last;
        }
    }

    // Dash0 returns at most one page per call; callers compare against small counts, so a single page is enough.
    private async Task<int> CountAsync(
        string path, Dash0Query query, string resourceArray, string scopeArray, string itemArray, CancellationToken cancellationToken)
    {
        var body = new
        {
            dataset,
            timeRange = new { from = query.Since.UtcDateTime.ToString("O"), to = "now" },
            filter = query.ToFilterBody()
        };

        using var response = await httpClient.PostAsJsonAsync(path, body, cancellationToken);
        response.EnsureSuccessStatusCode();

        var payload = await response.Content.ReadFromJsonAsync<JsonNode>(cancellationToken);

        return payload?[resourceArray]?.AsArray()
            .SelectMany(resource => resource?[scopeArray]?.AsArray() ?? [])
            .Sum(scope => scope?[itemArray]?.AsArray().Count ?? 0) ?? 0;
    }
}
