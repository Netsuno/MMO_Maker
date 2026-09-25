using System.Text.Json;
using System.Text.Json.Serialization;
using Frog.Application.Prefabs;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Maps;
using Frog.Core.Models;

namespace Frog.Application.Maps;

/// <summary>
/// Modèle de carte réutilisable : rectangle de tuiles (toutes les couches) et prefabs
/// dont l’origine tombe dans le rectangle. Une carte TileAsset stocke des
/// <see cref="TileAssetId"/>, jamais la position dans le tileset de travail.
/// </summary>
public sealed class MapStampTemplate
{
    public string Id { get; set; } = string.Empty;

    public string Name { get; set; } = string.Empty;

    public int Width { get; set; }

    public int Height { get; set; }

    public int TileSizePixels { get; set; }

    public TileGraphicIdentity GraphicIdentity { get; set; }

    public List<MapStampTemplateLayer> Layers { get; set; } = new();

    public List<MapStampTemplatePrefab> Prefabs { get; set; } = new();
}

public sealed class MapStampTemplateLayer
{
    public int LayerIndex { get; set; }

    public LayerType LayerType { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public List<MapStampTemplateTile> Tiles { get; set; } = new();
}

public sealed class MapStampTemplateTile
{
    public int X { get; set; }

    public int Y { get; set; }

    public TileType Type { get; set; }

    /// <summary>64 hex, ou vide si la tuile n’a pas de graphique TileAsset.</summary>
    public string AssetId { get; set; } = string.Empty;

    public int TilesetId { get; set; }

    public int SrcX { get; set; }

    public int SrcY { get; set; }

    public string? ScriptId { get; set; }

    public Guid WarpTargetMapId { get; set; }

    public int WarpTargetX { get; set; }

    public int WarpTargetY { get; set; }

    public bool Block { get; set; }

    public bool HasWarpAttribute { get; set; }

    public Guid WarpAttributeMapId { get; set; }

    public int WarpAttributeX { get; set; }

    public int WarpAttributeY { get; set; }

    public bool HasResourceAttribute { get; set; }

    public int ResourceId { get; set; }
}

public sealed class MapStampTemplatePrefab
{
    public string PrefabId { get; set; } = string.Empty;

    public PrefabFacing Facing { get; set; }

    public int TileX { get; set; }

    public int TileY { get; set; }
}

public readonly record struct MapStampTileApplyResult(int Painted, int Cleared, int LayersSkipped)
{
    public bool Changed => Painted > 0 || Cleared > 0;
}

/// <summary>Fichier <c>map-templates.json</c> (camelCase, UTF-8).</summary>
public sealed class MapStampTemplateLibrary
{
    public const int DocumentVersion = 1;
    public const string FileName = "map-templates.json";
    public const int MaxNameLength = 80;
    public const int MaxTemplates = 64;
    public const int MaxTileCount = 20_000;

    private readonly List<MapStampTemplate> _templates = new();

    public IReadOnlyList<MapStampTemplate> Templates => _templates;

    public MapStampTemplate? FindByName(string? name)
    {
        var key = NormalizeName(name);
        if (key.Length == 0)
        {
            return null;
        }

        foreach (var template in _templates)
        {
            if (string.Equals(NormalizeName(template.Name), key, StringComparison.OrdinalIgnoreCase))
            {
                return template;
            }
        }

        return null;
    }

    public bool TryUpsert(MapStampTemplate template, out string? error)
    {
        ArgumentNullException.ThrowIfNull(template);
        if (!MapStampTemplateOperations.TryValidate(template, out error))
        {
            return false;
        }

        var existing = FindByName(template.Name);
        if (existing is null && _templates.Count >= MaxTemplates)
        {
            error = $"Liste pleine ({MaxTemplates} modèles). Supprimez-en un avant d’en ajouter.";
            return false;
        }

        if (existing is not null)
        {
            template.Id = existing.Id;
            var index = _templates.IndexOf(existing);
            _templates[index] = template;
        }
        else
        {
            _templates.Add(template);
        }

        _templates.Sort(static (a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        error = null;
        return true;
    }

    public bool TryRemove(string? name, out string? error)
    {
        var existing = FindByName(name);
        if (existing is null)
        {
            error = "Modèle introuvable.";
            return false;
        }

        _templates.Remove(existing);
        error = null;
        return true;
    }

    public static bool TryLoad(string directory, out MapStampTemplateLibrary library, out string? error)
    {
        library = new MapStampTemplateLibrary();
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        var path = Path.Combine(directory, FileName);
        if (!File.Exists(path))
        {
            error = null;
            return true;
        }

        MapStampTemplateFile? file;
        try
        {
            file = JsonSerializer.Deserialize<MapStampTemplateFile>(File.ReadAllBytes(path), Json);
        }
        catch (JsonException)
        {
            error = "Le fichier de modèles est illisible.";
            return false;
        }
        catch (IOException ex)
        {
            error = "Lecture des modèles impossible : " + ex.Message;
            return false;
        }

        if (file is null || file.Version != DocumentVersion)
        {
            error = "Version de modèles non prise en charge.";
            return false;
        }

        foreach (var template in file.Templates ?? new List<MapStampTemplate>())
        {
            if (template is null)
            {
                error = "Modèle invalide dans le fichier.";
                return false;
            }

            template.Layers ??= new List<MapStampTemplateLayer>();
            template.Prefabs ??= new List<MapStampTemplatePrefab>();
            foreach (var layer in template.Layers)
            {
                if (layer is null)
                {
                    error = "Couche de modèle invalide.";
                    return false;
                }

                layer.Tiles ??= new List<MapStampTemplateTile>();
                layer.DisplayName ??= string.Empty;
            }

            if (!MapStampTemplateOperations.TryValidate(template, out error))
            {
                return false;
            }

            if (library.FindByName(template.Name) is not null)
            {
                error = "Deux modèles portent le même nom : " + template.Name.Trim();
                return false;
            }

            library._templates.Add(template);
        }

        if (library._templates.Count > MaxTemplates)
        {
            error = $"Liste trop grande (> {MaxTemplates}).";
            return false;
        }

        library._templates.Sort(static (a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));
        error = null;
        return true;
    }

    public bool TryWrite(string directory, out string? error)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        try
        {
            Directory.CreateDirectory(directory);
            var path = Path.Combine(directory, FileName);
            var payload = new MapStampTemplateFile
            {
                Version = DocumentVersion,
                Templates = _templates,
            };
            var bytes = JsonSerializer.SerializeToUtf8Bytes(payload, Json);
            var temporary = path + ".tmp";
            File.WriteAllBytes(temporary, bytes);
            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            error = "Enregistrement des modèles impossible : " + ex.Message;
            return false;
        }

        error = null;
        return true;
    }

    internal static string NormalizeName(string? name) => (name ?? string.Empty).Trim();

    private static readonly JsonSerializerOptions Json = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.CamelCase) },
    };

    private sealed class MapStampTemplateFile
    {
        public int Version { get; set; }

        public List<MapStampTemplate>? Templates { get; set; }
    }
}

public static class MapStampTemplateOperations
{
    public static bool TryCapture(
        Map map,
        int left,
        int top,
        int width,
        int height,
        IReadOnlyList<PrefabPlacement>? prefabs,
        string? name,
        out MapStampTemplate? template,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(map);
        template = null;
        var trimmed = MapStampTemplateLibrary.NormalizeName(name);
        if (!TryValidateName(trimmed, out error))
        {
            return false;
        }

        if (!TryNormalizeRect(map, left, top, width, height, out var rectLeft, out var rectTop, out var rectWidth, out var rectHeight, out error))
        {
            return false;
        }

        if (!TryValidateMapIdentity(map, out error))
        {
            return false;
        }

        var layers = new List<MapStampTemplateLayer>(map.Layers.Count);
        var tileCount = 0;
        for (var i = 0; i < map.Layers.Count; i++)
        {
            var source = map.Layers[i];
            var captured = new List<MapStampTemplateTile>();
            for (var y = rectTop; y < rectTop + rectHeight; y++)
            {
                for (var x = rectLeft; x < rectLeft + rectWidth; x++)
                {
                    var tile = source.Tiles.LastOrDefault(candidate => candidate.X == x && candidate.Y == y);
                    if (tile is null)
                    {
                        continue;
                    }

                    if (!TryCopyTile(map.GraphicIdentity, tile, x - rectLeft, y - rectTop, out var copy, out error))
                    {
                        return false;
                    }

                    captured.Add(copy);
                    tileCount++;
                    if (tileCount > MapStampTemplateLibrary.MaxTileCount)
                    {
                        error = $"Modèle trop grand (plus de {MapStampTemplateLibrary.MaxTileCount} tuiles). Réduisez la sélection.";
                        return false;
                    }
                }
            }

            layers.Add(new MapStampTemplateLayer
            {
                LayerIndex = i,
                LayerType = source.LayerType,
                DisplayName = source.DisplayName ?? string.Empty,
                Tiles = captured,
            });
        }

        var capturedPrefabs = new List<MapStampTemplatePrefab>();
        if (prefabs is not null)
        {
            foreach (var placement in prefabs)
            {
                if (placement is null)
                {
                    continue;
                }

                var inside = placement.TileX >= rectLeft
                    && placement.TileY >= rectTop
                    && placement.TileX < rectLeft + rectWidth
                    && placement.TileY < rectTop + rectHeight;
                if (!inside)
                {
                    continue;
                }

                if (!PrefabPlacementService.IsValidId(placement.PrefabId))
                {
                    error = "Prefab sans identifiant valide dans la sélection.";
                    return false;
                }

                capturedPrefabs.Add(new MapStampTemplatePrefab
                {
                    PrefabId = placement.PrefabId.Trim(),
                    Facing = placement.Facing,
                    TileX = placement.TileX - rectLeft,
                    TileY = placement.TileY - rectTop,
                });
            }
        }

        if (tileCount == 0 && capturedPrefabs.Count == 0)
        {
            error = "Rien à enregistrer dans ce rectangle.";
            return false;
        }

        template = new MapStampTemplate
        {
            Id = Guid.NewGuid().ToString("N"),
            Name = trimmed,
            Width = rectWidth,
            Height = rectHeight,
            TileSizePixels = map.GraphicIdentity == TileGraphicIdentity.TileAsset
                ? map.TileSizePixels
                : WorldMetrics.DefaultTileSizePixels,
            GraphicIdentity = map.GraphicIdentity,
            Layers = layers,
            Prefabs = capturedPrefabs,
        };
        return TryValidate(template, out error);
    }

    public static bool TryValidate(MapStampTemplate template, out string? error)
    {
        ArgumentNullException.ThrowIfNull(template);
        if (string.IsNullOrWhiteSpace(template.Id) || template.Id.Length > 40
            || template.Id.Any(ch => !char.IsAsciiLetterOrDigit(ch)))
        {
            error = "Identifiant de modèle invalide.";
            return false;
        }

        if (!TryValidateName(template.Name, out error))
        {
            return false;
        }

        if (template.Width < MapEditOperations.MinDimensionTiles || template.Height < MapEditOperations.MinDimensionTiles
            || template.Width > MapEditOperations.MaxDimensionTiles || template.Height > MapEditOperations.MaxDimensionTiles)
        {
            error = $"La taille du modèle doit rester entre {MapEditOperations.MinDimensionTiles} et {MapEditOperations.MaxDimensionTiles} tuiles.";
            return false;
        }

        if (template.GraphicIdentity == TileGraphicIdentity.TileAsset)
        {
            if (template.TileSizePixels != TileAssetMetrics.TargetTileSizePixels)
            {
                error = "Un modèle TileAsset reste en tuiles de 48 px.";
                return false;
            }
        }
        else if (template.GraphicIdentity != TileGraphicIdentity.SheetSource)
        {
            error = "Identité graphique du modèle inconnue.";
            return false;
        }

        template.Layers ??= new List<MapStampTemplateLayer>();
        template.Prefabs ??= new List<MapStampTemplatePrefab>();
        var seenLayers = new HashSet<int>();
        var tiles = 0;
        foreach (var layer in template.Layers)
        {
            if (layer is null)
            {
                error = "Couche de modèle invalide.";
                return false;
            }

            if (layer.LayerIndex < 0 || layer.LayerIndex > 63 || !seenLayers.Add(layer.LayerIndex))
            {
                error = "Index de couche invalide dans le modèle.";
                return false;
            }

            if ((layer.DisplayName ?? string.Empty).Length > 120)
            {
                error = "Nom de couche trop long.";
                return false;
            }

            layer.Tiles ??= new List<MapStampTemplateTile>();
            var seenCells = new HashSet<(int X, int Y)>();
            foreach (var tile in layer.Tiles)
            {
                if (tile is null)
                {
                    error = "Tuile de modèle invalide.";
                    return false;
                }

                if (tile.X < 0 || tile.Y < 0 || tile.X >= template.Width || tile.Y >= template.Height)
                {
                    error = "Tuile hors du rectangle du modèle.";
                    return false;
                }

                if (!seenCells.Add((tile.X, tile.Y)))
                {
                    error = "Deux tuiles occupent la même case du modèle.";
                    return false;
                }

                if (!TryValidateTileIdentity(template.GraphicIdentity, tile, out error))
                {
                    return false;
                }

                tiles++;
            }
        }

        if (tiles > MapStampTemplateLibrary.MaxTileCount)
        {
            error = $"Modèle trop grand (plus de {MapStampTemplateLibrary.MaxTileCount} tuiles).";
            return false;
        }

        foreach (var prefab in template.Prefabs)
        {
            if (prefab is null || !PrefabPlacementService.IsValidId(prefab.PrefabId))
            {
                error = "Prefab du modèle invalide.";
                return false;
            }

            if (!Enum.IsDefined(prefab.Facing))
            {
                error = "Orientation de prefab inconnue.";
                return false;
            }

            if (prefab.TileX < 0 || prefab.TileY < 0 || prefab.TileX >= template.Width || prefab.TileY >= template.Height)
            {
                error = "Prefab hors du rectangle du modèle.";
                return false;
            }
        }

        if (tiles == 0 && template.Prefabs.Count == 0)
        {
            error = "Rien à enregistrer dans ce rectangle.";
            return false;
        }

        error = null;
        return true;
    }

    public static bool TryValidateForStamp(
        Map map,
        MapStampTemplate template,
        int anchorX,
        int anchorY,
        out string? error)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(template);
        if (!TryValidate(template, out error))
        {
            return false;
        }

        if (!TryValidateMapIdentity(map, out error))
        {
            return false;
        }

        if (map.GraphicIdentity != template.GraphicIdentity)
        {
            error = $"Ce modèle est en {FormatIdentity(template.GraphicIdentity, template.TileSizePixels)}. La carte courante est en {FormatIdentity(map.GraphicIdentity, map.TileSizePixels)}.";
            return false;
        }

        if (template.GraphicIdentity == TileGraphicIdentity.TileAsset
            && (map.TileSizePixels != TileAssetMetrics.TargetTileSizePixels
                || template.TileSizePixels != TileAssetMetrics.TargetTileSizePixels))
        {
            error = "Une carte TileAsset reste en tuiles de 48 px.";
            return false;
        }

        if (!IntersectsMap(anchorX, anchorY, template.Width, template.Height, map.Width, map.Height))
        {
            error = "Le modèle dépasse entièrement la carte.";
            return false;
        }

        error = null;
        return true;
    }

    public static MapStampTileApplyResult ApplyTiles(
        Map map,
        MapStampTemplate template,
        int anchorX,
        int anchorY,
        Action? beforeMutate)
    {
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(template);
        if (!TryValidateForStamp(map, template, anchorX, anchorY, out _))
        {
            return default;
        }

        var ops = new List<(int LayerIndex, int X, int Y, MapStampTemplateTile? Tile)>();
        var skipped = 0;
        foreach (var layer in template.Layers)
        {
            var editable = MapEditOperations.IsLayerEditable(map, layer.LayerIndex);
            var byCell = Index(layer.Tiles);
            Dictionary<(int X, int Y), Tile>? destination = null;
            if (layer.LayerIndex >= 0 && layer.LayerIndex < map.Layers.Count)
            {
                destination = new Dictionary<(int X, int Y), Tile>();
                foreach (var tile in map.Layers[layer.LayerIndex].Tiles)
                {
                    destination[(tile.X, tile.Y)] = tile;
                }
            }
            var layerOps = new List<(int LayerIndex, int X, int Y, MapStampTemplateTile? Tile)>();
            var relevant = false;
            for (var y = 0; y < template.Height; y++)
            {
                for (var x = 0; x < template.Width; x++)
                {
                    var gx = anchorX + x;
                    var gy = anchorY + y;
                    if (gx < 0 || gy < 0 || gx >= map.Width || gy >= map.Height)
                    {
                        continue;
                    }

                    byCell.TryGetValue((x, y), out var stamp);
                    var hasDest = destination?.ContainsKey((gx, gy)) == true;
                    if (stamp is null && !hasDest)
                    {
                        continue;
                    }

                    relevant = true;
                    if (!editable)
                    {
                        continue;
                    }

                    layerOps.Add((layer.LayerIndex, gx, gy, stamp));
                }
            }

            if (!editable && relevant)
            {
                skipped++;
                continue;
            }

            ops.AddRange(layerOps);
        }

        if (ops.Count > 0)
        {
            beforeMutate?.Invoke();
            foreach (var (layerIndex, x, y, stamp) in ops)
            {
                if (stamp is null)
                {
                    MapEditOperations.EraseTile(map, layerIndex, x, y);
                }
                else
                {
                    MapEditOperations.PaintTile(map, layerIndex, x, y, ToRuntimeTile(stamp, x, y));
                }
            }
        }

        var painted = 0;
        var cleared = 0;
        foreach (var op in ops)
        {
            if (op.Tile is null)
            {
                cleared++;
            }
            else
            {
                painted++;
            }
        }

        return new MapStampTileApplyResult(painted, cleared, skipped);
    }

    public static IReadOnlyList<PrefabPlacement> PrefabsAt(MapStampTemplate template, int anchorX, int anchorY)
    {
        ArgumentNullException.ThrowIfNull(template);
        var list = new List<PrefabPlacement>();
        foreach (var prefab in template.Prefabs ?? new List<MapStampTemplatePrefab>())
        {
            if (prefab is null || !PrefabPlacementService.IsValidId(prefab.PrefabId))
            {
                continue;
            }

            list.Add(new PrefabPlacement
            {
                PrefabId = prefab.PrefabId.Trim(),
                Facing = prefab.Facing,
                TileX = anchorX + prefab.TileX,
                TileY = anchorY + prefab.TileY,
            });
        }

        return list;
    }

    public static string FormatSummary(MapStampTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);
        var tiles = template.Layers?.Sum(layer => layer?.Tiles?.Count ?? 0) ?? 0;
        var prefabs = template.Prefabs?.Count ?? 0;
        return $"{template.Name} — {template.Width}×{template.Height} · {FormatIdentity(template.GraphicIdentity, template.TileSizePixels)} · {Count(tiles, "tuile", "tuiles")} · {Count(prefabs, "prefab", "prefabs")}";
    }

    public static string FormatSaved(MapStampTemplate template)
    {
        ArgumentNullException.ThrowIfNull(template);
        var tiles = template.Layers?.Sum(layer => layer?.Tiles?.Count ?? 0) ?? 0;
        var prefabs = template.Prefabs?.Count ?? 0;
        return $"Modèle « {template.Name} » enregistré · {template.Width}×{template.Height} · {FormatIdentity(template.GraphicIdentity, template.TileSizePixels)} · {Count(tiles, "tuile", "tuiles")} · {Count(prefabs, "prefab", "prefabs")}";
    }

    public static string FormatStamped(MapStampTemplate template, int anchorX, int anchorY, MapStampTileApplyResult tiles, int prefabsPlaced, int prefabsSkipped)
    {
        ArgumentNullException.ThrowIfNull(template);
        var text = $"Modèle « {template.Name} » posé en ({anchorX}, {anchorY}) · {FormatIdentity(template.GraphicIdentity, template.TileSizePixels)} · {Count(tiles.Painted, "tuile", "tuiles")} · {Count(tiles.Cleared, "case vidée", "cases vidées")} · {Count(prefabsPlaced, "prefab", "prefabs")}";
        if (tiles.LayersSkipped > 0)
        {
            text += " · " + Count(tiles.LayersSkipped, "couche ignorée", "couches ignorées");
        }

        if (prefabsSkipped > 0)
        {
            text += " · " + Count(prefabsSkipped, "prefab non posé", "prefabs non posés");
        }

        return text;
    }

    public static string FormatRejected(MapStampTileApplyResult tiles, int prefabsSkipped)
    {
        if (tiles.LayersSkipped > 0 && prefabsSkipped > 0)
        {
            return "Couches verrouillées ou absentes, et prefabs non posés.";
        }

        if (tiles.LayersSkipped > 0)
        {
            return "Les couches de destination sont verrouillées ou absentes.";
        }

        if (prefabsSkipped > 0)
        {
            return "Prefab hors carte ou inconnu du catalogue.";
        }

        return "Rien à poser ici.";
    }

    private static bool TryCopyTile(
        TileGraphicIdentity identity,
        Tile tile,
        int localX,
        int localY,
        out MapStampTemplateTile copy,
        out string? error)
    {
        copy = new MapStampTemplateTile
        {
            X = localX,
            Y = localY,
            Type = tile.Type,
            AssetId = tile.AssetId.IsNone ? string.Empty : tile.AssetId.ToHex(),
            TilesetId = tile.TilesetId,
            SrcX = tile.SrcX,
            SrcY = tile.SrcY,
            ScriptId = tile.ScriptId,
            WarpTargetMapId = tile.WarpTargetMapId,
            WarpTargetX = tile.WarpTargetX,
            WarpTargetY = tile.WarpTargetY,
        };

        foreach (var attribute in tile.Attributes)
        {
            switch (attribute)
            {
                case BlockAttribute:
                    copy.Block = true;
                    break;
                case WarpAttribute warp:
                    copy.HasWarpAttribute = true;
                    copy.WarpAttributeMapId = warp.TargetMapId;
                    copy.WarpAttributeX = warp.TargetX;
                    copy.WarpAttributeY = warp.TargetY;
                    break;
                case ResourceAttribute resource:
                    copy.HasResourceAttribute = true;
                    copy.ResourceId = resource.ResourceId;
                    break;
                default:
                    error = "Attribut de tuile non copié : " + attribute.GetType().Name + ".";
                    return false;
            }
        }

        return TryValidateTileIdentity(identity, copy, out error);
    }

    private static bool TryValidateTileIdentity(TileGraphicIdentity identity, MapStampTemplateTile tile, out string? error)
    {
        var hasSheet = tile.TilesetId != 0 || tile.SrcX != 0 || tile.SrcY != 0;
        var hasAsset = !string.IsNullOrWhiteSpace(tile.AssetId);
        if (identity == TileGraphicIdentity.TileAsset)
        {
            if (hasSheet)
            {
                error = "Ce modèle TileAsset ne peut pas stocker une position de feuille (Src).";
                return false;
            }

            if (hasAsset && (!TileAssetId.TryParse(tile.AssetId, out var id) || id.IsNone))
            {
                error = "TileAssetId invalide dans le modèle.";
                return false;
            }
        }
        else if (hasAsset)
        {
            error = "Un modèle feuille (v5) ne stocke pas de TileAssetId.";
            return false;
        }

        error = null;
        return true;
    }

    private static Tile ToRuntimeTile(MapStampTemplateTile stamp, int x, int y)
    {
        var tile = new Tile
        {
            X = x,
            Y = y,
            Type = stamp.Type,
            TilesetId = stamp.TilesetId,
            SrcX = stamp.SrcX,
            SrcY = stamp.SrcY,
            ScriptId = stamp.ScriptId,
            WarpTargetMapId = stamp.WarpTargetMapId,
            WarpTargetX = stamp.WarpTargetX,
            WarpTargetY = stamp.WarpTargetY,
        };
        if (!string.IsNullOrWhiteSpace(stamp.AssetId) && TileAssetId.TryParse(stamp.AssetId, out var id))
        {
            tile.AssetId = id;
        }

        if (stamp.Block)
        {
            tile.Attributes.Add(new BlockAttribute());
        }

        if (stamp.HasWarpAttribute)
        {
            tile.Attributes.Add(new WarpAttribute
            {
                TargetMapId = stamp.WarpAttributeMapId,
                TargetX = stamp.WarpAttributeX,
                TargetY = stamp.WarpAttributeY,
            });
        }

        if (stamp.HasResourceAttribute)
        {
            tile.Attributes.Add(new ResourceAttribute { ResourceId = stamp.ResourceId });
        }

        return tile;
    }

    private static bool TryValidateName(string name, out string? error)
    {
        if (name.Length is < 1 or > MapStampTemplateLibrary.MaxNameLength)
        {
            error = $"Le nom du modèle doit faire entre 1 et {MapStampTemplateLibrary.MaxNameLength} caractères.";
            return false;
        }

        foreach (var ch in name)
        {
            if (char.IsControl(ch))
            {
                error = "Le nom du modèle ne peut pas contenir de caractère de contrôle.";
                return false;
            }
        }

        error = null;
        return true;
    }

    private static bool TryValidateMapIdentity(Map map, out string? error)
    {
        if (map.GraphicIdentity == TileGraphicIdentity.TileAsset && map.TileSizePixels != TileAssetMetrics.TargetTileSizePixels)
        {
            error = "Une carte TileAsset reste en tuiles de 48 px.";
            return false;
        }

        if (map.GraphicIdentity is not (TileGraphicIdentity.TileAsset or TileGraphicIdentity.SheetSource))
        {
            error = "Identité graphique de la carte inconnue.";
            return false;
        }

        error = null;
        return true;
    }

    private static bool TryNormalizeRect(
        Map map,
        int left,
        int top,
        int width,
        int height,
        out int rectLeft,
        out int rectTop,
        out int rectWidth,
        out int rectHeight,
        out string? error)
    {
        rectLeft = 0;
        rectTop = 0;
        rectWidth = 0;
        rectHeight = 0;
        if (width <= 0 || height <= 0)
        {
            error = "Rectangle vide.";
            return false;
        }

        var x0 = Math.Max(0, left);
        var y0 = Math.Max(0, top);
        var x1 = Math.Min(map.Width, left + width);
        var y1 = Math.Min(map.Height, top + height);
        if (x1 <= x0 || y1 <= y0)
        {
            error = "La sélection est hors de la carte.";
            return false;
        }

        rectLeft = x0;
        rectTop = y0;
        rectWidth = x1 - x0;
        rectHeight = y1 - y0;
        error = null;
        return true;
    }

    private static bool IntersectsMap(int anchorX, int anchorY, int width, int height, int mapWidth, int mapHeight)
        => anchorX < mapWidth && anchorY < mapHeight && anchorX + width > 0 && anchorY + height > 0;

    private static string FormatIdentity(TileGraphicIdentity identity, int tileSizePixels)
        => identity == TileGraphicIdentity.TileAsset
            ? $"TileAsset (v6, {tileSizePixels} px)"
            : "Feuille (v5)";

    private static string Count(int count, string one, string many)
        => count == 1 ? "1 " + one : count + " " + many;

    private static Dictionary<(int X, int Y), MapStampTemplateTile> Index(List<MapStampTemplateTile> tiles)
    {
        var index = new Dictionary<(int X, int Y), MapStampTemplateTile>();
        foreach (var tile in tiles)
        {
            if (tile is not null)
            {
                index[(tile.X, tile.Y)] = tile;
            }
        }

        return index;
    }

}
