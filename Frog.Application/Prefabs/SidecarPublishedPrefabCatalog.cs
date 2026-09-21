using System.Text.Json;
using System.Text.Json.Serialization;
using Frog.Core.Protocol;

namespace Frog.Application.Prefabs;

/// <summary>
/// Catalogue prefab publié lu depuis un JSON additif (playtest : à côté du manifeste).
/// Livré au <see cref="PublishedCatalogWire"/> sans bump de protocole.
/// </summary>
public sealed class SidecarPublishedPrefabCatalog : IPublishedPrefabCatalog
{
    public const string FileName = "published-prefabs.json";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        WriteIndented = true,
    };

    private readonly string _path;

    public SidecarPublishedPrefabCatalog(string jsonPath)
    {
        _path = jsonPath ?? throw new ArgumentNullException(nameof(jsonPath));
    }

    public static string PathBesideManifest(string manifestPath)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(manifestPath);
        var dir = Path.GetDirectoryName(Path.GetFullPath(manifestPath));
        if (string.IsNullOrWhiteSpace(dir))
        {
            throw new InvalidOperationException("Répertoire manifeste playtest introuvable.");
        }

        return Path.Combine(dir, FileName);
    }

    public static void Write(string jsonPath, PublishedCatalogWire wire)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(jsonPath);
        var dir = Path.GetDirectoryName(jsonPath);
        if (!string.IsNullOrWhiteSpace(dir))
        {
            Directory.CreateDirectory(dir);
        }

        File.WriteAllText(jsonPath, JsonSerializer.Serialize(wire, JsonOptions));
    }

    public static void WriteFromDocument(
        string jsonPath,
        Guid mapId,
        string mapName,
        MapPrefabPersistDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var sprites = document.ToSpriteFiles();
        var wire = new PublishedCatalogWire
        {
            Prefabs = PublishedPrefabClientCoverage.ToWirePrefabs(document.Catalog, sprites),
            PrefabMaps = [PublishedPrefabClientCoverage.ToWireMap(mapId, mapName, document.Placements)],
        };
        Write(jsonPath, wire);
    }

    public static void WriteFromMaps(
        string jsonPath,
        IReadOnlyList<(Guid MapId, string MapName, MapPrefabPersistDocument Document)> maps)
    {
        var prefabs = new Dictionary<string, PublishedPrefabWireEntry>(StringComparer.Ordinal);
        var prefabMaps = new List<PublishedPrefabMapWireEntry>();
        foreach (var (mapId, mapName, document) in maps)
        {
            if (document is null)
            {
                continue;
            }

            var sprites = document.ToSpriteFiles();
            foreach (var entry in PublishedPrefabClientCoverage.ToWirePrefabs(document.Catalog, sprites))
            {
                prefabs[entry.Id] = MergePrefab(prefabs.GetValueOrDefault(entry.Id), entry);
            }

            prefabMaps.Add(PublishedPrefabClientCoverage.ToWireMap(mapId, mapName, document.Placements));
        }

        Write(jsonPath, new PublishedCatalogWire
        {
            Prefabs = prefabs.Values.OrderBy(p => p.Id, StringComparer.Ordinal).ToArray(),
            PrefabMaps = prefabMaps,
        });
    }

    public Task<PublishedPrefabCatalogBundle> LoadPublishedAsync(CancellationToken cancellationToken = default)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (!TryRead(out var catalog))
        {
            return Task.FromResult(new PublishedPrefabCatalogBundle());
        }

        return Task.FromResult(new PublishedPrefabCatalogBundle
        {
            Prefabs = catalog.Prefabs,
            PrefabMaps = catalog.PrefabMaps,
        });
    }

    public bool TryRead(out PublishedCatalogWire catalog)
    {
        catalog = new PublishedCatalogWire();
        if (!File.Exists(_path))
        {
            return false;
        }

        try
        {
            var parsed = JsonSerializer.Deserialize<PublishedCatalogWire>(File.ReadAllText(_path), JsonOptions);
            if (parsed is null)
            {
                return false;
            }

            catalog = parsed;
            return true;
        }
        catch
        {
            return false;
        }
    }

    private static PublishedPrefabWireEntry MergePrefab(PublishedPrefabWireEntry? existing, PublishedPrefabWireEntry incoming)
    {
        if (existing is null)
        {
            return incoming;
        }

        var variants = existing.Variants.ToList();
        var seen = new HashSet<string>(
            variants.Select(v => v.SpriteFileName),
            StringComparer.OrdinalIgnoreCase);
        foreach (var variant in incoming.Variants)
        {
            if (string.IsNullOrWhiteSpace(variant.SpriteFileName) || !seen.Add(variant.SpriteFileName))
            {
                continue;
            }

            variants.Add(variant);
        }

        return new PublishedPrefabWireEntry
        {
            Id = existing.Id,
            DisplayName = string.IsNullOrWhiteSpace(existing.DisplayName) ? incoming.DisplayName : existing.DisplayName,
            FootprintWidthTiles = existing.FootprintWidthTiles,
            FootprintHeightTiles = existing.FootprintHeightTiles,
            WidthPixels = existing.WidthPixels,
            HeightPixels = existing.HeightPixels,
            Variants = variants,
        };
    }
}
