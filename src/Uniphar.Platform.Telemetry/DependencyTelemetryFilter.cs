namespace Uniphar.Platform.Telemetry;

/// <summary>
/// Filters out HTTP dependency errors based on configured rules to prevent them from being recorded as failed dependencies in Application Insights.
/// </summary>
public sealed class DependencyTelemetryFilter : BaseProcessor<Activity>
{
    private readonly DependencyFilterConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="DependencyTelemetryFilter"/> class.
    /// </summary>
    public DependencyTelemetryFilter(DependencyFilterConfiguration configuration)
    {
        _configuration = configuration;
    }

    /// <summary>
    /// Checks if the activity should be filtered out based on configured rules.
    /// </summary>
    public override void OnEnd(Activity activity)
    {
        var resourceNamespace = activity.GetTagItem("az.namespace")?.ToString() ?? activity.GetTagItem("azure.resource_provider.namespace")?.ToString();

        if (string.IsNullOrEmpty(resourceNamespace))
        {
            base.OnEnd(activity);
            return;
        }

        var applicableRules = _configuration.Rules
            .Where(rule => rule.ResourceNamespace.Equals(resourceNamespace, StringComparison.OrdinalIgnoreCase));
        
        if (TryGetStatusCode(activity, out var statusCode) &&
            applicableRules.Any(r => r.StatusCodes.Contains(statusCode)))
            activity.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;

        base.OnEnd(activity);
    }

    // error.type is only a status code for HTTP failures (e.g. "409"); otherwise it is an exception name, so fall back to the HTTP status tag.
    private static bool TryGetStatusCode(Activity activity, out int statusCode)
    {
        if (int.TryParse(activity.GetTagItem("error.type")?.ToString(), out statusCode)) return true;

        return int.TryParse(activity.GetTagItem("http.response.status_code")?.ToString(), out statusCode);
    }
}
