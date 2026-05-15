using System.Reflection;
using Vowels.Common.Attributes;

namespace Vowels.Daemon.Plugins;

public static class PluginManager
{
    // DLL filename prefixes that are never plugin assemblies - skip without loading.
    private static readonly string[] _skipPrefixes =
    [
        "Vowels.Common",
        "Vowels.Core",
        "Microsoft.",
        "System.",
        "netstandard",
        "mscorlib",
    ];

    public static IEnumerable<DiscoveredPlugin> DiscoverPlugins(string pluginsDirectory)
    {
        var plugins = new List<DiscoveredPlugin>();

        if (!Directory.Exists(pluginsDirectory)) return plugins;

        foreach (var pluginDir in Directory.GetDirectories(pluginsDirectory))
        {
            foreach (var dll in Directory.GetFiles(pluginDir, "*.dll"))
            {
                var fileName = Path.GetFileNameWithoutExtension(dll);

                // Fast path: skip known infrastructure assemblies without loading.
                if (_skipPrefixes.Any(p => fileName.StartsWith(p, StringComparison.OrdinalIgnoreCase)))
                    continue;

                try
                {
                    var loadContext = new PluginLoadContext(dll);
                    var assembly = loadContext.LoadFromAssemblyName(new AssemblyName(fileName));

                    // Vowels.Common resolves from the default context (see PluginLoadContext),
                    // so attribute types are identical on both sides — typed access is safe.
                    if (assembly.GetCustomAttribute<VowelsPluginAssemblyAttribute>() is null)
                        continue;

                    foreach (var type in assembly.GetExportedTypes())
                    {
                        var attr = type.GetCustomAttribute<VowelsPluginAttribute>();
                        if (attr is null) continue;

                        plugins.Add(new DiscoveredPlugin(attr.Name, attr.Version, attr.AllowMultipleInstances, type));
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to load plugin assembly {dll}: {ex.Message}");
                }
            }
        }

        return plugins;
    }
}
