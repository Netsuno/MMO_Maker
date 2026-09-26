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

    /// <summary>Catalogue prefabs publié (JSON additif, pas de bump de version fil).</summary>
    [JsonPropertyName("prefabs")]
    public IReadOnlyList<PublishedPrefabWireEntry> Prefabs { get; init; } = Array.Empty<PublishedPrefabWireEntry>();

    /// <summary>Placements prefab par carte publiée (JSON additif).</summary>
    [JsonPropertyName("prefabMaps")]
    public IReadOnlyList<PublishedPrefabMapWireEntry> PrefabMaps { get; init; } = Array.Empty<PublishedPrefabMapWireEntry>();
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

public sealed class PublishedPrefabWireEntry
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("displayName")]
    public string DisplayName { get; init; } = string.Empty;

    [JsonPropertyName("footprintWidthTiles")]
    public int FootprintWidthTiles { get; init; }

    [JsonPropertyName("footprintHeightTiles")]
    public int FootprintHeightTiles { get; init; }

    [JsonPropertyName("widthPixels")]
    public int WidthPixels { get; init; }

    [JsonPropertyName("heightPixels")]
    public int HeightPixels { get; init; }

    [JsonPropertyName("variants")]
    public IReadOnlyList<PublishedPrefabVariantWire> Variants { get; init; } = Array.Empty<PublishedPrefabVariantWire>();
}

public sealed class PublishedPrefabVariantWire
{
    [JsonPropertyName("facing")]
    public string Facing { get; init; } = "south";

    [JsonPropertyName("spriteFileName")]
    public string SpriteFileName { get; init; } = string.Empty;

    [JsonPropertyName("footprintWidthTiles")]
    public int FootprintWidthTiles { get; init; }

    [JsonPropertyName("footprintHeightTiles")]
    public int FootprintHeightTiles { get; init; }

    [JsonPropertyName("widthPixels")]
    public int WidthPixels { get; init; }

    [JsonPropertyName("heightPixels")]
    public int HeightPixels { get; init; }

    [JsonPropertyName("sha256Hex")]
    public string Sha256Hex { get; init; } = string.Empty;

    /// <summary>PNG optionnel (base64). Absent si le serveur n’a pas les octets publiés.</summary>
    [JsonPropertyName("pngBase64")]
    public string? PngBase64 { get; init; }
}

public sealed class PublishedPrefabMapWireEntry
{
    [JsonPropertyName("mapId")]
    public string MapId { get; init; } = string.Empty;

    [JsonPropertyName("mapName")]
    public string MapName { get; init; } = string.Empty;

    /// <summary>Identifiant runtime serveur (additif). Sert au client quand deux cartes ont le même nom.</summary>
    [JsonPropertyName("runtimeMapId")]
    public int? RuntimeMapId { get; init; }

    [JsonPropertyName("placements")]
    public IReadOnlyList<PublishedPrefabPlacementWire> Placements { get; init; } =
        Array.Empty<PublishedPrefabPlacementWire>();
}

public sealed class PublishedPrefabPlacementWire
{
    [JsonPropertyName("prefabId")]
    public string PrefabId { get; init; } = string.Empty;

    [JsonPropertyName("facing")]
    public string Facing { get; init; } = "south";

    [JsonPropertyName("tileX")]
    public int TileX { get; init; }

    [JsonPropertyName("tileY")]
    public int TileY { get; init; }
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

    /// <summary>Prix de revente. 0 si le catalogue ancien ne l’envoie pas.</summary>
    [JsonPropertyName("sellPrice")]
    public int SellPrice { get; init; }

    /// <summary>Taille de pile. 0 si absente (le client ne bloque pas un empilement inconnu).</summary>
    [JsonPropertyName("maxStack")]
    public int MaxStack { get; init; }
}

public sealed class PublishedSpellWireEntry
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("mpCost")]
    public int MpCost { get; init; }

    /// <summary>
    /// <c>Spell</c> ou <c>Skill</c>. Absent sur un catalogue ancien : le client traite l’entrée comme un sort.
    /// </summary>
    [JsonPropertyName("kind")]
    public string Kind { get; init; } = string.Empty;

    /// <summary>Recharge en millisecondes. 0 si le catalogue ancien ne l’envoie pas.</summary>
    [JsonPropertyName("cooldownMs")]
    public int CooldownMs { get; init; }

    /// <summary>Nom de <see cref="Frog.Core.Enums.TargetType"/>. Vide si absent.</summary>
    [JsonPropertyName("targetType")]
    public string TargetType { get; init; } = string.Empty;
}

public sealed class PublishedShopWireEntry
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    [JsonPropertyName("itemIds")]
    public IReadOnlyList<string> ItemIds { get; init; } = Array.Empty<string>();

    /// <summary>Prix et stock (JSON additif). Vide sur un catalogue ancien : le client retombe sur <see cref="ItemIds"/>.</summary>
    [JsonPropertyName("listings")]
    public IReadOnlyList<PublishedShopListingWireEntry> Listings { get; init; } = Array.Empty<PublishedShopListingWireEntry>();
}

public sealed class PublishedShopListingWireEntry
{
    [JsonPropertyName("itemId")]
    public string ItemId { get; init; } = string.Empty;

    [JsonPropertyName("price")]
    public int Price { get; init; }

    /// <summary>Null et <see cref="Unlimited"/> : stock illimité.</summary>
    [JsonPropertyName("stock")]
    public int? Stock { get; init; }

    [JsonPropertyName("unlimited")]
    public bool Unlimited { get; init; }
}

public sealed class PublishedNpcWireEntry
{
    [JsonPropertyName("id")]
    public string Id { get; init; } = string.Empty;

    [JsonPropertyName("name")]
    public string Name { get; init; } = string.Empty;

    /// <summary>Boutique liée (JSON additif). Vide si le PNJ n’en a pas.</summary>
    [JsonPropertyName("shopId")]
    public string ShopId { get; init; } = string.Empty;
}
