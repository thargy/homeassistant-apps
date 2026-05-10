using System.Text.Json.Serialization;

namespace Vowels.Core.Models;

public record VowelsConfig
{
    [JsonPropertyName("ha_url")]
    public string HaUrl { get; init; } = "http://supervisor/core/api";

    [JsonPropertyName("ha_token")]
    public string? HaToken { get; init; }

    [JsonPropertyName("ingress_port")]
    public int IngressPort { get; init; } = 5000;

    [JsonPropertyName("entities")]
    public List<EntitySchema> Entities { get; init; } = new();
}

public record EntitySchema
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("attributes")]
    public List<string> Attributes { get; init; } = new();
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(VowelsConfig))]
public partial class VowelsJsonContext : JsonSerializerContext
{
}
