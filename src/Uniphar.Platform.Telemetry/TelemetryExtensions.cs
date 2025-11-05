namespace Uniphar.Platform.Telemetry;

/// <summary>
///     Extension methods for adding telemetry clients.
/// </summary>
public static class TelemetryExtensions
{
    /// <summary>
    ///     Sets ambient properties to be included in all telemetry events tracked within the current async context.
    /// </summary>
    /// <param name="telemetry"></param>
    /// <param name="properties"></param>
    public static AmbientTelemetryProperties WithProperties(this ICustomEventTelemetryClient telemetry, IEnumerable<KeyValuePair<string, string>> properties) => AmbientTelemetryProperties.Initialize(properties);

    /// <summary>
    ///     Registers OpenTelemetry services and configures telemetry for the application.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="appName">name of the service, this will be the role-name in insights</param>
    /// <param name="exceptionHandlingRules">optional exception handling rules</param>
    public static void RegisterOpenTelemetry(this IHostApplicationBuilder builder, string appName, IEnumerable<ExceptionHandlingRule>? exceptionHandlingRules = null)
    {
        builder.Services.AddSingleton<ICustomEventTelemetryClient, CustomEventTelemetryClient>();

        // Register exception handling rules
        builder.Services.AddSingleton<IEnumerable<ExceptionHandlingRule>>(_ => exceptionHandlingRules ?? []);


        var cloudRoleName = $"{appName}";
        var resourceBuilder = ResourceBuilder
            .CreateDefault()
            .AddTelemetrySdk()
            .AddService(cloudRoleName);

        builder.Logging.AddOpenTelemetry(options =>
        {
            options.SetResourceBuilder(resourceBuilder);
            options.IncludeScopes = true;
            options.IncludeFormattedMessage = true;
            options.ParseStateValues = true;
        });

        var appInsightsConnectionString = builder.Configuration["APPLICATIONINSIGHTS:CONNECTIONSTRING"];
        builder
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
                    .AddSource(appName)
                    .SetResourceBuilder(resourceBuilder)
                    .AddAspNetCoreInstrumentation(options =>
                    {
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
                    .AddMeter($"{appName}.*");

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

/// <summary>
///     Represents a set of ambient properties for telemetry, disposable so they can be scoped.
/// </summary>
public sealed class AmbientTelemetryProperties : IDisposable
{
    private AmbientTelemetryProperties(IEnumerable<KeyValuePair<string, string>>? propertiesToInject)
    {
        var exclusions = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { "ado" };

        PropertiesToInject = propertiesToInject?.Where(x => !exclusions.Contains(x.Key)).ToImmutableArray() ??
                             ImmutableArray<KeyValuePair<string, string>>.Empty;
    }

    private static AsyncLocal<ImmutableList<AmbientTelemetryProperties>> AmbientPropertiesAsyncLocal { get; } = new();

    internal static ImmutableList<AmbientTelemetryProperties> AmbientProperties
    {
        get => AmbientPropertiesAsyncLocal.Value ?? ImmutableList<AmbientTelemetryProperties>.Empty;
        private set => AmbientPropertiesAsyncLocal.Value = value;
    }

    internal ImmutableArray<KeyValuePair<string, string>> PropertiesToInject { get; }


    /// <inheritdoc />
    public void Dispose()
    {
        AmbientProperties = AmbientProperties.Remove(this);
    }

    internal static AmbientTelemetryProperties Initialize(IEnumerable<KeyValuePair<string, string>>? propertiesToInject)
    {
        var ambientProps = new AmbientTelemetryProperties(propertiesToInject);
        // Insert at the beginning of the list so that these props take precedence over existing ambient props
        AmbientProperties = AmbientProperties.Insert(0, ambientProps);
        return ambientProps;
    }
}