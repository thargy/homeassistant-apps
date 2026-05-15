namespace Vowels.Daemon.Plugins;

/// <summary>
/// Metadata and type handle for a discovered plugin, extracted from
/// CustomAttributeData to avoid cross-AssemblyLoadContext type identity issues.
/// </summary>
public record DiscoveredPlugin(
    string Name,
    string Version,
    bool AllowMultipleInstances,
    Type PluginType)
{
    /// <summary>
    /// Creates an instance of the plugin type via Activator.CreateInstance.
    /// All instantiation goes through here to keep ALC-boundary logic centralised.
    /// </summary>
    public object CreateInstance(params object[]? args)
    {
        return Activator.CreateInstance(PluginType, args)
            ?? throw new InvalidOperationException(
                $"Activator returned null for plugin '{Name}' ({PluginType.FullName})");
    }
}

