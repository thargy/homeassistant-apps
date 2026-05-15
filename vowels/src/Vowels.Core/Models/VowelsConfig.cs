using System.Text.Json.Serialization;
using YamlDotNet.Serialization;

namespace Vowels.Core.Models;

/// <summary>
/// Configuration for the Vowels daemon.
/// </summary>
[YamlSerializable]
public record VowelsConfig
{
    /// <summary>
    /// The base URL for the Home Assistant API.
    /// </summary>
    [JsonPropertyName("ha_url")]
    [YamlMember(Alias = "ha_url")]
    public string HaUrl { get; init; } = "http://supervisor/core/api";

    /// <summary>
    /// Long-lived access token for Home Assistant.
    /// </summary>
    [JsonPropertyName("ha_token")]
    [YamlMember(Alias = "ha_token")]
    public string? HaToken { get; init; }

    /// <summary>
    /// Port for the Ingress server.
    /// </summary>
    [JsonPropertyName("ingress_port")]
    [YamlMember(Alias = "ingress_port")]
    public int IngressPort { get; init; } = 5000;

    /// <summary>
    /// List of entity schemas to monitor.
    /// </summary>
    [JsonPropertyName("entities")]
    [YamlMember(Alias = "entities")]
    public List<EntitySchema> Entities { get; init; } = new();

    /// <summary>
    /// Plugin-specific configuration blocks. Each key is the plugin name (matching
    /// <see cref="Vowels.Common.Attributes.VowelsPluginAttribute.Name"/>) and the value
    /// is a free-form dictionary of settings dispatched to that plugin on startup.
    /// </summary>
    [JsonPropertyName("plugins")]
    [YamlMember(Alias = "plugins")]
    public Dictionary<string, Dictionary<string, object>> Plugins { get; init; } = new();
}

/// <summary>
/// Defines the schema for a single Home Assistant entity.
/// </summary>
[YamlSerializable]
public record EntitySchema
{
    /// <summary>
    /// The Home Assistant entity ID.
    /// </summary>
    [JsonPropertyName("id")]
    [YamlMember(Alias = "id")]
    public string Id { get; init; } = string.Empty;

    /// <summary>
    /// List of attribute names to track for this entity.
    /// </summary>
    [JsonPropertyName("attributes")]
    [YamlMember(Alias = "attributes")]
    public List<string> Attributes { get; init; } = new();
}

/// <summary>
/// JSON source generation context for VowelsConfig.
/// </summary>
[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(VowelsConfig))]
public partial class VowelsJsonContext : JsonSerializerContext
{
}

/// <summary>
/// YAML source generation context for VowelsConfig, ensuring AOT compatibility.
/// </summary>
[YamlStaticContext]
[YamlSerializable(typeof(VowelsConfig))]
[YamlSerializable(typeof(EntitySchema))]
[YamlSerializable(typeof(List<EntitySchema>))]
[YamlSerializable(typeof(List<string>))]
[YamlSerializable(typeof(Dictionary<string, Dictionary<string, object>>))]
[YamlSerializable(typeof(Dictionary<string, object>))]
public partial class VowelsYamlContext : StaticContext
{
}
