namespace Uniphar.Platform.Telemetry;

/// <summary>
///     Extension methods for adding telemetry clients.
/// </summary>
public static class TelemetryExtensions
{
    internal static readonly ActivitySource OperationActivitySource = new("Uniphar.Platform.Telemetry.Operations");

    /// <summary>
    ///     Sets ambient properties to be included in all telemetry events tracked within the current async context.
    ///     When no <see cref="Activity" /> is current (e.g. message handlers), an operation span is started and ended with the returned scope,
    ///     so everything emitted inside shares one trace.
    /// </summary>
    /// <param name="telemetry"></param>
    /// <param name="properties"></param>
    /// <param name="operationName">Optional operation span name; defaults to <c>ClassFile.Method</c> of the caller.</param>
    /// <param name="callerMember">Filled in by the compiler.</param>
    /// <param name="callerFilePath">Filled in by the compiler.</param>
    public static AmbientTelemetryProperties WithProperties(
        this ICustomEventTelemetryClient telemetry,
        IEnumerable<KeyValuePair<string, string>> properties,
        string? operationName = null,
        [CallerMemberName] string? callerMember = null,
        [CallerFilePath] string? callerFilePath = null)
    {
        var scope = AmbientTelemetryProperties.Initialize(properties);

        if (Activity.Current is null)
        {
            var name = string.IsNullOrWhiteSpace(operationName)
                ? $"{Path.GetFileNameWithoutExtension(callerFilePath)}.{callerMember}"
                : operationName;
            scope.OperationActivity = OperationActivitySource.StartActivity(name, ActivityKind.Consumer);
        }

        return scope;
    }

    /// <summary>
    ///     Registers OpenTelemetry services and configures telemetry for the application.
    /// </summary>
    /// <param name="builder"></param>
    /// <param name="appName">name of the service, this will be the role-name in insights</param>
    public static TelemetryBuilder RegisterOpenTelemetry(this IHostApplicationBuilder builder, string appName) => new(builder, appName);

    /// <param name="telemetryBuilder"></param>
    extension(TelemetryBuilder telemetryBuilder)
    {
        /// <summary>
        ///     Enables diagnostic logging of custom events to stderr (container logs).
        ///     Use to cross-check whether TrackEvent was called when events are missing in AppInsights.
        /// </summary>
        public TelemetryBuilder WithDiagnosticLogging(bool enabled = false)
        {
            telemetryBuilder.EnableDiagnosticLogging = enabled;
            return telemetryBuilder;
        }

        /// <summary>
        ///     Overrides the environment variable / configuration key used to read the
        ///     Application Insights connection string.
        ///     Defaults to <c>APPLICATIONINSIGHTS_CONNECTION_STRING</c> when not called.
        /// </summary>
        /// <param name="appInsightsConnectionString">The Application Insights connection string.</param>
        public TelemetryBuilder WithAppInsightsConnectionString(string appInsightsConnectionString)
        {
            ArgumentException.ThrowIfNullOrWhiteSpace(appInsightsConnectionString);

            telemetryBuilder.AppInsightsConnectionString = appInsightsConnectionString;

            return telemetryBuilder;

        }

        /// <summary>
        /// </summary>
        /// <param name="exceptionHandlingRules">exception handling rules</param>
        public TelemetryBuilder WithExceptionsFilters(IEnumerable<ExceptionHandlingRule> exceptionHandlingRules)
        {
            telemetryBuilder.ExceptionHandlingRules = exceptionHandlingRules;
            return telemetryBuilder;
        }

        /// <summary>
        ///     Configures paths to filter out from telemetry.
        /// </summary>
        /// <param name="pathsToFilterOutStartingWith">paths to exclude from telemetry</param>
        public TelemetryBuilder WithFilterExclusion(IEnumerable<string> pathsToFilterOutStartingWith)
        {
            telemetryBuilder.PathsToFilterOutStartingWith = pathsToFilterOutStartingWith;
            return telemetryBuilder;
        }

        /// <summary>
        ///     Configures HTTP dependency telemetry filtering with custom rules.
        ///     If no configuration is provided, uses the default configuration that filters 409 Conflict errors for Azure Storage and Service Bus.
        /// </summary>
        /// <param name="configuration">The dependency filter configuration.</param>
        /// <example>
        /// Configure custom rules:
        /// <code>
        /// builder
        ///     .WithDependencyFilter(new DependencyFilterConfiguration
        ///     {
        ///         Rules =
        ///         [
        ///             new DependencyFilterRule
        ///             {
        ///                 ResourceNamespace = AzureResourceNamespaces.Storage,
        ///                 StatusCodes = [(int)HttpStatusCode.Conflict]
        ///             }
        ///         ]
        ///     });
        /// </code>
        /// </example>
        public TelemetryBuilder WithDependencyFilter(DependencyFilterConfiguration? configuration = null)
        {
            telemetryBuilder.DependencyFilterConfiguration = configuration ?? DependencyFilterConfiguration.Default;
            return telemetryBuilder;
        }
    }
}