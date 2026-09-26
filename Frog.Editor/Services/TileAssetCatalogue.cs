using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;

using Frog.Core.Constants;
using Frog.Core.Distribution;
using Frog.Core.Maps;
using Frog.Core.Models;

namespace Frog.Editor.Services;

/// <summary>
/// Catalogue global des <see cref="TileAsset"/> de l’éditeur. Les déblocages joueur
/// (<c>player.player_tile_unlocks</c>) ne filtrent pas cette liste : l’éditeur voit tout.
/// </summary>
public sealed class TileAssetCatalogue : ITileAssetLookup
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    private readonly Dictionary<TileAssetId, TileAsset> _assets = new();
    private readonly Dictionary<TileAssetId, byte[]> _straight = new();
    private readonly List<TileAssetId> _order = new();
    private readonly List<WorkingTileset> _working = new();
    private readonly TileAssetFlagTable _flags = new();

    public event Action? Changed;

    /// <summary>Dossier d’enregistrement local. Null : mémoire seulement.</summary>
    public string? StoreDirectory { get; set; }

    public int Count => _order.Count;

    public IReadOnlyList<TileAssetId> Ids => _order;

    public IReadOnlyList<WorkingTileset> WorkingTilesets => _working;

    public WorkingTileset? ActiveWorkingTileset { get; private set; }

    /// <summary>Drapeaux partagés (passage, priorité, buisson, comptoir, dégâts). La carte v6 peut pointer cette table.</summary>
    public TileAssetFlagTable Flags => _flags;

    public TileAssetFlags GetFlags(TileAssetId id) => _flags.Get(id);

    public static string DefaultStoreDirectory()
    {
        var root = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
        if (string.IsNullOrWhiteSpace(root))
        {
            root = AppContext.BaseDirectory;
        }

        return Path.Combine(root, "MmoMaker", "GameData", "tile-assets");
    }

    public bool TryGet(TileAssetId id, out TileAsset? asset)
    {
        if (_assets.TryGetValue(id, out var found))
        {
            asset = found;
            return true;
        }

        asset = null;
        return false;
    }

    public bool TryGetStraightRgba(TileAssetId id, out byte[]? rgba)
    {
        if (_straight.TryGetValue(id, out var found))
        {
            rgba = found;
            return true;
        }

        rgba = null;
        return false;
    }

    public MemoryTileAssetLookup ToMemoryLookup() => new(_assets.Values);

    public IReadOnlyList<TileAssetId> Search(string? query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return _order.ToArray();
        }

        var needle = query.Trim();
        return _order.Where(id => id.ToHex().Contains(needle, StringComparison.OrdinalIgnoreCase)).ToArray();
    }

    public TileSheetImportResult ImportStraightRgba(
        ReadOnlySpan<byte> straightRgba,
        int width,
        int height,
        TileImportOptions? options = null)
    {
        var resize = options?.ExplicitResize == true;
        var source = straightRgba;
        byte[]? owned = null;
        if (resize)
        {
            var cell = options!.SourceCellPixels;
            owned = TileSheetExplicitScaler.ScaleCellsNearest(straightRgba, width, height, cell, out width, out height);
            source = owned;
        }

        var slice = TileSheetSlicer.Slice(source, width, height);
        var added = 0;
        var already = 0;
        var seen = new HashSet<TileAssetId>();
        var uniqueIndex = 0;
        for (var i = 0; i < slice.CellIds.Count; i++)
        {
            var id = slice.CellIds[i];
            if (!seen.Add(id))
            {
                continue;
            }

            var asset = slice.UniqueAssets[uniqueIndex++];
            if (_assets.ContainsKey(id))
            {
                already++;
                continue;
            }

            var column = i % slice.Columns;
            var row = i / slice.Columns;
            var straight = ExtractCell(source, width, column, row, slice.TileWidth, slice.TileHeight);
            var check = TileAsset.FromStraightRgba(straight);
            if (check.Id != asset.Id)
            {
                throw new InvalidDataException("Cellule découpée incohérente avec le TileAssetId.");
            }

            _assets.Add(id, asset);
            _straight.Add(id, straight);
            _order.Add(id);
            added++;
        }

        PersistQuietly();
        Changed?.Invoke();
        return new TileSheetImportResult
        {
            Columns = slice.Columns,
            Rows = slice.Rows,
            UniqueAdded = added,
            UniqueAlreadyPresent = already,
            DiscardedRightPixels = slice.DiscardedRightPixels,
            DiscardedBottomPixels = slice.DiscardedBottomPixels,
            ExplicitResizeApplied = resize,
            CatalogueCount = Count,
        };
    }

    public TileSheetImportResult ImportPng(ReadOnlySpan<byte> png, TileImportOptions? options = null)
    {
        if (!TileAssetPngCodec.TryDecode(png, out var width, out var height, out var rgba))
        {
            throw new InvalidDataException("PNG illisible (RGBA ou RGB 8 bits, sans entrelacement, attendu).");
        }

        return ImportStraightRgba(rgba, width, height, options);
    }

    public void EnsureDefaultWorkingTileset()
    {
        if (_working.Count > 0)
        {
            ActiveWorkingTileset ??= _working[0];
            return;
        }

        var set = new WorkingTileset { Name = "Tileset de travail" };
        _working.Add(set);
        ActiveWorkingTileset = set;
    }

    public bool TryCreateWorkingTileset(string name, out string? error)
    {
        var set = new WorkingTileset { Name = name?.Trim() ?? string.Empty };
        if (!set.Validate(out error))
        {
            return false;
        }

        if (_working.Any(existing => string.Equals(existing.Name, set.Name, StringComparison.OrdinalIgnoreCase)))
        {
            error = "Un tileset de travail porte déjà ce nom.";
            return false;
        }

        _working.Add(set);
        ActiveWorkingTileset = set;
        PersistQuietly();
        Changed?.Invoke();
        error = null;
        return true;
    }

    public bool TrySelectWorkingTileset(string name)
    {
        var found = _working.FirstOrDefault(set => string.Equals(set.Name, name, StringComparison.Ordinal));
        if (found is null)
        {
            return false;
        }

        ActiveWorkingTileset = found;
        Changed?.Invoke();
        return true;
    }

    public bool TryRenameActiveWorkingTileset(string name, out string? error)
    {
        var set = ActiveWorkingTileset;
        if (set is null)
        {
            error = "Aucun tileset de travail.";
            return false;
        }

        var trimmed = name?.Trim() ?? string.Empty;
        if (_working.Any(existing => !ReferenceEquals(existing, set)
                                     && string.Equals(existing.Name, trimmed, StringComparison.OrdinalIgnoreCase)))
        {
            error = "Un tileset de travail porte déjà ce nom.";
            return false;
        }

        var previous = set.Name;
        set.Name = trimmed;
        if (!set.Validate(out error))
        {
            set.Name = previous;
            return false;
        }

        PersistQuietly();
        Changed?.Invoke();
        error = null;
        return true;
    }

    public bool TryAddToActiveWorkingTileset(TileAssetId id, out string? error)
    {
        if (id.IsNone || !_assets.ContainsKey(id))
        {
            error = "Cette tuile n’est pas dans le catalogue.";
            return false;
        }

        var set = ActiveWorkingTileset;
        if (set is null)
        {
            error = "Aucun tileset de travail.";
            return false;
        }

        if (set.Tiles.Count >= WorkingTileset.MaxTileCount)
        {
            error = $"Palette trop grande (> {WorkingTileset.MaxTileCount}).";
            return false;
        }

        set.Tiles.Add(id);
        PersistQuietly();
        Changed?.Invoke();
        error = null;
        return true;
    }

    public bool TryRemoveFromActiveWorkingTileset(int index, out string? error)
    {
        var set = ActiveWorkingTileset;
        if (set is null || (uint)index >= (uint)set.Tiles.Count)
        {
            error = "Tuile de palette introuvable.";
            return false;
        }

        set.Tiles.RemoveAt(index);
        PersistQuietly();
        Changed?.Invoke();
        error = null;
        return true;
    }

    public bool TryMoveInActiveWorkingTileset(int from, int to, out string? error)
    {
        var set = ActiveWorkingTileset;
        if (set is null || (uint)from >= (uint)set.Tiles.Count || (uint)to >= (uint)set.Tiles.Count)
        {
            error = "Déplacement hors de la palette.";
            return false;
        }

        if (from != to)
        {
            var id = set.Tiles[from];
            set.Tiles.RemoveAt(from);
            set.Tiles.Insert(to, id);
            PersistQuietly();
            Changed?.Invoke();
        }

        error = null;
        return true;
    }

    public bool TrySetFlags(TileAssetId id, TileAssetFlags flags, out string? error)
    {
        if (id.IsNone || !_assets.ContainsKey(id))
        {
            error = "Cette tuile n’est pas dans le catalogue.";
            return false;
        }

        if (flags.Priority > TileAssetFlags.MaxPriority)
        {
            error = $"La priorité est entre 0 et {TileAssetFlags.MaxPriority}.";
            return false;
        }

        if (flags.Terrain > TileAssetFlags.MaxTerrain)
        {
            error = $"Le numéro de terrain est entre 0 et {TileAssetFlags.MaxTerrain}.";
            return false;
        }

        if (_flags.Get(id).Equals(flags))
        {
            error = null;
            return true;
        }

        _flags.Set(id, flags);
        PersistFlagsQuietly();
        Changed?.Invoke();
        error = null;
        return true;
    }

    public void MergeFlags(TileAssetFlagTable incoming)
    {
        ArgumentNullException.ThrowIfNull(incoming);
        if (incoming.Count == 0)
        {
            return;
        }

        _flags.Merge(incoming);
        PersistFlagsQuietly();
        Changed?.Invoke();
    }

    public void WriteMapFlagSidecar(string mapFilePath, Map map)
    {
        ArgumentNullException.ThrowIfNull(map);
        _flags.Project(map).SaveSidecar(mapFilePath);
    }

    /// <summary>Importe le sidecar carte dans le catalogue. Absent : ne touche pas aux drapeaux déjà saisis.</summary>
    public bool TryImportMapFlagSidecar(string mapFilePath, out string? error)
    {
        try
        {
            var sidecar = TileAssetFlagTable.TryLoadSidecar(mapFilePath);
            if (sidecar is null)
            {
                error = null;
                return false;
            }

            MergeFlags(sidecar);
            error = null;
            return true;
        }
        catch (Exception ex) when (ex is IOException or InvalidDataException or UnauthorizedAccessException)
        {
            error = ex.Message;
            return false;
        }
    }

    public void SaveToDirectory(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        Directory.CreateDirectory(directory);
        var blobs = Path.Combine(directory, "blobs");
        Directory.CreateDirectory(blobs);
        foreach (var id in _order)
        {
            var png = TileAssetPngCodec.Encode(_straight[id], TileAssetMetrics.TargetTileSizePixels, TileAssetMetrics.TargetTileSizePixels);
            File.WriteAllBytes(Path.Combine(blobs, id.ToHex() + ".png"), png);
        }

        var catalogue = new CatalogueFile { Tiles = _order.Select(id => id.ToHex()).ToList() };
        File.WriteAllText(Path.Combine(directory, "catalogue.json"), JsonSerializer.Serialize(catalogue, JsonOptions));

        var working = new WorkingTilesetFile
        {
            Active = ActiveWorkingTileset?.Name,
            Tilesets = _working.Select(set => new WorkingTilesetDto
            {
                Name = set.Name,
                Tiles = set.Tiles.Select(id => id.ToHex()).ToList(),
            }).ToList(),
        };
        File.WriteAllText(Path.Combine(directory, "working-tilesets.json"), JsonSerializer.Serialize(working, JsonOptions));
        _flags.Save(directory);
    }

    public void LoadFromDirectory(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        if (!Directory.Exists(directory))
        {
            return;
        }

        var cataloguePath = Path.Combine(directory, "catalogue.json");
        if (!File.Exists(cataloguePath))
        {
            _flags.Clear();
            LoadWorkingOnly(directory);
            LoadFlags(directory);
            return;
        }

        var catalogue = JsonSerializer.Deserialize<CatalogueFile>(File.ReadAllText(cataloguePath), JsonOptions)
                        ?? throw new InvalidDataException("catalogue.json illisible.");
        _assets.Clear();
        _straight.Clear();
        _order.Clear();
        _working.Clear();
        _flags.Clear();
        ActiveWorkingTileset = null;
        var blobs = Path.Combine(directory, "blobs");
        foreach (var hex in catalogue.Tiles)
        {
            if (!TileAssetId.TryParse(hex, out var id) || id.IsNone)
            {
                throw new InvalidDataException("TileAssetId invalide dans catalogue.json.");
            }

            if (_assets.ContainsKey(id))
            {
                continue;
            }

            var path = Path.Combine(blobs, hex.ToLowerInvariant() + ".png");
            if (!File.Exists(path))
            {
                throw new FileNotFoundException("Blob TileAsset manquant.", path);
            }

            if (!TileAssetPngCodec.TryDecode(File.ReadAllBytes(path), out var width, out var height, out var rgba)
                || !TileAssetMetrics.IsCanonicalSize(width, height))
            {
                throw new InvalidDataException($"Blob PNG invalide : {path}");
            }

            var asset = TileAsset.FromStraightRgba(rgba);
            if (asset.Id != id)
            {
                throw new InvalidDataException($"Le PNG {hex} ne correspond pas à son TileAssetId.");
            }

            _assets.Add(id, asset);
            _straight.Add(id, rgba);
            _order.Add(id);
        }

        LoadWorkingOnly(directory);
        LoadFlags(directory);
        Changed?.Invoke();
    }

    private void LoadWorkingOnly(string directory)
    {
        var path = Path.Combine(directory, "working-tilesets.json");
        if (!File.Exists(path))
        {
            return;
        }

        var file = JsonSerializer.Deserialize<WorkingTilesetFile>(File.ReadAllText(path), JsonOptions)
                   ?? throw new InvalidDataException("working-tilesets.json illisible.");
        _working.Clear();
        foreach (var dto in file.Tilesets)
        {
            var set = new WorkingTileset { Name = dto.Name ?? string.Empty };
            foreach (var hex in dto.Tiles)
            {
                if (!TileAssetId.TryParse(hex, out var id) || id.IsNone)
                {
                    throw new InvalidDataException($"TileAssetId invalide dans « {set.Name} ».");
                }

                set.Tiles.Add(id);
            }

            if (!set.Validate(out var error))
            {
                throw new InvalidDataException(error ?? "Tileset de travail invalide.");
            }

            _working.Add(set);
        }

        ActiveWorkingTileset = _working.FirstOrDefault(set => string.Equals(set.Name, file.Active, StringComparison.Ordinal))
                                ?? _working.FirstOrDefault();
    }

    private void LoadFlags(string directory)
    {
        var loaded = TileAssetFlagTable.TryLoad(directory);
        if (loaded is null)
        {
            return;
        }

        _flags.Clear();
        _flags.Merge(loaded);
    }

    private void PersistFlagsQuietly()
    {
        if (string.IsNullOrWhiteSpace(StoreDirectory))
        {
            return;
        }

        try
        {
            _flags.Save(StoreDirectory);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private void PersistQuietly()
    {
        if (string.IsNullOrWhiteSpace(StoreDirectory))
        {
            return;
        }

        try
        {
            SaveToDirectory(StoreDirectory);
        }
        catch (IOException)
        {
            // Le catalogue mémoire reste la source tant que le disque n’est pas disponible.
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    private static byte[] ExtractCell(ReadOnlySpan<byte> source, int imageWidth, int column, int row, int tileWidth, int tileHeight)
    {
        var cell = new byte[tileWidth * tileHeight * TileAssetMetrics.BytesPerPixel];
        var rowBytes = tileWidth * TileAssetMetrics.BytesPerPixel;
        var originX = column * tileWidth;
        var originY = row * tileHeight;
        for (var y = 0; y < tileHeight; y++)
        {
            var src = (((originY + y) * imageWidth) + originX) * TileAssetMetrics.BytesPerPixel;
            source.Slice(src, rowBytes).CopyTo(cell.AsSpan(y * rowBytes, rowBytes));
        }

        return cell;
    }

    private sealed class CatalogueFile
    {
        public int Version { get; set; } = 1;

        public List<string> Tiles { get; set; } = new();
    }

    private sealed class WorkingTilesetFile
    {
        public int Version { get; set; } = 1;

        public string? Active { get; set; }

        public List<WorkingTilesetDto> Tilesets { get; set; } = new();
    }

    private sealed class WorkingTilesetDto
    {
        public string Name { get; set; } = string.Empty;

        public List<string> Tiles { get; set; } = new();
    }
}

public sealed class TileImportOptions
{
    /// <summary>Faux par défaut : la feuille est déjà en cellules 48×48. Le hash ne redimensionne pas.</summary>
    public bool ExplicitResize { get; init; }

    /// <summary>Utilisé seulement si <see cref="ExplicitResize"/> est vrai. 32 est une suggestion, pas un upscale silencieux.</summary>
    public int SourceCellPixels { get; init; } = WorldMetrics.DefaultTileSizePixels;
}

public sealed class TileSheetImportResult
{
    public int Columns { get; init; }

    public int Rows { get; init; }

    public int UniqueAdded { get; init; }

    public int UniqueAlreadyPresent { get; init; }

    public int DiscardedRightPixels { get; init; }

    public int DiscardedBottomPixels { get; init; }

    public bool ExplicitResizeApplied { get; init; }

    public int CatalogueCount { get; init; }
}
