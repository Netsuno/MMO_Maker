using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Frog.Core.Maps;

/// <summary>
/// Une ligne de la zone : un monstre déjà défini (<c>NpcKind.Monster</c>),
/// un nombre maximum de vivants, et le délai avant de le remplacer.
/// </summary>
public sealed class MobSpawnEntry
{
    public Guid Id { get; set; }

    public Guid MonsterId { get; set; }

    public string Label { get; set; } = string.Empty;

    public int Quantity { get; set; }

    public int RespawnSeconds { get; set; }
}

/// <summary>Rectangle de tuiles. Les monstres n’apparaissent qu’à l’intérieur.</summary>
public sealed class MobSpawnZone
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int TileX { get; set; }

    public int TileY { get; set; }

    public int Width { get; set; }

    public int Height { get; set; }

    public List<MobSpawnEntry> Entries { get; set; } = new();
}

/// <summary>
/// Zones de réapparition d’une carte. Hors blob <c>.fmap</c> et hors Hello :
/// colonne JSON de la carte (brouillon et snapshot publié), comme les autres contenus de carte.
/// </summary>
public sealed class MobSpawnZoneDocument
{
    public const int FormatVersion = 1;
    public const int MaxZones = 32;
    public const int MaxEntries = 16;
    public const int MaxNameLength = 80;
    public const int MaxLabelLength = 80;
    public const int MinQuantity = 1;
    public const int MaxQuantity = 99;
    public const int MaxRespawnSeconds = 86_400;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly List<MobSpawnZone> _zones = new();

    public IReadOnlyList<MobSpawnZone> Zones => _zones;

    public bool IsEmpty => _zones.Count == 0;

    public static MobSpawnZoneDocument Empty() => new();

    public void ReplaceWith(MobSpawnZoneDocument? source)
    {
        _zones.Clear();
        if (source is null)
        {
            return;
        }

        foreach (var zone in source.Zones)
        {
            _zones.Add(CloneZone(zone));
        }
    }

    public MobSpawnZoneDocument Clone()
    {
        var copy = new MobSpawnZoneDocument();
        foreach (var zone in _zones)
        {
            copy._zones.Add(CloneZone(zone));
        }

        return copy;
    }

    public MobSpawnZone? Find(Guid id)
    {
        foreach (var zone in _zones)
        {
            if (zone.Id == id)
            {
                return zone;
            }
        }

        return null;
    }

    public string ToJson()
    {
        var file = new FileDto { FormatVersion = FormatVersion };
        foreach (var zone in _zones)
        {
            var dto = new ZoneDto
            {
                Id = zone.Id.ToString("D"),
                Name = zone.Name,
                TileX = zone.TileX,
                TileY = zone.TileY,
                Width = zone.Width,
                Height = zone.Height,
            };
            foreach (var entry in zone.Entries)
            {
                dto.Entries.Add(new EntryDto
                {
                    Id = entry.Id.ToString("D"),
                    MonsterId = entry.MonsterId.ToString("D"),
                    Label = string.IsNullOrEmpty(entry.Label) ? null : entry.Label,
                    Quantity = entry.Quantity,
                    RespawnSeconds = entry.RespawnSeconds,
                });
            }

            file.Zones.Add(dto);
        }

        return JsonSerializer.Serialize(file, JsonOptions);
    }

    public static MobSpawnZoneDocument FromJson(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        FileDto file;
        try
        {
            file = JsonSerializer.Deserialize<FileDto>(json, JsonOptions)
                   ?? throw new InvalidDataException("Zones de monstres vides.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Zones de monstres illisibles.", ex);
        }

        var version = file.FormatVersion == 0 ? FormatVersion : file.FormatVersion;
        if (version != FormatVersion)
        {
            throw new InvalidDataException(
                $"Zones de monstres version {version} non supportée (attendu {FormatVersion}).");
        }

        var document = new MobSpawnZoneDocument();
        file.Zones ??= new List<ZoneDto>();
        if (file.Zones.Count > MaxZones)
        {
            throw new InvalidDataException($"Trop de zones ({MaxZones} maximum).");
        }

        foreach (var dto in file.Zones)
        {
            if (dto is null || !Guid.TryParse(dto.Id, out var id) || id == Guid.Empty)
            {
                throw new InvalidDataException("Identifiant de zone invalide.");
            }

            if (document.Find(id) is not null)
            {
                throw new InvalidDataException("Zone en double.");
            }

            var zone = new MobSpawnZone
            {
                Id = id,
                Name = (dto.Name ?? string.Empty).Trim(),
                TileX = dto.TileX,
                TileY = dto.TileY,
                Width = dto.Width,
                Height = dto.Height,
            };
            dto.Entries ??= new List<EntryDto>();
            if (dto.Entries.Count > MaxEntries)
            {
                throw new InvalidDataException($"Trop de monstres dans « {zone.Name} ».");
            }

            foreach (var entryDto in dto.Entries)
            {
                if (entryDto is null || !Guid.TryParse(entryDto.Id, out var entryId) || entryId == Guid.Empty)
                {
                    throw new InvalidDataException("Identifiant de monstre de zone invalide.");
                }

                if (!Guid.TryParse(entryDto.MonsterId, out var monsterId) || monsterId == Guid.Empty)
                {
                    throw new InvalidDataException("Monstre de zone invalide.");
                }

                zone.Entries.Add(new MobSpawnEntry
                {
                    Id = entryId,
                    MonsterId = monsterId,
                    Label = (entryDto.Label ?? string.Empty).Trim(),
                    Quantity = entryDto.Quantity,
                    RespawnSeconds = entryDto.RespawnSeconds,
                });
            }

            document._zones.Add(zone);
        }

        return document;
    }

    public static bool TryFromJson(string? json, out MobSpawnZoneDocument document)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            document = Empty();
            return true;
        }

        try
        {
            document = FromJson(json);
            return true;
        }
        catch (InvalidDataException)
        {
            document = Empty();
            return false;
        }
    }

    internal void Add(MobSpawnZone zone) => _zones.Add(zone);

    internal bool Remove(Guid id)
    {
        for (var i = 0; i < _zones.Count; i++)
        {
            if (_zones[i].Id != id)
            {
                continue;
            }

            _zones.RemoveAt(i);
            return true;
        }

        return false;
    }

    private static MobSpawnZone CloneZone(MobSpawnZone zone)
    {
        var copy = new MobSpawnZone
        {
            Id = zone.Id,
            Name = zone.Name,
            TileX = zone.TileX,
            TileY = zone.TileY,
            Width = zone.Width,
            Height = zone.Height,
        };
        foreach (var entry in zone.Entries)
        {
            copy.Entries.Add(new MobSpawnEntry
            {
                Id = entry.Id,
                MonsterId = entry.MonsterId,
                Label = entry.Label,
                Quantity = entry.Quantity,
                RespawnSeconds = entry.RespawnSeconds,
            });
        }

        return copy;
    }

    private sealed class FileDto
    {
        public int FormatVersion { get; set; }

        public List<ZoneDto> Zones { get; set; } = new();
    }

    private sealed class ZoneDto
    {
        public string? Id { get; set; }

        public string? Name { get; set; }

        public int TileX { get; set; }

        public int TileY { get; set; }

        public int Width { get; set; }

        public int Height { get; set; }

        public List<EntryDto> Entries { get; set; } = new();
    }

    private sealed class EntryDto
    {
        public string? Id { get; set; }

        public string? MonsterId { get; set; }

        public string? Label { get; set; }

        public int Quantity { get; set; }

        public int RespawnSeconds { get; set; }
    }
}

/// <summary>Libellés français de l’outil Zone. L’éditeur les reprend tels quels.</summary>
public static class MobSpawnZoneLabels
{
    public const string ToolName = "Zone";
    public const string PanelTitle = "Zone de monstres";
    public const string Hint = "Tracez le rectangle. Glisser gauche déplace, glisser droit copie. Les monstres n’apparaissent que dans la zone.";
    public const string Name = "Nom";
    public const string Zones = "Zones";
    public const string Monsters = "Monstres";
    public const string Monster = "Monstre";
    public const string FreeEntry = "Saisie libre";
    public const string Identifier = "Identifiant";
    public const string Quantity = "Quantité";
    public const string Respawn = "Réapparition (s)";
    public const string Add = "Ajouter";
    public const string Remove = "Retirer";
    public const string DeleteZone = "Supprimer la zone";
    public const string RefreshCatalog = "Actualiser";
    public const string NoCatalog = "Aucun monstre publié — saisissez un identifiant.";
    public const string CatalogReady = "Monstres du catalogue. La saisie libre reste possible.";
    public const string EmptySelection = "Aucune zone. Tracez un rectangle sur la carte.";
    public const string EditMenu = "Outil zone de monstres";

    public static string FormatStatus(string? zoneName)
    {
        var who = string.IsNullOrWhiteSpace(zoneName) ? "aucune sélection" : $"« {zoneName.Trim()} »";
        return $"Zone (Z) · {who} · tracez un rectangle · glisser gauche déplace · glisser droit copie";
    }

    public static string FormatEntry(MobSpawnEntry entry)
    {
        ArgumentNullException.ThrowIfNull(entry);
        var who = string.IsNullOrWhiteSpace(entry.Label)
            ? entry.MonsterId.ToString("D")[..8]
            : entry.Label.Trim();
        return string.Create(
            CultureInfo.InvariantCulture,
            $"{who} · quantité {entry.Quantity} · {entry.RespawnSeconds} s");
    }
}
