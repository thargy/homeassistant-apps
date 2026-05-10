using System.Text.Json;
using Vowels.Core.Models;
using YamlDotNet.Serialization;
using YamlDotNet.Serialization.NamingConventions;

namespace Vowels.Daemon.Services;

public class ConfigLoader
{
    private readonly IDeserializer _yamlDeserializer;

    public ConfigLoader()
    {
        _yamlDeserializer = new DeserializerBuilder()
            .WithNamingConvention(UnderscoredNamingConvention.Instance)
            .IgnoreUnmatchedProperties()
            .Build();
    }

    public VowelsConfig Load(string? yamlPath = null, string? jsonPath = null)
    {
        VowelsConfig config = new();

        // 1. Load from YAML (Development config)
        if (!string.IsNullOrEmpty(yamlPath) && File.Exists(yamlPath))
        {
            var yamlContent = File.ReadAllText(yamlPath);
            var yamlConfig = _yamlDeserializer.Deserialize<VowelsConfig>(yamlContent);
            if (yamlConfig != null)
            {
                config = yamlConfig;
            }
        }

        // 2. Override with JSON (Supervisor options)
        if (!string.IsNullOrEmpty(jsonPath) && File.Exists(jsonPath))
        {
            var jsonContent = File.ReadAllText(jsonPath);
            var jsonConfig = JsonSerializer.Deserialize(jsonContent, VowelsJsonContext.Default.VowelsConfig);
            if (jsonConfig != null)
            {
                // Manual merge for AOT efficiency
                config = config with {
                    HaUrl = !string.IsNullOrEmpty(jsonConfig.HaUrl) ? jsonConfig.HaUrl : config.HaUrl,
                    HaToken = jsonConfig.HaToken ?? config.HaToken,
                    IngressPort = jsonConfig.IngressPort != 0 ? jsonConfig.IngressPort : config.IngressPort,
                    Entities = jsonConfig.Entities.Count > 0 ? jsonConfig.Entities : config.Entities
                };
            }
        }

        return config;
    }
}
