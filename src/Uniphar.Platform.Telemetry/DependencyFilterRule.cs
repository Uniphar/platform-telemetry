namespace Uniphar.Platform.Telemetry;

/// <summary>
/// Defines a rule for filtering HTTP dependency telemetry based on resource type and status codes.
/// </summary>
public sealed class DependencyFilterRule
{
    /// <summary>
    /// The Azure resource type to filter.
    /// </summary>
    public required string ResourceNamespace { get; init; }

    /// <summary>
    /// The HTTP status codes to filter out (e.g., 401, 403, 409).
    /// </summary>
    public required IReadOnlyCollection<int> StatusCodes { get; init; }
}
