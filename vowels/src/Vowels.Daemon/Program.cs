using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vowels.Common;
using Vowels.Common.Attributes;
using Vowels.Daemon.Plugins;
using Vowels.Daemon.Services;
using System.Reflection;

namespace Vowels.Daemon;

class Program
{
    static async Task Main(string[] args)
    {
        // 1. Configuration (Singleton)
        var config = ConfigLoader.Instance.Load("config.yaml", "/data/options.json");

        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddSingleton(config);

        // 2. Discover and load plugins from the Plugins/ directory
        var pluginsPath = Path.Combine(AppContext.BaseDirectory, "Plugins");
        var pluginManager = new PluginManager();
        var pluginTypes = pluginManager.DiscoverPlugins(pluginsPath);

        // 3. Instantiate each discovered plugin and dispatch its scoped config
        foreach (var pluginType in pluginTypes)
        {
            var attr = pluginType.GetCustomAttribute<VowelsPluginAttribute>()!;
            
            // Look up plugin-specific config from the master config
            config.Plugins.TryGetValue(attr.Name, out var pluginConfig);

            Console.WriteLine($"Loading plugin: {attr.Name} v{attr.Version}");

            // TODO: Define a standard IVowelsPlugin interface that plugins implement,
            // then call plugin.Initialize(pluginConfig) here.
            // For now, log the discovered plugin.
        }

        using var host = builder.Build();
        await host.RunAsync();
    }
}
