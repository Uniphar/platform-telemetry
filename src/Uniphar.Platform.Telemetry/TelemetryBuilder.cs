namespace Uniphar.Platform.Telemetry;

using Microsoft.AspNetCore.Http;
using System.Diagnostics;

/// <summary>
///     Builder for configuring OpenTelemetry services.
/// </summary>
public sealed class TelemetryBuilder
{
    private readonly string _appName;
    private readonly IHostApplicationBuilder _builder;

    internal TelemetryBuilder(IHostApplicationBuilder builder, string appName)
    {
        _builder = builder;
        _appName = appName;
        PathsToFilterOutStartingWith = ["/health"];
        ExceptionHandlingRules = [];
    }

    internal IEnumerable<ExceptionHandlingRule> ExceptionHandlingRules { get; set; }
    internal IEnumerable<string> PathsToFilterOutStartingWith { get; set; }

    /// <summary>
    /// Determines whether a given HTTP request should be sampled for telemetry,
    /// applying health-path filtering only when the response is successful (2xx-3xx).
    /// </summary>
    internal static bool ShouldSampleRequest(HttpContext httpContext, IEnumerable<string> pathsToFilterOutStartingWith)
    {
        var path = httpContext.Request.Path;

        if (path.HasValue)
        {
            bool success = true;

            try
            {
                success = httpContext.Response.StatusCode is (>= 200 and < 400);
            }
            catch
            {
                // If StatusCode is inaccessible, default to success=true to avoid false negatives in filtering.
            }

            if (success && pathsToFilterOutStartingWith.Any(p => path.Value.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                return false;
        }

        return true;
    }

    /// <summary>
    ///     Builds and configures the OpenTelemetry services.
    /// </summary>
    public void Build()
    {
        _builder.Services.AddSingleton<ICustomEventTelemetryClient, CustomEventTelemetryClient>();

        // Register exception handling rules
        _builder.Services.AddSingleton<IEnumerable<ExceptionHandlingRule>>(_ => ExceptionHandlingRules);


        var cloudRoleName = $"{_appName}";
        var resourceBuilder = ResourceBuilder
            .CreateDefault()
            .AddTelemetrySdk()
            .AddService(cloudRoleName);

        _builder.Logging.AddOpenTelemetry(options =>
        {
            options.SetResourceBuilder(resourceBuilder);
            options.IncludeScopes = true;
            options.IncludeFormattedMessage = true;
            options.ParseStateValues = true;
        });

        var appInsightsConnectionString = _builder.Configuration["APPLICATIONINSIGHTS:CONNECTIONSTRING"];
        _builder
            .Services
            .AddOpenTelemetry()
            .UseAzureMonitor(options => options.ConnectionString = appInsightsConnectionString)
            .ConfigureResource(resource =>
            {
                // Override 'service.instance.id' and 'host.name' resource attributes to ensure telemetry reflects the current pod or machine name.
                // The default values set earlier by ResourceBuilder use the auto-generated guid to represent the running instance.
                var podName = Environment.MachineName;
                resource.AddAttributes(new Dictionary<string, object>
                {
                    ["service.instance.id"] = podName,
                    ["host.name"] = podName
                });
            })
            .WithTracing(tracerProviderBuilder =>
            {
                tracerProviderBuilder
                    .AddSource(_appName)
                    .SetResourceBuilder(resourceBuilder)
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.Filter = httpContext =>
                        {
                            // Use extracted logic to make it testable.
                            return ShouldSampleRequest(httpContext, PathsToFilterOutStartingWith);
                        };
                        //override the display name of the Request activity to be the path with actual values, not the generic route with placeholders
                        options.EnrichWithHttpResponse = (activity, _) =>
                        {
                            var path = activity.GetTagItem("url.path")?.ToString();
                            if (!string.IsNullOrWhiteSpace(path))
                            {
                                activity.DisplayName = path;
                                activity.SetTag("http.route", path);
                            }
                        };
                    });

#if LOCAL || DEBUG
                tracerProviderBuilder.AddConsoleExporter();
                //no sampling in local environment
                tracerProviderBuilder.SetSampler(new AlwaysOnSampler());
#endif
            })
            .WithLogging(loggerProviderBuilder => loggerProviderBuilder
                .SetResourceBuilder(resourceBuilder)
                .AddProcessor<AmbientPropertiesLogRecordInjector>()
                .AddProcessor<ExceptionToCustomEventConverter>()
            )
            .WithMetrics(metricsBuilder =>
            {
                metricsBuilder
                    .SetResourceBuilder(resourceBuilder)
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter($"{_appName}.*");

#if LOCAL || DEBUG
                metricsBuilder.AddConsoleExporter();
#endif
            });

        //enrich Dependency telemetry with ambient properties
        ActivitySource.AddActivityListener(new()
        {
            ShouldListenTo = _ => true,
            Sample = (ref _) => ActivitySamplingResult.AllDataAndRecorded,
            ActivityStarted = activity =>
            {
                var activityTags = AmbientTelemetryProperties.AmbientProperties.SelectMany(p => p.PropertiesToInject);
                foreach (var (name, value) in activityTags) activity.SetTag(name, value);
            }
        });
    }
}