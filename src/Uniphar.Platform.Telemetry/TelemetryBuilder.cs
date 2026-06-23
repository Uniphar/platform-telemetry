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

    internal bool EnableDiagnosticLogging { get; set; }
    internal IEnumerable<ExceptionHandlingRule> ExceptionHandlingRules { get; set; }
    internal IEnumerable<string> PathsToFilterOutStartingWith { get; set; }
    internal DependencyFilterConfiguration? DependencyFilterConfiguration { get; set; }
    internal string? AppInsightsConnectionString { get; set; } = null;


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
        // Suppress any environment-based console logging from OpenTelemetry SDK itself
        Environment.SetEnvironmentVariable("OTEL_LOG_LEVEL", "none");

        // Adjust BatchExportProcessor defaults to reduce the chance of the telemetry drop/data-loss.
        // https://github.com/open-telemetry/opentelemetry-dotnet/blob/main/src/OpenTelemetry/BatchExportProcessor.cs#L109
        SetEnvDefault("OTEL_BSP_SCHEDULE_DELAY", "1000"); // default 5000 ms
        SetEnvDefault("OTEL_BSP_MAX_QUEUE_SIZE", "4096"); // default 2048
        SetEnvDefault("OTEL_BLRP_MAX_QUEUE_SIZE", "4096"); //default 2048

        // Remove all default logging providers (Console, Debug, EventSource) so that
        // application logs no longer write to stdout/stderr. Telemetry is exported via the configured exporter(s).
        _builder.Logging.ClearProviders();

        var appInsightsConnectionString = AppInsightsConnectionString
                                            ?? _builder.Configuration["APPLICATIONINSIGHTS_CONNECTION_STRING"]
                                            ?? Environment.GetEnvironmentVariable("APPLICATIONINSIGHTS_CONNECTION_STRING");
        var otlpEndpoint = _builder.Configuration["OTEL_EXPORTER_OTLP_ENDPOINT"]
                           ?? Environment.GetEnvironmentVariable("OTEL_EXPORTER_OTLP_ENDPOINT");

        var useAzureMonitor = !string.IsNullOrWhiteSpace(appInsightsConnectionString);
        var useOtlp = !string.IsNullOrWhiteSpace(otlpEndpoint);

        if (!useAzureMonitor && !useOtlp)
            throw new InvalidOperationException(
                $"No telemetry exporter configured. Set APPLICATIONINSIGHTS_CONNECTION_STRING and/or OTEL_EXPORTER_OTLP_ENDPOINT (or call WithAppInsightsConnectionString(...)).");

        var otel = _builder
            .Services
            .AddOpenTelemetry();

        if (useAzureMonitor)
        {
            otel.UseAzureMonitor(options =>
            {
                options.ConnectionString = appInsightsConnectionString;
                // NOTE: Azure.Monitor.OpenTelemetry.AspNetCore v1.5.0 changed the default sampler
                // to RateLimitedSampler (5 traces/sec). Both options are required to restore 100% sampling:
                // See: https://github.com/Azure/azure-sdk-for-net/blob/main/sdk/monitor/Azure.Monitor.OpenTelemetry.AspNetCore/CHANGELOG.md
                options.SamplingRatio = 1.0f;
                options.TracesPerSecond = null;

                // Disable the built-in TraceBasedLogsSampler to prevent it from dropping any logs based on trace sampling decisions.
                options.EnableTraceBasedLogsSampler = false;
            });
        }

        if (useOtlp)
        {
            otel.UseOtlpExporter();
        }

        otel
            .ConfigureResource(resource =>
            {
                // Override 'service.instance.id' and 'host.name' resource attributes to ensure telemetry reflects the current pod or machine name.
                // The default values set by ResourceBuilder.CreateDefault() use an auto-generated guid for the running instance.
                var podName = Environment.MachineName;
                resource
                    .AddTelemetrySdk()
                    .AddService(_appName)
                    .AddAttributes(new Dictionary<string, object>
                    {
                        ["service.instance.id"] = podName,
                        ["host.name"] = podName
                    });
            })
            .WithTracing(tracerProviderBuilder =>
            {
                tracerProviderBuilder
                    .AddSource(_appName)
                    .AddSource(CustomEventTelemetryClient.CustomEventActivitySource.Name)
                    .AddAspNetCoreInstrumentation(options =>
                    {
                        options.RecordException = true;
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

                // When Azure Monitor is not active, explicitly add HTTP client instrumentation
                // (the Azure Monitor distro normally registers this automatically).
                if (!useAzureMonitor)
                {
                    tracerProviderBuilder.AddHttpClientInstrumentation();
                }

                if (DependencyFilterConfiguration is not null)
                {
                    tracerProviderBuilder
                        //capture all dependencies from Azure SDKs
                        .AddSource("Azure.*")
                        .AddProcessor(new DependencyTelemetryFilter(DependencyFilterConfiguration));
                }
            })
            .WithLogging(loggerProviderBuilder =>
            {
                loggerProviderBuilder
                    .AddProcessor<AmbientPropertiesLogRecordInjector>()
                    .AddProcessor<ExceptionToCustomEventConverter>();
            })
            .WithMetrics(metricsBuilder =>
            {
                metricsBuilder
                    .AddHttpClientInstrumentation()
                    .AddRuntimeInstrumentation()
                    .AddMeter($"{_appName}.*");
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

        _builder.Services.AddSingleton<ICustomEventTelemetryClient, CustomEventTelemetryClient>(sp =>
        {
            // Auto-select emit mode based on which exporters are configured:
            // Azure Monitor only  -> LogRecord (maps to customEvents/traces table)
            // OTLP only           -> ActivitySpan (distributed tracing backends)
            // Both                -> Both signals emitted
            var emitMode = (useAzureMonitor, useOtlp) switch
            {
                (true, true) => CustomEventEmitMode.Both,
                (true, false) => CustomEventEmitMode.LogRecord,
                (false, true) => CustomEventEmitMode.ActivitySpan,
                _ => CustomEventEmitMode.ActivitySpan
            };

            var logger = sp.GetRequiredService<ILogger<CustomEventTelemetryClient>>();
            return new CustomEventTelemetryClient(
                EnableDiagnosticLogging,
                emitMode,
                logger);
        });
        // Register exception handling rules
        _builder.Services.AddSingleton<IEnumerable<ExceptionHandlingRule>>(_ => ExceptionHandlingRules);

        // Ensure the host gives the OpenTelemetry BatchExportProcessors enough time to flush
        _builder.Services.PostConfigure<HostOptions>(opts =>
        {
            opts.ShutdownTimeout = TimeSpan.FromSeconds(15);
        });
    }

    private static void SetEnvDefault(string key, string value)
    {
        if (string.IsNullOrEmpty(Environment.GetEnvironmentVariable(key)))
            Environment.SetEnvironmentVariable(key, value);
    }
}