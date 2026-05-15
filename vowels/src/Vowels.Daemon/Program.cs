using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vowels.Common;
using Vowels.Core.Registry;
using Vowels.Daemon.Services;

namespace Vowels.Daemon;

class Program
{
    static async Task Main(string[] args)
    {
        // 1. Configuration (Singleton)
        var config = ConfigLoader.Instance.Load("config.yaml", "/data/options.json");

        var builder = Host.CreateApplicationBuilder(args);
        builder.Services.AddSingleton(config);

        // 2. Storage and Plugin Services
        // TODO (Task 6/7): Wire PluginManager here to discover and load plugins
        // from the Plugins/ directory. FileStoreRegistry is now a plugin and no
        // longer directly referenced from the Daemon project.

        using var host = builder.Build();
        await host.RunAsync();
    }
}
