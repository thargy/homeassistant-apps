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
        var loader = new ConfigLoader();
        var yamlPath = "test_config.yaml";
        var jsonPath = "test_options.json";

        var yamlContent = @"
ha_url: http://dev-ha:8123
ingress_port: 8080
entities:
  - entity_id: sensor.test_yaml
    page_hint: 5
";
        var jsonContent = @"{
            ""ha_url"": ""http://supervisor/core/api"",
            ""entities"": [
                { ""entity_id"": ""sensor.test_json"", ""page_hint"": 10 }
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
            Assert.Equal(8080, config.IngressPort); // Kept from YAML if not in JSON (assuming merge logic, though currently I just replace)
            // Wait, my current implementation replaces. Let's adjust implementation or test.
            // If the user wants "merging", I should probably implement a real merge.
        }
        finally
        {
            if (File.Exists(yamlPath)) File.Delete(yamlPath);
            if (File.Exists(jsonPath)) File.Delete(jsonPath);
        }
    }
}
