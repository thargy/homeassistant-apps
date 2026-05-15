using System.Reflection;
using System.Runtime.Loader;

namespace Vowels.Daemon.Plugins;

public class PluginLoadContext(string pluginPath) : AssemblyLoadContext(isCollectible: true)
{
    private readonly AssemblyDependencyResolver _resolver = new AssemblyDependencyResolver(pluginPath);

    protected override Assembly? Load(AssemblyName assemblyName)
    {
        // Vowels.Common is the shared contract assembly — it must resolve from the
        // default context so that types (interfaces, attributes) have the same
        // identity in both host and plugin code. Returning null here causes the
        // runtime to fall back to AssemblyLoadContext.Default.
        // All other assemblies are plugin-private; we don't restrict plugin authors
        // beyond this single shared boundary.
        if (assemblyName.Name?.StartsWith("Vowels.Common", StringComparison.OrdinalIgnoreCase) is true)
            return null;

        string? assemblyPath = _resolver.ResolveAssemblyToPath(assemblyName);
        if (assemblyPath != null)
        {
            return LoadFromAssemblyPath(assemblyPath);
        }

        return null;
    }

    protected override IntPtr LoadUnmanagedDll(string unmanagedDllName)
    {
        string? libraryPath = _resolver.ResolveUnmanagedDllToPath(unmanagedDllName);
        if (libraryPath != null)
        {
            return LoadUnmanagedDllFromPath(libraryPath);
        }

        return IntPtr.Zero;
    }
}
