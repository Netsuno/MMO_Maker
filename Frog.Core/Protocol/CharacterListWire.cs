using System.Text.Json.Serialization;

namespace Frog.Core.Protocol;

/// <summary>Élément JSON pour <see cref="Frog.Core.Enums.PacketId.CharacterListResult"/>.</summary>
public sealed class CharacterListWireEntry
{
    [JsonPropertyName("id")]
    public string Id { get; set; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; set; } = string.Empty;
}
