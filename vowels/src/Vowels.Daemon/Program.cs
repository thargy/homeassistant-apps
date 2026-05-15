using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vowels.Common;
using Vowels.Daemon.Plugins;
using Vowels.Daemon.Services;

namespace Vowels.Daemon;

public static class Program
{
    static async Task Main(string[] args)
    {
        // 1. Configuration (Singleton)
        var config = ConfigLoader.Instance.Load("config.yaml", "/data/options.json");

        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddSingleton(config);

        // 2. Discover and load plugins from the Plugins/ directory
        var pluginsPath = Path.Combine(AppContext.BaseDirectory, "Plugins");
        var pluginTypes = PluginManager.DiscoverPlugins(pluginsPath);

        // 3. Instantiate each discovered plugin and dispatch its scoped config
        foreach (var plugin in pluginTypes)
        {
            // Look up plugin-specific config from the master config
            config.Plugins.TryGetValue(plugin.Name, out var pluginConfig);

            Console.WriteLine($"Loading plugin: {plugin.Name} v{plugin.Version}");

            // TODO: Define a standard IVowelsPlugin interface that plugins implement,
            // then call pluginManager.CreateInstance(plugin, <ctor args from pluginConfig>)
            // and register the result with the DI container.
        }

        using var host = builder.Build();
        await host.RunAsync();
    }
}
