using System.Reflection;
using Vowels.Common.Attributes;

namespace Vowels.Daemon.Plugins;

public class PluginManager
{
    public IEnumerable<Type> DiscoverPlugins(string pluginsDirectory)
    {
        var pluginTypes = new List<Type>();
        
        if (!Directory.Exists(pluginsDirectory)) return pluginTypes;

        foreach (var pluginDir in Directory.GetDirectories(pluginsDirectory))
        {
            var pluginDlls = Directory.GetFiles(pluginDir, "*.dll");
            
            // Each plugin subdirectory contains its own DLLs.
            // Load each DLL in its own isolated AssemblyLoadContext.
            foreach (var dll in pluginDlls)
            {
                try
                {
                    var loadContext = new PluginLoadContext(dll);
                    var assembly = loadContext.LoadFromAssemblyName(
                        new AssemblyName(Path.GetFileNameWithoutExtension(dll)));
                    
                    foreach (var type in assembly.GetTypes())
                    {
                        if (type.GetCustomAttribute<VowelsPluginAttribute>() != null)
                        {
                            pluginTypes.Add(type);
                        }
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Failed to load plugin assembly {dll}: {ex.Message}");
                }
            }
        }
        
        return pluginTypes;
    }
}
