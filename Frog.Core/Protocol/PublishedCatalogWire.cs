using System.Text.Json.Serialization;

namespace Frog.Core.Protocol;

/// <summary>Catalogue contenu publié pour le client gameplay (JSON UTF-8).</summary>
public sealed class PublishedCatalogWire
{
    [JsonPropertyName("classes")]
    public IReadOnlyList<PublishedClassWireEntry> Classes { get; init; } = Array.Empty<PublishedClassWireEntry>();

    [JsonPropertyName("items")]
    public IReadOnlyList<PublishedItemWireEntry> Items { get; init; } = Array.Empty<PublishedItemWireEntry>();

    [JsonPropertyName("spells")]
    public IReadOnlyList<PublishedSpellWireEntry> Spells { get; init; } = Array.Empty<PublishedSpellWireEntry>();

    [JsonPropertyName("shops")]
    public IReadOnlyList<PublishedShopWireEntry> Shops { get; init; } = Array.Empty<PublishedShopWireEntry>();

    [JsonPropertyName("npcs")]
    public IReadOnlyList<PublishedNpcWireEntry> Npcs { get; init; } = Array.Empty<PublishedNpcWireEntry>();
}

public sealed class PublishedClassWireEntry
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("description")]
    public string Description { get; init; } = string.Empty;
}

public sealed class PublishedItemWireEntry
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("type")]
    public string Type { get; init; } = string.Empty;

    [JsonPropertyName("stackable")]
    public bool Stackable { get; init; }
}

public sealed class PublishedSpellWireEntry
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("mpCost")]
    public int MpCost { get; init; }
}

public sealed class PublishedShopWireEntry
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("itemIds")]
    public IReadOnlyList<string> ItemIds { get; init; } = Array.Empty<string>();
}

public sealed class PublishedNpcWireEntry
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
}
