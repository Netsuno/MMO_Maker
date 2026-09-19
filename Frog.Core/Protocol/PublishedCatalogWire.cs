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

    /// <summary>Recettes publiées (JSON additif, pas de bump de version fil).</summary>
    [JsonPropertyName("recipes")]
    public IReadOnlyList<PublishedRecipeWireEntry> Recipes { get; init; } = Array.Empty<PublishedRecipeWireEntry>();

    /// <summary>Tilesets publiés avec palette éditeur (JSON additif, pas de bump de version fil).</summary>
    [JsonPropertyName("tilesets")]
    public IReadOnlyList<PublishedTilesetWireEntry> Tilesets { get; init; } = Array.Empty<PublishedTilesetWireEntry>();
}

public sealed class PublishedTilesetWireEntry
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("paletteId")]
    public int PaletteId { get; init; }

    [JsonPropertyName("logicalPath")]
    public string LogicalPath { get; init; } = string.Empty;

    [JsonPropertyName("sha256Hex")]
    public string Sha256Hex { get; init; } = string.Empty;

    [JsonPropertyName("tileSizePixels")]
    public int TileSizePixels { get; init; }

    [JsonPropertyName("widthPixels")]
    public int WidthPixels { get; init; }

    [JsonPropertyName("heightPixels")]
    public int HeightPixels { get; init; }

    /// <summary>PNG optionnel (base64). Absent si le serveur n’a pas le fichier sous la racine assets.</summary>
    [JsonPropertyName("pngBase64")]
    public string? PngBase64 { get; init; }
}

public sealed class PublishedRecipeWireEntry
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;
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
