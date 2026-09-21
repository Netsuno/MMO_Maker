using Frog.Core.IO;
using Frog.Core.Models;

namespace Frog.Application.Prefabs;

/// <summary>Sprite persisté (catalogue publié / snapshot PostgreSQL).</summary>
public sealed class PrefabSpritePersistEntry
{
    public string FileName { get; set; } = string.Empty;

    public string Sha256Hex { get; set; } = string.Empty;

    public string PngBase64 { get; set; } = string.Empty;
}

/// <summary>
/// Paquet prefab additif (hors <c>.fmap</c>) : catalogue + placements + PNG.
/// Stocké en JSON sur le brouillon / snapshot carte, et dans le catalogue fil.
/// </summary>
public sealed class MapPrefabPersistDocument
{
    public const int CurrentDocumentVersion = 1;

    public int DocumentVersion { get; set; } = CurrentDocumentVersion;

    public PrefabCatalog Catalog { get; set; } = new();

    public List<PrefabPlacement> Placements { get; set; } = new();

    public List<PrefabSpritePersistEntry> Sprites { get; set; } = new();

    public static MapPrefabPersistDocument Create(
        PrefabCatalog catalog,
        IReadOnlyList<PrefabPlacement> placements,
        IReadOnlyList<PrefabSpriteFile>? sprites = null)
    {
        ArgumentNullException.ThrowIfNull(catalog);
        ArgumentNullException.ThrowIfNull(placements);

        var usedIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (var placement in placements)
        {
            if (!string.IsNullOrWhiteSpace(placement?.PrefabId))
            {
                usedIds.Add(placement.PrefabId);
            }
        }

        var trimmed = new PrefabCatalog { CatalogVersion = catalog.CatalogVersion };
        foreach (var prefab in catalog.Prefabs)
        {
            if (prefab is null || string.IsNullOrWhiteSpace(prefab.Id))
            {
                continue;
            }

            if (usedIds.Count == 0 || usedIds.Contains(prefab.Id))
            {
                trimmed.Prefabs.Add(prefab);
            }
        }

        if (usedIds.Count == 0)
        {
            return new MapPrefabPersistDocument
            {
                DocumentVersion = CurrentDocumentVersion,
                Catalog = catalog,
                Placements = new List<PrefabPlacement>(),
                Sprites = new List<PrefabSpritePersistEntry>(),
            };
        }

        var neededSprites = PrefabPlacementService.CollectRequiredSpriteFileNames(trimmed, placements);
        var persistSprites = new List<PrefabSpritePersistEntry>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var sprite in sprites ?? Array.Empty<PrefabSpriteFile>())
        {
            var name = Path.GetFileName(sprite.FileName?.Trim() ?? string.Empty);
            if (string.IsNullOrEmpty(name)
                || name.Contains("..", StringComparison.Ordinal)
                || sprite.PngBytes.Length == 0
                || !seen.Add(name))
            {
                continue;
            }

            if (neededSprites.Count > 0 && !neededSprites.Contains(name))
            {
                continue;
            }

            persistSprites.Add(new PrefabSpritePersistEntry
            {
                FileName = name,
                Sha256Hex = Frog.Core.Models.TilesetDefinition.ComputeSha256Hex(sprite.PngBytes),
                PngBase64 = Convert.ToBase64String(sprite.PngBytes),
            });
        }

        return new MapPrefabPersistDocument
        {
            DocumentVersion = CurrentDocumentVersion,
            Catalog = trimmed,
            Placements = PrefabPlacementService.ClonePlacements(placements),
            Sprites = persistSprites,
        };
    }

    public IReadOnlyList<PrefabSpriteFile> ToSpriteFiles()
    {
        var files = new List<PrefabSpriteFile>();
        foreach (var sprite in Sprites)
        {
            if (!TryDecodeSprite(sprite, out var fileName, out var png))
            {
                continue;
            }

            files.Add(new PrefabSpriteFile(fileName, png));
        }

        return files;
    }

    public static bool TryDecodeSprite(PrefabSpritePersistEntry? entry, out string fileName, out byte[] png)
    {
        fileName = string.Empty;
        png = [];
        if (entry is null || string.IsNullOrWhiteSpace(entry.FileName) || string.IsNullOrWhiteSpace(entry.PngBase64))
        {
            return false;
        }

        var name = Path.GetFileName(entry.FileName.Trim());
        if (string.IsNullOrEmpty(name) || name.Contains("..", StringComparison.Ordinal))
        {
            return false;
        }

        byte[] bytes;
        try
        {
            bytes = Convert.FromBase64String(entry.PngBase64);
        }
        catch
        {
            return false;
        }

        if (bytes.Length == 0)
        {
            return false;
        }

        if (!string.IsNullOrWhiteSpace(entry.Sha256Hex) && entry.Sha256Hex.Length == 64)
        {
            var actual = Frog.Core.Models.TilesetDefinition.ComputeSha256Hex(bytes);
            if (!actual.Equals(entry.Sha256Hex, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }
        }

        fileName = name;
        png = bytes;
        return true;
    }
}

/// <summary>Sérialisation JSON du paquet prefab (UTF‑8, camelCase, enums en chaîne).</summary>
public static class MapPrefabPersistJson
{
    public static byte[] Serialize(MapPrefabPersistDocument document)
        => PrefabCatalogJson.SerializeObject(document);

    public static string SerializeToString(MapPrefabPersistDocument document)
        => System.Text.Encoding.UTF8.GetString(Serialize(document));

    public static MapPrefabPersistDocument? TryDeserialize(ReadOnlySpan<byte> utf8)
        => PrefabCatalogJson.TryDeserializeObject<MapPrefabPersistDocument>(utf8);

    public static MapPrefabPersistDocument? TryDeserializeFromString(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        return TryDeserialize(System.Text.Encoding.UTF8.GetBytes(json));
    }
}
