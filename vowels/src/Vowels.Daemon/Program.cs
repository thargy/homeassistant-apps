using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vowels.Core.Common;
using Vowels.Core.Registry;
using Vowels.FileStoreRegistry;
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

        // 2. Storage Services (Singletons)
        // Manual initialization in dependency order
        var store = new FileStoreManager("vowels_data"); // Directory for hourly files
        EntityRegistry.Initialize(store);

        // Register instances in DI for components that still use injection
        builder.Services.AddSingleton<IEntityStore>(store);
        builder.Services.AddSingleton(EntityRegistry.Instance);

        // 3. Background Workers (To be implemented in Phase 3)
        // builder.Services.AddHostedService<StorageWorker>();

        using var host = builder.Build();
        await host.RunAsync();
    }
}
