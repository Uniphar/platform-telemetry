namespace Uniphar.Platform.Telemetry;

/// <summary>
/// Azure resource provider namespaces used for telemetry filtering.
/// </summary>
public static class AzureResourceNamespaces
{
    /// <summary>
    /// Azure Storage services (Blob, Queue, Table, File)
    /// </summary>
    public const string Storage = "Microsoft.Storage";

    /// <summary>
    /// Azure Container Registry
    /// </summary>
    public const string ContainerRegistry = "Microsoft.ContainerRegistry";

    /// <summary>
    /// Azure Service Bus
    /// </summary>
    public const string ServiceBus = "Microsoft.ServiceBus";
}
