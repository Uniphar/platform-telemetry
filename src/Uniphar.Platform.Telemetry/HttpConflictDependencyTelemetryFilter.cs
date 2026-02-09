using System.Net;

namespace Uniphar.Platform.Telemetry;

/// <summary>
/// Filters out 409 conflict error dependencies to prevent them from being recorded as failed dependencies in Application Insights.
/// </summary>
public sealed class HttpConflictDependencyTelemetryFilter : BaseProcessor<Activity>
{
    /// <summary>
    /// Overrides the OnEnd method to check if the activity should be filtered out.
    /// </summary>
    public override void OnEnd(Activity activity)
    {
        if (ShouldFilterOut(activity))
        {
            var status = activity.GetTagItem("http.response.status_code");
            if (status != null && int.TryParse(status.ToString(), out var statusCode) && statusCode == (int)HttpStatusCode.Conflict)
            {
                //remove the flag to prevent recording of failed dependency, learn more
                //https://www.stevejgordon.co.uk/disabling-recording-of-an-activity-span-in-dotnet-opentelemetry-instrumentation
                activity.ActivityTraceFlags &= ~ActivityTraceFlags.Recorded;
            }
        }

        base.OnEnd(activity);
    }

    private static bool ShouldFilterOut(Activity activity)
    {
        //Blob
        if (activity.DisplayName.StartsWith("Azure blob:", StringComparison.OrdinalIgnoreCase))
            return true;

        //ShareDirectoryClient
        if (activity.DisplayName.Contains("CreateIfNotExists", StringComparison.OrdinalIgnoreCase))
            return true;

        //File shares
        var url = activity.GetTagItem("url.full")?.ToString() ?? activity.GetTagItem("http.url")?.ToString();
        if (url?.Contains(".core.windows.net", StringComparison.OrdinalIgnoreCase) == true)
            return true;

        return false;
    }
}