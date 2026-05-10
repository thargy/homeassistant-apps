using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Vowels.Core.Storage;
using Vowels.Daemon.Services;

namespace Vowels.Daemon;

class Program
{
    static async Task Main(string[] args)
    {
        // Use CreateSlimBuilder for Native AOT compatibility
        var builder = Host.CreateSlimBuilder(args);

        // 1. Configuration
        var loader = new ConfigLoader();
        var config = loader.Load("config.yaml", "/data/options.json");
        builder.Services.AddSingleton(config);

        // 2. Storage Services
        builder.Services.AddSingleton<IPageManager>(sp => 
            new PagedMmfManager("vowels_data.vowl", 1024 * 1024)); // 1MB initial
        builder.Services.AddSingleton<StringTable>();
        builder.Services.AddSingleton<SchemaRegistry>();
        builder.Services.AddSingleton<EntityStore>();

        // 3. Background Workers (To be implemented in Phase 3)
        // builder.Services.AddHostedService<StorageWorker>();

        using var host = builder.Build();
        await host.RunAsync();
    }
}
