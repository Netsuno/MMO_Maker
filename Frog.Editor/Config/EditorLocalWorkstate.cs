using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using Frog.Application.Maps;
using Frog.Application.Prefabs;
using Frog.Core.Models;

namespace Frog.Editor.Config;

/// <summary>Mémo locale éditeur (à côté de l’exécutable ; non sensible).</summary>
public static class EditorLocalWorkstate
{
    private sealed class PersistedDto
    {
        public int LastPublishedFrogMapId { get; set; } = 1;

        /// <summary>Chemin absolu vers <c>Frog.Client.exe</c> si la détection automatique a échoué une première fois.</summary>
        public string? ClientExePath { get; set; }

        /// <summary>Chemin absolu vers <c>Frog.Server.exe</c> ou <c>Frog.Server.dll</c>.</summary>
        public string? ServerExePath { get; set; }

        /// <summary>Largeur colonne gauche WPF (GridLength Absolute en DIPs), 0 = défaut.</summary>
        public double ShellLeftColumnWidth { get; set; }

        /// <summary>Largeur colonne droite WPF, 0 = défaut.</summary>
        public double ShellRightColumnWidth { get; set; }

        /// <summary>
        /// Spawn playtest / départ par carte. Clé <c>id:{guidN}</c> ou <c>local:{nom}|WxH</c>.
        /// Mémo éditeur uniquement — pas de bump <c>.fmap</c> ni protocole fil.
        /// </summary>
        public Dictionary<string, MapPlaytestSpawnDto>? MapPlaytestSpawns { get; set; }

        /// <summary>Placements prefab par carte (même clé que le spawn). Additif, hors <c>.fmap</c>.</summary>
        public Dictionary<string, MapPrefabPlacementsDto>? MapPrefabPlacements { get; set; }

        /// <summary>
        /// Apparitions, PNJ et objets posés (même clé que le spawn). Mémo éditeur, hors SQL et hors <c>.fmap</c>.
        /// </summary>
        public Dictionary<string, MapPlacedEntitiesDto>? MapPlacedEntities { get; set; }

        public string? LastPrefabId { get; set; }

        public PrefabFacing LastPrefabFacing { get; set; } = PrefabFacing.South;
    }

    public sealed class MapPlaytestSpawnDto
    {
        public int TileX { get; set; }
        public int TileY { get; set; }
    }

    public sealed class MapPrefabPlacementsDto
    {
        public List<PrefabPlacement> Placements { get; set; } = new();
    }

    public sealed class MapPlacedEntitiesDto
    {
        public List<MapPlacedEntity> Entities { get; set; } = new();
    }

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    /// <summary>Tests : redirige le JSON workstate vers un fichier temporaire.</summary>
    public static string? OverrideFilePathForTest { get; set; }

    private static string FilePath =>
        !string.IsNullOrWhiteSpace(OverrideFilePathForTest)
            ? OverrideFilePathForTest
            : Path.Combine(AppContext.BaseDirectory, "editor-workstate.json");

    private static PersistedDto LoadOrDefault()
    {
        try
        {
            if (!File.Exists(FilePath))
            {
                return new PersistedDto();
            }

            var json = File.ReadAllText(FilePath);
            var dto = JsonSerializer.Deserialize<PersistedDto>(json, SerializerOptions);
            return dto ?? new PersistedDto();
        }
        catch
        {
            return new PersistedDto();
        }
    }

    private static void Save(PersistedDto dto)
    {
        try
        {
            File.WriteAllText(FilePath, JsonSerializer.Serialize(dto, SerializerOptions));
        }
        catch
        {
            // optionnel pour l’UX ; échec ignoré
        }
    }

    public static int ReadLastPublishedFrogMapId()
    {
        var id = LoadOrDefault().LastPublishedFrogMapId;
        return id >= 1 ? id : 1;
    }

    public static void WriteLastPublishedFrogMapId(int frogMapId)
    {
        if (frogMapId < 1)
        {
            return;
        }

        var dto = LoadOrDefault();
        dto.LastPublishedFrogMapId = frogMapId;
        Save(dto);
    }

    public static bool TryReadClientExePath(out string fullPath)
    {
        fullPath = string.Empty;
        var p = LoadOrDefault().ClientExePath?.Trim();
        if (string.IsNullOrEmpty(p) || !File.Exists(p))
        {
            return false;
        }

        fullPath = p;
        return true;
    }

    public static void WriteClientExePath(string absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath))
        {
            return;
        }

        var dto = LoadOrDefault();
        dto.ClientExePath = Path.GetFullPath(absolutePath);
        Save(dto);
    }

    public static bool TryReadServerExePath(out string fullPath)
    {
        fullPath = string.Empty;
        var p = LoadOrDefault().ServerExePath?.Trim();
        if (string.IsNullOrEmpty(p) || !File.Exists(p))
        {
            return false;
        }

        fullPath = p;
        return true;
    }

    public static void WriteServerExePath(string absolutePath)
    {
        if (string.IsNullOrWhiteSpace(absolutePath))
        {
            return;
        }

        var dto = LoadOrDefault();
        dto.ServerExePath = Path.GetFullPath(absolutePath);
        Save(dto);
    }

    public static bool TryReadShellColumnWidths(out double left, out double right)
    {
        var dto = LoadOrDefault();
        left = dto.ShellLeftColumnWidth;
        right = dto.ShellRightColumnWidth;
        return left >= 180 && right >= 200;
    }

    public static void WriteShellColumnWidths(double left, double right)
    {
        if (left < 180 || right < 200)
        {
            return;
        }

        var dto = LoadOrDefault();
        dto.ShellLeftColumnWidth = left;
        dto.ShellRightColumnWidth = right;
        Save(dto);
    }

    public static bool TryReadMapPlaytestSpawn(string key, out int tileX, out int tileY)
    {
        tileX = 0;
        tileY = 0;
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        var map = LoadOrDefault().MapPlaytestSpawns;
        if (map is null || !map.TryGetValue(key, out var entry) || entry is null)
        {
            return false;
        }

        tileX = entry.TileX;
        tileY = entry.TileY;
        return true;
    }

    public static void WriteMapPlaytestSpawn(string key, int tileX, int tileY)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        var dto = LoadOrDefault();
        dto.MapPlaytestSpawns ??= new Dictionary<string, MapPlaytestSpawnDto>(StringComparer.Ordinal);
        dto.MapPlaytestSpawns[key] = new MapPlaytestSpawnDto { TileX = tileX, TileY = tileY };
        Save(dto);
    }

    public static bool TryReadMapPrefabPlacements(string key, out List<PrefabPlacement> placements)
    {
        placements = new List<PrefabPlacement>();
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        var map = LoadOrDefault().MapPrefabPlacements;
        if (map is null || !map.TryGetValue(key, out var entry) || entry?.Placements is null)
        {
            return false;
        }

        placements = PrefabPlacementService.ClonePlacements(entry.Placements);
        return true;
    }

    public static void WriteMapPrefabPlacements(string key, IReadOnlyList<PrefabPlacement> placements)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        var dto = LoadOrDefault();
        dto.MapPrefabPlacements ??= new Dictionary<string, MapPrefabPlacementsDto>(StringComparer.Ordinal);
        dto.MapPrefabPlacements[key] = new MapPrefabPlacementsDto
        {
            Placements = PrefabPlacementService.ClonePlacements(placements),
        };
        Save(dto);
    }

    public static bool TryReadMapPlacedEntities(string key, out List<MapPlacedEntity> entities)
    {
        entities = new List<MapPlacedEntity>();
        if (string.IsNullOrWhiteSpace(key))
        {
            return false;
        }

        var map = LoadOrDefault().MapPlacedEntities;
        if (map is null || !map.TryGetValue(key, out var entry) || entry?.Entities is null)
        {
            return false;
        }

        entities = MapPlacedEntityEdit.Clone(entry.Entities);
        return true;
    }

    public static void WriteMapPlacedEntities(string key, IReadOnlyList<MapPlacedEntity> entities)
    {
        if (string.IsNullOrWhiteSpace(key))
        {
            return;
        }

        var dto = LoadOrDefault();
        dto.MapPlacedEntities ??= new Dictionary<string, MapPlacedEntitiesDto>(StringComparer.Ordinal);
        dto.MapPlacedEntities[key] = new MapPlacedEntitiesDto
        {
            Entities = MapPlacedEntityEdit.Clone(entities),
        };
        Save(dto);
    }

    public static void TryReadLastPrefabSelection(out string? prefabId, out PrefabFacing facing)
    {
        var dto = LoadOrDefault();
        prefabId = dto.LastPrefabId;
        facing = dto.LastPrefabFacing;
    }

    public static void WriteLastPrefabSelection(string prefabId, PrefabFacing facing)
    {
        if (string.IsNullOrWhiteSpace(prefabId))
        {
            return;
        }

        var dto = LoadOrDefault();
        dto.LastPrefabId = prefabId;
        dto.LastPrefabFacing = facing;
        Save(dto);
    }
}
