using System.Text.Json;
using System.Text.Json.Serialization;
using Frog.Application.Maps;
using Frog.Core.Models;

namespace Frog.Application.Playtest;

/// <summary>
/// Sidecar fichier des apparitions, PNJ et objets posés dans l’éditeur.
/// Écrit sous <c>Maps/runtime-{id}.placed.json</c> (workspace playtest et dossier du client).
/// Le client le relit au chargement de la carte. Pas de bump Hello, pas de blob <c>.fmap</c>.
/// Les dialogues PostgreSQL et le combat ne sont pas simulés par ce fichier.
/// </summary>
public static class PlaytestPlacedEntityPackage
{
    public const int SchemaVersion = 1;
    public const string MapsFolderName = "Maps";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public static string RuntimeFileName(int runtimeMapId) => $"runtime-{runtimeMapId}.placed.json";

    public static void WriteForPlan(
        string rootDirectory,
        PlaytestLaunchPlan plan,
        IReadOnlyList<MapPlacedEntity>? entities)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        ArgumentNullException.ThrowIfNull(plan);
        var primary = plan.Maps.FirstOrDefault(m => m.CanonicalMapId == plan.PrimaryCanonicalMapId)
                      ?? plan.Maps.FirstOrDefault();
        if (primary is null)
        {
            return;
        }

        Write(rootDirectory, primary.RuntimeMapId, primary.CanonicalMapId, primary.Name, primary.Map, entities);
    }

    public static void Write(
        string rootDirectory,
        int runtimeMapId,
        Guid canonicalMapId,
        string? mapName,
        Map map,
        IReadOnlyList<MapPlacedEntity>? entities)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rootDirectory);
        ArgumentNullException.ThrowIfNull(map);
        if (runtimeMapId <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(runtimeMapId), "Identifiant runtime playtest invalide.");
        }

        var mapsDir = Path.Combine(rootDirectory, MapsFolderName);
        Directory.CreateDirectory(mapsDir);
        var document = new PlaytestPlacedEntityDocument
        {
            SchemaVersion = SchemaVersion,
            CanonicalMapId = canonicalMapId,
            RuntimeMapId = runtimeMapId,
            MapName = mapName ?? map.Name ?? string.Empty,
            Entities = ToEntries(map, entities),
        };
        var json = JsonSerializer.Serialize(document, JsonOptions);
        File.WriteAllText(Path.Combine(mapsDir, RuntimeFileName(runtimeMapId)), json);
    }

    public static IReadOnlyList<MapPlacedEntity> TryLoadForRuntimeMap(
        IEnumerable<string> searchDirectories,
        int runtimeMapId,
        Map map)
    {
        ArgumentNullException.ThrowIfNull(searchDirectories);
        ArgumentNullException.ThrowIfNull(map);
        if (runtimeMapId <= 0)
        {
            return Array.Empty<MapPlacedEntity>();
        }

        foreach (var root in searchDirectories)
        {
            if (string.IsNullOrWhiteSpace(root))
            {
                continue;
            }

            var path = Path.Combine(root, MapsFolderName, RuntimeFileName(runtimeMapId));
            if (!File.Exists(path))
            {
                continue;
            }

            var document = TryRead(path);
            if (document is null
                || document.SchemaVersion != SchemaVersion
                || document.RuntimeMapId != runtimeMapId)
            {
                continue;
            }

            return Materialize(map, document.Entities);
        }

        return Array.Empty<MapPlacedEntity>();
    }

    public static PlaytestPlacedEntityDocument? TryRead(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            var json = File.ReadAllText(path);
            return JsonSerializer.Deserialize<PlaytestPlacedEntityDocument>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static List<PlaytestPlacedEntityEntry> ToEntries(Map map, IReadOnlyList<MapPlacedEntity>? entities)
    {
        var kept = new List<MapPlacedEntity>();
        foreach (var entity in MapPlacedEntityEdit.Clone(entities))
        {
            if (!MapPlacedEntityEdit.TryValidate(entity, map, out _))
            {
                continue;
            }

            if (MapPlacedEntityEdit.FindAt(kept, entity.TileX, entity.TileY) is not null)
            {
                continue;
            }

            kept.Add(entity);
        }

        var entries = new List<PlaytestPlacedEntityEntry>(kept.Count);
        foreach (var entity in kept)
        {
            entries.Add(new PlaytestPlacedEntityEntry
            {
                Id = entity.Id,
                Kind = entity.Kind.ToString().ToLowerInvariant(),
                TileX = entity.TileX,
                TileY = entity.TileY,
                Name = entity.Name,
                Notes = entity.Notes,
                Facing = entity.Facing.ToString().ToLowerInvariant(),
                RespawnSeconds = entity.RespawnSeconds,
                Level = entity.Level,
            });
        }

        return entries;
    }

    private static IReadOnlyList<MapPlacedEntity> Materialize(Map map, IReadOnlyList<PlaytestPlacedEntityEntry>? entries)
    {
        var kept = new List<MapPlacedEntity>();
        if (entries is null)
        {
            return kept;
        }

        foreach (var entry in entries)
        {
            if (entry is null || entry.Id == Guid.Empty)
            {
                continue;
            }

            if (!Enum.TryParse<MapPlacedKind>(entry.Kind, ignoreCase: true, out var kind))
            {
                continue;
            }

            if (!Enum.TryParse<MapPlacedFacing>(entry.Facing, ignoreCase: true, out var facing))
            {
                facing = MapPlacedFacing.South;
            }

            var entity = new MapPlacedEntity
            {
                Id = entry.Id,
                Kind = kind,
                TileX = entry.TileX,
                TileY = entry.TileY,
                Name = entry.Name?.Trim() ?? string.Empty,
                Notes = entry.Notes ?? string.Empty,
                Facing = facing,
                RespawnSeconds = entry.RespawnSeconds,
                Level = entry.Level,
            };
            if (!MapPlacedEntityEdit.TryValidate(entity, map, out _))
            {
                continue;
            }

            if (MapPlacedEntityEdit.FindAt(kept, entity.TileX, entity.TileY) is not null)
            {
                continue;
            }

            kept.Add(entity);
        }

        return kept;
    }
}

public sealed class PlaytestPlacedEntityDocument
{
    public int SchemaVersion { get; set; } = PlaytestPlacedEntityPackage.SchemaVersion;

    public Guid CanonicalMapId { get; set; }

    public int RuntimeMapId { get; set; }

    public string MapName { get; set; } = string.Empty;

    public List<PlaytestPlacedEntityEntry> Entities { get; set; } = new();
}

public sealed class PlaytestPlacedEntityEntry
{
    public Guid Id { get; set; }

    public string Kind { get; set; } = "npc";

    public int TileX { get; set; }

    public int TileY { get; set; }

    public string Name { get; set; } = string.Empty;

    public string Notes { get; set; } = string.Empty;

    public string Facing { get; set; } = "south";

    public int RespawnSeconds { get; set; }

    public int Level { get; set; } = 1;
}
