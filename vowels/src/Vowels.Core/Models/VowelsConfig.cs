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
    [JsonPropertyName("entity_id")]
    public string EntityId { get; init; } = string.Empty;

    [JsonPropertyName("friendly_name")]
    public string? FriendlyName { get; init; }

    [JsonPropertyName("page_hint")]
    public int PageHint { get; init; } = 1;
}

[JsonSourceGenerationOptions(WriteIndented = true)]
[JsonSerializable(typeof(VowelsConfig))]
public partial class VowelsJsonContext : JsonSerializerContext
{
}
