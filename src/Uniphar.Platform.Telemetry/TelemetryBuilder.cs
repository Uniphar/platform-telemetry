namespace Uniphar.Platform.Telemetry;

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
    internal DependencyFilterConfiguration? DependencyFilterConfiguration { get; set; }

    /// <summary>
    /// Determines whether a given HTTP request should be sampled for telemetry,
    /// applying health-path filtering only when the response is successful (2xx-3xx).
    /// </summary>
    internal static bool ShouldSampleRequest(HttpContext httpContext, IEnumerable<string> pathsToFilterOutStartingWith)
    {
        var path = httpContext.Request.Path;

        if (!path.HasValue) return true;
        var success = true;

        try
        {
            success = httpContext.Response.StatusCode is (>= 200 and < 400);
        }
        catch
        {
            // If StatusCode is inaccessible, default to success=true to avoid false negatives in filtering.
        }

        return !success || !pathsToFilterOutStartingWith.Any(p => path.Value.StartsWith(p, StringComparison.OrdinalIgnoreCase));
    }

    /// <summary>
    ///     Builds and configures the OpenTelemetry services.
    /// </summary>
    public void Build()
    {
        _builder.Services.AddSingleton<ICustomEventTelemetryClient>(_ => new CustomEventTelemetryClient(_appName));

        // Register exception handling rules
        _builder.Services.AddSingleton<IEnumerable<ExceptionHandlingRule>>(_ => ExceptionHandlingRules);


        var resourceBuilder = ResourceBuilder
            .CreateDefault()
            .AddTelemetrySdk()
            .AddService(_appName);

        // Remove all default logging providers (Console, Debug, EventSource) so that
        // application logs no longer write to stdout/stderr. This prevents duplicate
        // data in ContainerLogsV2/ADX — all telemetry is exported directly to
        // Application Insights via the OpenTelemetry Azure Monitor exporter.
#if !LOCAL && !DEBUG
        _builder.Logging.ClearProviders();
#endif

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
                    .AddSource($"{_appName}.CustomEvents") // Add custom events source
                    .SetResourceBuilder(resourceBuilder)
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.Filter = httpContext => ShouldSampleRequest(httpContext, PathsToFilterOutStartingWith);
                        //override the display name of the Request activity to be the path with actual values, not the generic route with placeholders
                        options.EnrichWithHttpResponse = (activity, _) =>
                        {
                            var path = activity.GetTagItem("url.path")?.ToString();
                            if (string.IsNullOrWhiteSpace(path)) return;
                            activity.DisplayName = path;
                            activity.SetTag("http.route", path);
                        };
                    });

                if (DependencyFilterConfiguration is not null)
                {
                    tracerProviderBuilder
                        //capture all dependencies from Azure SDKs
                        .AddSource("Azure.*")
                        .AddProcessor(new DependencyTelemetryFilter(DependencyFilterConfiguration));
                }

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

        //enrich all telemetry (requests, dependencies, custom events) with ambient properties
        ActivitySource.AddActivityListener(new()
        {
            ShouldListenTo = _ => true,
            ActivityStarted = activity =>
            {
                var activityTags = AmbientTelemetryProperties.AmbientProperties.SelectMany(p => p.PropertiesToInject);
                foreach (var (name, value) in activityTags) activity.SetTag(name, value);
            }
        });
    }
}