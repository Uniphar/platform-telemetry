namespace Uniphar.Platform.Telemetry;

/// <summary>
/// Configuration for HTTP dependency telemetry filtering.
/// </summary>
public sealed class DependencyFilterConfiguration
{
    /// <summary>
    /// The filter rules to apply.
    /// </summary>
    public IReadOnlyCollection<DependencyFilterRule> Rules { get; init; } = [];

    /// <summary>
    /// Creates a default configuration that filters 409 Conflict errors for Azure Storage and Service Bus.
    /// </summary>
    public static DependencyFilterConfiguration Default => new()
    {
        Rules =
        [
            new DependencyFilterRule
            {
                ResourceNamespace = AzureResourceNamespaces.Storage,
                StatusCodes = [409]
            },
            new DependencyFilterRule
            {
                ResourceNamespace = AzureResourceNamespaces.ServiceBus,
                StatusCodes = [409]
            }
        ]
    };
}
