namespace Uniphar.Platform.Telemetry;

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