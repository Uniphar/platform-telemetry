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
    public static TelemetryBuilder RegisterOpenTelemetry(this IHostApplicationBuilder builder, string appName) => new(builder, appName);

    /// <param name="telemetryBuilder"></param>
    extension(TelemetryBuilder telemetryBuilder)
    {
        /// <summary>
        ///     Configures exception handling rules for telemetry.
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