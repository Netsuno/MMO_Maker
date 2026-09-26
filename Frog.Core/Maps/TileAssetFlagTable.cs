using System.Text.Json;
using System.Text.Json.Serialization;

using Frog.Core.Models;

namespace Frog.Core.Maps;

/// <summary>
/// Meta des drapeaux, indexée par <see cref="TileAssetId"/>.
/// Fichier <c>tile-flags.json</c> dans le catalogue, ou sidecar <c>{carte}.tileflags.json</c> à côté du <c>.fmap</c>.
/// Le blob carte (v5 / v6) ne change pas. Une tuile absente de la table a les drapeaux <see cref="TileAssetFlags.Default"/>.
/// </summary>
public sealed class TileAssetFlagTable
{
    public const string FileName = "tile-flags.json";
    public const string MapSidecarSuffix = ".tileflags.json";
    public const int FormatVersion = 1;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly Dictionary<TileAssetId, TileAssetFlags> _byId = new();

    public int Count => _byId.Count;

    public IReadOnlyCollection<TileAssetId> Ids => _byId.Keys;

    public TileAssetFlags Get(TileAssetId id)
        => id.IsNone || !_byId.TryGetValue(id, out var flags) ? TileAssetFlags.Default : flags;

    public bool TryGetExplicit(TileAssetId id, out TileAssetFlags flags)
        => _byId.TryGetValue(id, out flags);

    public void Set(TileAssetId id, TileAssetFlags flags)
    {
        if (id.IsNone)
        {
            throw new ArgumentException("TileAssetId.None n’a pas de drapeaux.", nameof(id));
        }

        if (flags.Priority > TileAssetFlags.MaxPriority)
        {
            throw new ArgumentOutOfRangeException(nameof(flags), "Priorité hors 0–5.");
        }

        if (flags.Terrain > TileAssetFlags.MaxTerrain)
        {
            throw new ArgumentOutOfRangeException(nameof(flags), "Numéro de terrain hors 0–7.");
        }

        if (flags.IsDefault)
        {
            _byId.Remove(id);
            return;
        }

        _byId[id] = flags;
    }

    public void Clear() => _byId.Clear();

    public void Merge(TileAssetFlagTable incoming)
    {
        ArgumentNullException.ThrowIfNull(incoming);
        foreach (var pair in incoming._byId)
        {
            Set(pair.Key, pair.Value);
        }
    }

    /// <summary>Ne garde que les drapeaux des <see cref="TileAssetId"/> réellement posés sur la carte.</summary>
    public TileAssetFlagTable Project(Map map)
    {
        ArgumentNullException.ThrowIfNull(map);
        var copy = new TileAssetFlagTable();
        foreach (var layer in map.Layers)
        {
            foreach (var tile in layer.Tiles)
            {
                if (tile.AssetId.IsNone || !_byId.TryGetValue(tile.AssetId, out var flags))
                {
                    continue;
                }

                copy.Set(tile.AssetId, flags);
            }
        }

        return copy;
    }

    public string ToJson()
    {
        var file = new FlagFile { Version = FormatVersion };
        foreach (var pair in _byId.OrderBy(pair => pair.Key.ToHex(), StringComparer.Ordinal))
        {
            var flags = pair.Value;
            file.Tiles[pair.Key.ToHex()] = new FlagDto
            {
                PassageNorth = flags.PassageNorth,
                PassageEast = flags.PassageEast,
                PassageSouth = flags.PassageSouth,
                PassageWest = flags.PassageWest,
                Priority = flags.Priority,
                Bush = flags.Bush,
                Counter = flags.Counter,
                Damage = flags.Damage,
                Star = flags.Star,
                Terrain = flags.Terrain,
            };
        }

        return JsonSerializer.Serialize(file, JsonOptions);
    }

    public static TileAssetFlagTable FromJson(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        FlagFile file;
        try
        {
            file = JsonSerializer.Deserialize<FlagFile>(json, JsonOptions)
                   ?? throw new InvalidDataException("tile-flags.json vide.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("tile-flags.json illisible.", ex);
        }

        var version = file.Version == 0 ? FormatVersion : file.Version;
        if (version != FormatVersion)
        {
            throw new InvalidDataException(
                $"tile-flags.json version {version} non supportée (attendu {FormatVersion}).");
        }

        var table = new TileAssetFlagTable();
        file.Tiles ??= new Dictionary<string, FlagDto>(StringComparer.OrdinalIgnoreCase);
        foreach (var pair in file.Tiles)
        {
            if (!TileAssetId.TryParse(pair.Key, out var id) || id.IsNone)
            {
                throw new InvalidDataException("TileAssetId invalide dans tile-flags.json.");
            }

            var dto = pair.Value ?? throw new InvalidDataException("Drapeaux manquants dans tile-flags.json.");
            table.Set(id, FromDto(dto));
        }

        return table;
    }

    public void Save(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        Directory.CreateDirectory(directory);
        File.WriteAllText(Path.Combine(directory, FileName), ToJson());
    }

    public void SaveSidecar(string mapFilePath)
    {
        var path = SidecarPath(mapFilePath);
        if (string.IsNullOrEmpty(path))
        {
            throw new ArgumentException("Chemin de carte invalide.", nameof(mapFilePath));
        }

        var directory = Path.GetDirectoryName(path);
        if (!string.IsNullOrEmpty(directory))
        {
            Directory.CreateDirectory(directory);
        }

        File.WriteAllText(path, ToJson());
    }

    /// <summary>Null si le fichier n’existe pas. Lève <see cref="InvalidDataException"/> s’il est illisible.</summary>
    public static TileAssetFlagTable? TryLoad(string? directory)
    {
        if (string.IsNullOrWhiteSpace(directory))
        {
            return null;
        }

        var path = Path.Combine(directory, FileName);
        if (!File.Exists(path))
        {
            return null;
        }

        return FromJson(File.ReadAllText(path));
    }

    public static TileAssetFlagTable? TryLoadSidecar(string? mapFilePath)
    {
        var path = SidecarPath(mapFilePath);
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return null;
        }

        return FromJson(File.ReadAllText(path));
    }

    /// <summary>Lecture tolérante : fichier absent ou illisible → null. Le jeu continue sans drapeaux.</summary>
    public static TileAssetFlagTable? TryLoadOrNull(string? directory)
    {
        try
        {
            return TryLoad(directory);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            return null;
        }
    }

    public static string SidecarPath(string? mapFilePath)
    {
        if (string.IsNullOrWhiteSpace(mapFilePath))
        {
            return string.Empty;
        }

        var dir = Path.GetDirectoryName(mapFilePath);
        var stem = Path.GetFileNameWithoutExtension(mapFilePath);
        if (string.IsNullOrEmpty(stem))
        {
            return string.Empty;
        }

        return string.IsNullOrEmpty(dir) ? stem + MapSidecarSuffix : Path.Combine(dir, stem + MapSidecarSuffix);
    }

    private static TileAssetFlags FromDto(FlagDto dto)
    {
        var priority = dto.Priority ?? 0;
        if ((uint)priority > TileAssetFlags.MaxPriority)
        {
            throw new InvalidDataException($"Priorité {priority} hors 0–{TileAssetFlags.MaxPriority}.");
        }

        var terrain = dto.Terrain ?? 0;
        if ((uint)terrain > TileAssetFlags.MaxTerrain)
        {
            throw new InvalidDataException($"Numéro de terrain {terrain} hors 0–{TileAssetFlags.MaxTerrain}.");
        }

        return new TileAssetFlags
        {
            PassageNorth = dto.PassageNorth ?? true,
            PassageEast = dto.PassageEast ?? true,
            PassageSouth = dto.PassageSouth ?? true,
            PassageWest = dto.PassageWest ?? true,
            Priority = (byte)priority,
            Bush = dto.Bush ?? false,
            Counter = dto.Counter ?? false,
            Damage = dto.Damage ?? false,
            Star = dto.Star ?? false,
            Terrain = (byte)terrain,
        };
    }

    private sealed class FlagFile
    {
        public int Version { get; set; } = FormatVersion;

        public Dictionary<string, FlagDto> Tiles { get; set; } = new(StringComparer.OrdinalIgnoreCase);
    }

    private sealed class FlagDto
    {
        public bool? PassageNorth { get; set; }

        public bool? PassageEast { get; set; }

        public bool? PassageSouth { get; set; }

        public bool? PassageWest { get; set; }

        public int? Priority { get; set; }

        public bool? Bush { get; set; }

        public bool? Counter { get; set; }

        public bool? Damage { get; set; }

        public bool? Star { get; set; }

        public int? Terrain { get; set; }
    }
}
