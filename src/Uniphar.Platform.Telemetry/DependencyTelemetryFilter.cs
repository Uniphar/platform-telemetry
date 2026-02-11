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
        var resourceNamespace = activity.GetTagItem("az.namespace")?.ToString()
                          ?? activity.GetTagItem("azure.resource_provider.namespace")?.ToString();

        if (string.IsNullOrEmpty(resourceNamespace))
        {
            base.OnEnd(activity);
            return;
        }

        var applicableRules = _configuration.Rules
            .Where(rule => rule.ResourceNamespace.Equals(resourceNamespace, StringComparison.OrdinalIgnoreCase));
        
        var status = activity.GetTagItem("error.type") ?? activity.GetTagItem("http.response.status_code");
        if (status != null && int.TryParse(status.ToString(), out var statusCode))
        {
            if (applicableRules.Any(r => r.StatusCodes.Contains(statusCode)))
            {
                activity.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
            }
        }

        base.OnEnd(activity);
    }
}
