using System.Collections.Immutable;
using System.Diagnostics;
using Azure.Monitor.OpenTelemetry.AspNetCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using OpenTelemetry.Logs;
using OpenTelemetry.Metrics;
using OpenTelemetry.Resources;
using OpenTelemetry.Trace;

namespace Uniphar.Platform.Telemetry;

public static class TelemetryExtensions
{
    public static AmbientTelemetryProperties WithProperties(this ICustomEventTelemetryClient telemetry, IEnumerable<KeyValuePair<string, string>> properties) => AmbientTelemetryProperties.Initialize(properties);


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
            .WithTracing(x =>
            {
                x
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
                x.AddConsoleExporter();
                //no sampling in local environment
                x.SetSampler(new AlwaysOnSampler());
#endif
            })
            .WithLogging(x => x
                .SetResourceBuilder(resourceBuilder)
                .AddProcessor<AmbientPropertiesLogRecordEnricher>()
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
        ActivitySource.AddActivityListener(new ActivityListener
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
        set => AmbientPropertiesAsyncLocal.Value = value;
    }

    internal ImmutableArray<KeyValuePair<string, string>> PropertiesToInject { get; }

    public void Dispose()
    {
        AmbientProperties = AmbientProperties.Remove(this);
    }

    public static AmbientTelemetryProperties Initialize(IEnumerable<KeyValuePair<string, string>>? propertiesToInject)
    {
        var ambientProps = new AmbientTelemetryProperties(propertiesToInject);
        // Insert at the beginning of the list so that these props take precedence over existing ambient props
        AmbientProperties = AmbientProperties.Insert(0, ambientProps);
        return ambientProps;
    }
}