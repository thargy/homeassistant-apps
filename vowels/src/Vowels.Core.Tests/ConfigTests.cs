using Xunit;
using Vowels.Daemon.Services;
using Vowels.Core.Models;
using System.IO;

namespace Vowels.Core.Tests;

public class ConfigTests
{
    [Fact]
    public void Load_ShouldMergeYamlAndJson()
    {
        // Arrange
        var loader = ConfigLoader.Instance;
        var yamlPath = "test_config.yaml";
        var jsonPath = "test_options.json";

        var yamlContent = @"
ha_url: http://dev-ha:8123
ingress_port: 8080
entities:
  - id: sensor.test_yaml
";
        var jsonContent = @"{
            ""ha_url"": ""http://supervisor/core/api"",
            ""entities"": [
                { ""id"": ""sensor.test_json"" }
            ]
        }";

        File.WriteAllText(yamlPath, yamlContent);
        File.WriteAllText(jsonPath, jsonContent);

        try
        {
            // Act
            var config = loader.Load(yamlPath, jsonPath);

            // Assert
            Assert.Equal("http://supervisor/core/api", config.HaUrl);
            Assert.Equal(8080, config.IngressPort);
            Assert.Single(config.Entities);
            Assert.Equal("sensor.test_json", config.Entities[0].Id);
        }
        finally
        {
            if (File.Exists(yamlPath)) File.Delete(yamlPath);
            if (File.Exists(jsonPath)) File.Delete(jsonPath);
        }
    }
}
