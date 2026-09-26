using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;

using Frog.Core.Models;

namespace Frog.Core.Maps;

/// <summary>
/// Une ligne de la table de rencontres, style RPG Maker VX.
/// Régions vides : la troupe peut apparaître sur toute la carte.
/// Sinon, seulement sur les cases dont le numéro de région est listé.
/// </summary>
public sealed class MapEncounterEntry
{
    public Guid MonsterId { get; init; }

    public int? AliasId { get; init; }

    public string Label { get; init; } = string.Empty;

    public int Weight { get; init; }

    public IReadOnlyList<byte> Regions { get; init; } = Array.Empty<byte>();
}

/// <summary>Saisie éditeur, avant validation.</summary>
public readonly record struct MapEncounterDraft
{
    public Guid MonsterId { get; init; }

    public int? AliasId { get; init; }

    public string? Label { get; init; }

    public int Weight { get; init; }

    /// <summary>Liste « 1, 3 ». Vide = toute la carte.</summary>
    public string? RegionsText { get; init; }
}

/// <summary>
/// Régions peintes (0–63) et table de rencontres d’une carte.
/// Absentes du blob <c>.fmap</c> (v5 et v6) : sidecar <c>{carte}.regions.json</c>.
/// Le protocole TCP ne change pas. Les ticks de rencontre (apparition selon le pas moyen)
/// ne sont pas exécutés : le client et le serveur peuvent seulement relire ce document.
/// </summary>
public sealed class MapRegionDocument
{
    public const int FormatVersion = 1;
    public const string SidecarSuffix = ".regions.json";
    public const byte MinRegionId = 0;
    public const byte MaxRegionId = 63;
    public const int DefaultEncounterSteps = 30;
    public const int MinEncounterSteps = 1;
    public const int MaxEncounterSteps = 999;
    public const int MinWeight = 1;
    public const int MaxWeight = 999;
    public const int MaxEncounters = 64;
    public const int MaxLabelLength = 120;
    public const int MaxDimension = 512;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly Dictionary<(int X, int Y), byte> _cells = new();
    private readonly List<MapEncounterEntry> _encounters = new();

    public int Width { get; private set; }

    public int Height { get; private set; }

    public int EncounterSteps { get; private set; } = DefaultEncounterSteps;

    public int Count => _cells.Count;

    public IReadOnlyList<MapEncounterEntry> Encounters => _encounters;

    public bool IsEmpty =>
        _cells.Count == 0
        && _encounters.Count == 0
        && EncounterSteps == DefaultEncounterSteps;

    public static MapRegionDocument Create(int width, int height)
    {
        if (width < 1 || height < 1 || width > MaxDimension || height > MaxDimension)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Dimensions de régions hors 1–512.");
        }

        return new MapRegionDocument { Width = width, Height = height };
    }

    public byte Get(int x, int y)
        => _cells.TryGetValue((x, y), out var id) ? id : (byte)0;

    public IReadOnlyList<(int X, int Y, byte Region)> Cells()
    {
        var list = new List<(int X, int Y, byte Region)>(_cells.Count);
        foreach (var pair in _cells)
        {
            list.Add((pair.Key.X, pair.Key.Y, pair.Value));
        }

        list.Sort(static (a, b) =>
        {
            var byY = a.Y.CompareTo(b.Y);
            return byY != 0 ? byY : a.X.CompareTo(b.X);
        });
        return list;
    }

    /// <summary>0 retire la case. Hors carte ou hors 0–63 : faux, sans mutation.</summary>
    public bool TrySet(int x, int y, byte regionId, out string? error)
    {
        if (regionId > MaxRegionId)
        {
            error = "Le numéro de région doit rester entre 0 et 63.";
            return false;
        }

        if ((uint)x >= (uint)Width || (uint)y >= (uint)Height)
        {
            error = "Case hors de la carte.";
            return false;
        }

        var key = (x, y);
        if (regionId == 0)
        {
            if (!_cells.Remove(key))
            {
                error = null;
                return false;
            }

            error = null;
            return true;
        }

        if (_cells.TryGetValue(key, out var current) && current == regionId)
        {
            error = null;
            return false;
        }

        _cells[key] = regionId;
        error = null;
        return true;
    }

    public bool TrySetEncounterSteps(int steps, out string? error)
    {
        if (steps < MinEncounterSteps || steps > MaxEncounterSteps)
        {
            error = $"Les pas moyens doivent rester entre {MinEncounterSteps} et {MaxEncounterSteps}.";
            return false;
        }

        if (EncounterSteps == steps)
        {
            error = null;
            return false;
        }

        EncounterSteps = steps;
        error = null;
        return true;
    }

    public bool TryAddEncounter(MapEncounterDraft draft, out string? error)
    {
        if (_encounters.Count >= MaxEncounters)
        {
            error = $"Trop de rencontres ({MaxEncounters} maximum).";
            return false;
        }

        if (!TryNormalize(draft, out var entry, out error))
        {
            return false;
        }

        _encounters.Add(entry);
        return true;
    }

    public bool TryReplaceEncounter(int index, MapEncounterDraft draft, out string? error)
    {
        if ((uint)index >= (uint)_encounters.Count)
        {
            error = "Index de rencontre invalide.";
            return false;
        }

        if (!TryNormalize(draft, out var entry, out error))
        {
            return false;
        }

        _encounters[index] = entry;
        return true;
    }

    public bool TryRemoveEncounter(int index, out string? error)
    {
        if ((uint)index >= (uint)_encounters.Count)
        {
            error = "Index de rencontre invalide.";
            return false;
        }

        _encounters.RemoveAt(index);
        error = null;
        return true;
    }

    /// <summary>
    /// Rencontres qui peuvent tomber sur cette case.
    /// Liste de régions vide : toute la carte, y compris le numéro 0.
    /// </summary>
    public IReadOnlyList<MapEncounterEntry> EncountersAt(int x, int y)
    {
        var region = Get(x, y);
        var list = new List<MapEncounterEntry>();
        foreach (var entry in _encounters)
        {
            if (entry.Regions.Count == 0 || Contains(entry.Regions, region))
            {
                list.Add(entry);
            }
        }

        return list;
    }

    /// <summary>Recadre sur la taille de carte. Les cases hors limites sont retirées.</summary>
    public void AdoptMapSize(int width, int height) => Shift(0, 0, width, height);

    /// <summary>Décale les cases puis recadre. dx/dy positifs poussent vers la droite et le bas.</summary>
    public void Shift(int deltaX, int deltaY, int width, int height)
    {
        if (width < 1 || height < 1 || width > MaxDimension || height > MaxDimension)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Dimensions de régions hors 1–512.");
        }

        var next = new Dictionary<(int X, int Y), byte>();
        foreach (var pair in _cells)
        {
            var x = (long)pair.Key.X + deltaX;
            var y = (long)pair.Key.Y + deltaY;
            if (x < 0 || y < 0 || x >= width || y >= height)
            {
                continue;
            }

            next[((int)x, (int)y)] = pair.Value;
        }

        _cells.Clear();
        foreach (var pair in next)
        {
            _cells[pair.Key] = pair.Value;
        }

        Width = width;
        Height = height;
    }

    public string ToJson()
    {
        var file = new FileDto
        {
            FormatVersion = FormatVersion,
            Width = Width,
            Height = Height,
            EncounterSteps = EncounterSteps == DefaultEncounterSteps ? null : EncounterSteps,
        };
        foreach (var cell in Cells())
        {
            file.Cells.Add(new CellDto { X = cell.X, Y = cell.Y, Region = cell.Region });
        }

        foreach (var entry in _encounters)
        {
            file.Encounters.Add(new EncounterDto
            {
                MonsterId = entry.MonsterId == Guid.Empty ? null : entry.MonsterId.ToString("D"),
                AliasId = entry.AliasId,
                Label = string.IsNullOrEmpty(entry.Label) ? null : entry.Label,
                Weight = entry.Weight,
                Regions = entry.Regions.Count == 0 ? null : entry.Regions.Select(id => (int)id).ToList(),
            });
        }

        return JsonSerializer.Serialize(file, JsonOptions);
    }

    public static MapRegionDocument FromJson(string json)
    {
        ArgumentNullException.ThrowIfNull(json);
        FileDto file;
        try
        {
            file = JsonSerializer.Deserialize<FileDto>(json, JsonOptions)
                   ?? throw new InvalidDataException("Fichier de régions vide.");
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Fichier de régions illisible.", ex);
        }

        var version = file.FormatVersion == 0 ? FormatVersion : file.FormatVersion;
        if (version != FormatVersion)
        {
            throw new InvalidDataException(
                $"Fichier de régions version {version} non supportée (attendu {FormatVersion}).");
        }

        MapRegionDocument document;
        try
        {
            document = Create(file.Width, file.Height);
        }
        catch (ArgumentOutOfRangeException ex)
        {
            throw new InvalidDataException("Dimensions de régions invalides.", ex);
        }

        var steps = file.EncounterSteps ?? DefaultEncounterSteps;
        if (!document.TrySetEncounterSteps(steps, out var stepsError) && stepsError is not null)
        {
            throw new InvalidDataException(stepsError);
        }

        file.Cells ??= new List<CellDto>();
        file.Encounters ??= new List<EncounterDto>();
        foreach (var cell in file.Cells)
        {
            if (cell is null)
            {
                throw new InvalidDataException("Cellule de région manquante.");
            }

            if (cell.Region is < MinRegionId or > MaxRegionId)
            {
                throw new InvalidDataException($"Numéro de région {cell.Region} hors 0–{MaxRegionId}.");
            }

            if (cell.Region == 0)
            {
                throw new InvalidDataException("Une case de région 0 ne se stocke pas.");
            }

            if ((uint)cell.X >= (uint)document.Width || (uint)cell.Y >= (uint)document.Height)
            {
                throw new InvalidDataException($"Cellule de région hors carte ({cell.X}, {cell.Y}).");
            }

            if (document._cells.ContainsKey((cell.X, cell.Y)))
            {
                throw new InvalidDataException($"Cellule de région en double ({cell.X}, {cell.Y}).");
            }

            document._cells[(cell.X, cell.Y)] = (byte)cell.Region;
        }

        foreach (var dto in file.Encounters)
        {
            if (dto is null)
            {
                throw new InvalidDataException("Rencontre manquante.");
            }

            if (!Guid.TryParse(dto.MonsterId, out var monsterId))
            {
                monsterId = Guid.Empty;
            }
            else if (monsterId == Guid.Empty && !string.IsNullOrWhiteSpace(dto.MonsterId))
            {
                throw new InvalidDataException("Identifiant de monstre invalide.");
            }

            if (!string.IsNullOrWhiteSpace(dto.MonsterId) && monsterId == Guid.Empty)
            {
                throw new InvalidDataException("Identifiant de monstre invalide.");
            }

            var regions = dto.Regions is null
                ? string.Empty
                : string.Join(", ", dto.Regions);
            var draft = new MapEncounterDraft
            {
                MonsterId = monsterId,
                AliasId = dto.AliasId,
                Label = dto.Label,
                Weight = dto.Weight,
                RegionsText = regions,
            };
            if (!document.TryAddEncounter(draft, out var error))
            {
                throw new InvalidDataException(error ?? "Rencontre illisible.");
            }
        }

        return document;
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

    /// <summary>
    /// Écrit le sidecar s’il y a des régions, des rencontres ou un pas moyen modifié.
    /// Document vide : retire un sidecar déjà présent. Document absent : ne touche pas au fichier.
    /// </summary>
    public static void WriteForMap(string mapFilePath, Map map)
    {
        ArgumentNullException.ThrowIfNull(map);
        var path = SidecarPath(mapFilePath);
        if (string.IsNullOrEmpty(path))
        {
            throw new ArgumentException("Chemin de carte invalide.", nameof(mapFilePath));
        }

        if (map.Regions is null)
        {
            return;
        }

        if (map.Regions.IsEmpty)
        {
            if (File.Exists(path))
            {
                File.Delete(path);
            }

            return;
        }

        map.Regions.AdoptMapSize(map.Width, map.Height);
        map.Regions.SaveSidecar(mapFilePath);
    }

    /// <summary>Null si le fichier n’existe pas. Lève <see cref="InvalidDataException"/> s’il est illisible.</summary>
    public static MapRegionDocument? TryLoadSidecar(string? mapFilePath)
    {
        var path = SidecarPath(mapFilePath);
        if (string.IsNullOrEmpty(path) || !File.Exists(path))
        {
            return null;
        }

        return FromJson(File.ReadAllText(path));
    }

    /// <summary>
    /// Pose le sidecar sur la carte s’il existe. Fichier absent : vrai, régions inchangées.
    /// Fichier illisible : faux, régions inchangées.
    /// </summary>
    public static bool TryAttach(Map map, string? mapFilePath, out string? error)
    {
        ArgumentNullException.ThrowIfNull(map);
        error = null;
        if (map.Regions is not null || string.IsNullOrWhiteSpace(mapFilePath))
        {
            return true;
        }

        try
        {
            var document = TryLoadSidecar(mapFilePath);
            if (document is null)
            {
                return true;
            }

            document.AdoptMapSize(map.Width, map.Height);
            map.Regions = document;
            return true;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            error = ex.Message;
            return false;
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

        return string.IsNullOrEmpty(dir) ? stem + SidecarSuffix : Path.Combine(dir, stem + SidecarSuffix);
    }

    public static bool TryParseRegionSet(string? text, out byte[] regions, out string? error)
    {
        regions = Array.Empty<byte>();
        if (string.IsNullOrWhiteSpace(text))
        {
            error = null;
            return true;
        }

        var parts = text.Split(new[] { ',', ';', ' ', '\t' }, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0)
        {
            error = null;
            return true;
        }

        if (parts.Length > MaxRegionId)
        {
            error = "Trop de numéros de région.";
            return false;
        }

        var set = new List<byte>(parts.Length);
        foreach (var part in parts)
        {
            if (!int.TryParse(part, NumberStyles.None, CultureInfo.InvariantCulture, out var value)
                || value < 1
                || value > MaxRegionId)
            {
                error = "Région de rencontre hors 1–63.";
                return false;
            }

            var id = (byte)value;
            if (set.Contains(id))
            {
                error = "Région de rencontre en double.";
                return false;
            }

            set.Add(id);
        }

        set.Sort();
        regions = set.ToArray();
        error = null;
        return true;
    }

    private static bool TryNormalize(MapEncounterDraft draft, out MapEncounterEntry entry, out string? error)
    {
        entry = new MapEncounterEntry();
        var label = (draft.Label ?? string.Empty).Trim();
        if (label.Length > MaxLabelLength)
        {
            error = $"Nom de rencontre trop long ({MaxLabelLength} caractères maximum).";
            return false;
        }

        if (draft.Weight < MinWeight || draft.Weight > MaxWeight)
        {
            error = $"Le poids doit rester entre {MinWeight} et {MaxWeight}.";
            return false;
        }

        if (draft.AliasId is <= 0)
        {
            error = "Alias de troupe invalide.";
            return false;
        }

        if (!TryParseRegionSet(draft.RegionsText, out var regions, out error))
        {
            return false;
        }

        var hasMonster = draft.MonsterId != Guid.Empty;
        var hasAlias = draft.AliasId is > 0;
        if (!hasMonster && !hasAlias && label.Length == 0)
        {
            error = "Indiquez un monstre, un alias ou un nom.";
            return false;
        }

        entry = new MapEncounterEntry
        {
            MonsterId = draft.MonsterId,
            AliasId = hasAlias ? draft.AliasId : null,
            Label = label,
            Weight = draft.Weight,
            Regions = regions,
        };
        error = null;
        return true;
    }

    private static bool Contains(IReadOnlyList<byte> regions, byte region)
    {
        foreach (var id in regions)
        {
            if (id == region)
            {
                return true;
            }
        }

        return false;
    }

    private sealed class FileDto
    {
        public int FormatVersion { get; set; } = MapRegionDocument.FormatVersion;

        public int Width { get; set; }

        public int Height { get; set; }

        public int? EncounterSteps { get; set; }

        public List<CellDto> Cells { get; set; } = new();

        public List<EncounterDto> Encounters { get; set; } = new();
    }

    private sealed class CellDto
    {
        public int X { get; set; }

        public int Y { get; set; }

        public int Region { get; set; }
    }

    private sealed class EncounterDto
    {
        public string? MonsterId { get; set; }

        public int? AliasId { get; set; }

        public string? Label { get; set; }

        public int Weight { get; set; }

        public List<int>? Regions { get; set; }
    }
}
