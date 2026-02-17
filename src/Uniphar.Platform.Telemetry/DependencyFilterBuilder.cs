namespace Uniphar.Platform.Telemetry;

/// <summary>
/// Fluent builder for creating dependency filter configuration.
/// </summary>
public sealed class DependencyFilterBuilder
{
    private readonly List<DependencyFilterRule> _rules = [];

    /// <summary>
    /// Adds a filter rule for a specific Azure resource namespace and status codes.
    /// </summary>
    public DependencyFilterBuilder AddRule(string resourceNamespace, params int[] statusCodes)
    {
        if (statusCodes.Length == 0)
            throw new ArgumentException("At least one status code must be specified.", nameof(statusCodes));

        _rules.Add(new DependencyFilterRule
        {
            ResourceNamespace = resourceNamespace,
            StatusCodes = statusCodes
        });
        return this;
    }

    /// <summary>
    /// Builds the dependency filter configuration.
    /// </summary>
    internal DependencyFilterConfiguration Build()
    {
        return new DependencyFilterConfiguration
        {
            Rules = _rules.AsReadOnly()
        };
    }
}
