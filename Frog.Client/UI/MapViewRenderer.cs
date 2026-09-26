#nullable enable
using System.Drawing;
using System.Drawing.Drawing2D;
using System.IO;
using Frog.Application.Maps;
using Frog.Application.Prefabs;
using Frog.Client.Assets;
using Frog.Core.Constants;
using Frog.Core.Distribution;
using Frog.Core.Enums;
using Frog.Core.Gameplay;
using Frog.Core.Maps;
using Frog.Core.Models;
using Frog.Core.Protocol;
using Frog.Core.Weather;

namespace Frog.Client.UI;

internal static class MapViewRenderer
{
    private static readonly Color BaseWalkable = Color.FromArgb(60, 90, 60);
    private static readonly Color GroundTile = Color.FromArgb(120, 160, 100);
    private static readonly Color BlockTile = Color.FromArgb(45, 45, 55);
    private static readonly Color WarpTile = Color.FromArgb(180, 100, 200);

    /// <param name="otherPlayerCentersPx">Centre joueur autres en pixels monde (coins carte = grille × taille tuile).</param>
    /// <param name="mapEvents">Tuiles avec événements serveur (léger surlignage).</param>
    /// <param name="tilesetBitmaps">Id tileset → image ; peut être vide (rendu couleur de secours).</param>
    /// <param name="showTileGrid">Contour de tuile debug. Défaut <c>false</c> — pas de grille visible en jeu.</param>
    /// <param name="localPose">Facing + walk frame for the local player (default south idle).</param>
    /// <param name="otherPoses">Optional facing + walk frame per other username.</param>
    /// <param name="prefabPlacements">Instances prefab (sidecar), triées avec la rangée sud de leur empreinte.</param>
    /// <param name="prefabCatalog">Catalogue pour résoudre empreinte / sprite.</param>
    /// <param name="prefabBitmaps">Nom de fichier sprite → image.</param>
    /// <param name="npcCentersPx">Centres PNJ en pixels monde (pieds). Optionnel.</param>
    /// <param name="npcPoses">Facing + walk frame par id PNJ.</param>
    /// <param name="monsterCentersPx">Centres monstre en pixels monde (pieds). Optionnel.</param>
    /// <param name="monsterPoses">Facing + walk frame par id monstre.</param>
    /// <param name="weatherPlan">Overlay teinte / traits (MVP). Défaut = pas de dessin.</param>
    /// <param name="weatherTickMs">Horloge cheap pour les traits de pluie.</param>
    /// <param name="localAppearance">Overlays du joueur local (tunique, arme, armure, casque). Les autres joueurs restent corps + tête : leur équipement n'est pas sur le fil.</param>
    /// <param name="localLook">Palettes corps / cheveux / tunique du joueur local. Client seulement.</param>
    /// <param name="tileAssets">Tuiles v6 vérifiées, indexées par <see cref="TileAssetId"/>. Absent : pas de blit 48×48.</param>
    /// <param name="tileAssetBitmaps">Cache d’affichage rempli à la demande. L’appelant dispose les bitmaps.</param>
    /// <param name="groundLootCentersPx">Ancres du butin (centre du sac). Même tri vertical que les acteurs.</param>
    /// <param name="playtestPlacedEntities">Apparitions, PNJ et objets du sidecar playtest. Absent en partie normale.</param>
    /// <param name="localDisplayName">Nom du personnage local s’il diffère du pseudo. Vide : le pseudo.</param>
    public static Bitmap Render(
        Map map,
        IReadOnlyDictionary<string, (float CxPx, float CyPx)> otherPlayerCentersPx,
        string? localUsername,
        float localCenterXPx,
        float localCenterYPx,
        IReadOnlyDictionary<int, Bitmap>? tilesetBitmaps,
        IReadOnlyList<MapEventWireEntry>? mapEvents = null,
        bool showTileGrid = false,
        PlayerSpritePose localPose = default,
        IReadOnlyDictionary<string, PlayerSpritePose>? otherPoses = null,
        IReadOnlyList<PrefabPlacement>? prefabPlacements = null,
        PrefabCatalog? prefabCatalog = null,
        IReadOnlyDictionary<string, Bitmap>? prefabBitmaps = null,
        IReadOnlyDictionary<string, (float CxPx, float CyPx)>? npcCentersPx = null,
        IReadOnlyDictionary<string, WorldSpritePose>? npcPoses = null,
        IReadOnlyDictionary<string, (float CxPx, float CyPx)>? monsterCentersPx = null,
        IReadOnlyDictionary<string, WorldSpritePose>? monsterPoses = null,
        WeatherOverlayPlan weatherPlan = default,
        int weatherTickMs = 0,
        PaperdollOverlaySet localAppearance = default,
        CharacterLook localLook = default,
        ITileAssetLookup? tileAssets = null,
        IDictionary<TileAssetId, Bitmap>? tileAssetBitmaps = null,
        IReadOnlyList<(int PixelX, int PixelY)>? groundLootCentersPx = null,
        IReadOnlyList<MapPlacedEntity>? playtestPlacedEntities = null,
        string? localDisplayName = null)
    {
        var tw = MapTileSizePixels(map);
        var w = map.Width * tw;
        var h = map.Height * tw;
        var bmp = new Bitmap(Math.Max(w, 1), Math.Max(h, 1));
        using var g = Graphics.FromImage(bmp);
        g.SmoothingMode = SmoothingMode.None;
        g.InterpolationMode = InterpolationMode.NearestNeighbor;
        g.PixelOffsetMode = PixelOffsetMode.Half;
        g.Clear(BaseWalkable);

        var below = new List<Layer>(map.Layers.Count);
        var fringe = new List<Layer>(4);
        var attributes = new List<Layer>(2);
        foreach (var layer in map.Layers)
        {
            if (!layer.Visible)
            {
                continue;
            }

            if (WorldDepth.IsAttributeLayer(layer.LayerType))
            {
                attributes.Add(layer);
            }
            else if (WorldDepth.IsFringeLayer(layer.LayerType))
            {
                fringe.Add(layer);
            }
            else if (WorldDepth.IsBelowActorLayer(layer.LayerType))
            {
                below.Add(layer);
            }
        }

        var actors = CollectDepthActors(
            tw,
            groundLootCentersPx,
            monsterCentersPx,
            monsterPoses,
            npcCentersPx,
            npcPoses,
            otherPlayerCentersPx,
            otherPoses,
            localUsername,
            localCenterXPx,
            localCenterYPx,
            localPose,
            localDisplayName);
        actors.Sort(static (a, b) => WorldDepth.CompareActors(a.Key, b.Key));
        var actorIndex = 0;

        foreach (var step in WorldDepth.RowSteps(map.Height))
        {
            switch (step.Kind)
            {
                case WorldDepth.RowStepKind.ActorsNorthOfMap:
                    DrawPlacedPrefabs(g, map, tw, prefabPlacements, prefabCatalog, prefabBitmaps, baselineRow: -1, outsideMap: true);
                    actorIndex = DrawActorsWhile(g, actors, actorIndex, static row => row < 0, localPose, localAppearance, localLook);
                    break;
                case WorldDepth.RowStepKind.BelowActors:
                    DrawTileRow(g, map, below, attributes, step.Row, tw, tilesetBitmaps, tileAssets, tileAssetBitmaps, showTileGrid);
                    break;
                case WorldDepth.RowStepKind.ActorsOnRow:
                    DrawPlacedPrefabs(g, map, tw, prefabPlacements, prefabCatalog, prefabBitmaps, baselineRow: step.Row, outsideMap: false);
                    DrawPlaytestPlacedEntities(g, tw, step.Row, playtestPlacedEntities);
                    actorIndex = DrawActorsWhile(g, actors, actorIndex, row => row == step.Row, localPose, localAppearance, localLook);
                    break;
                case WorldDepth.RowStepKind.Fringe:
                    DrawTileRow(g, map, fringe, attributes: null, step.Row, tw, tilesetBitmaps, tileAssets, tileAssetBitmaps, showTileGrid: false);
                    break;
                case WorldDepth.RowStepKind.ActorsSouthOfMap:
                    DrawPlacedPrefabs(g, map, tw, prefabPlacements, prefabCatalog, prefabBitmaps, baselineRow: map.Height, outsideMap: true);
                    while (actorIndex < actors.Count)
                    {
                        DrawDepthActor(g, actors[actorIndex], localPose, localAppearance, localLook);
                        actorIndex++;
                    }

                    break;
            }
        }

        if (mapEvents is { Count: > 0 })
        {
            var prevEvSmooth = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            try
            {
                foreach (var ev in mapEvents)
                {
                    if (ev.TileX < 0 || ev.TileY < 0 || ev.TileX >= map.Width || ev.TileY >= map.Height)
                    {
                        continue;
                    }

                    var accent = string.Equals(ev.Slug, MapEventSlugs.DemoInteract, StringComparison.Ordinal)
                        ? Color.FromArgb(230, 255, 210, 72)
                        : Color.FromArgb(200, 96, 212, 255);
                    var kind = MapEventTriggerNormalization.NormalizeTriggerKind(ev.TriggerKind);
                    var stepOn = string.Equals(kind, MapEventTriggerKinds.StepOn, StringComparison.Ordinal);
                    var page = string.Equals(kind, MapEventTriggerKinds.Page, StringComparison.Ordinal);
                    var autoTile = string.Equals(kind, MapEventTriggerKinds.AutoTile, StringComparison.Ordinal);
                    if (page)
                    {
                        DrawEventTilePageDotOutline(g, ev.TileX, ev.TileY, tw, accent);
                    }
                    else if (autoTile)
                    {
                        DrawEventTileDashedOutline(g, ev.TileX, ev.TileY, tw, accent);
                    }
                    else if (stepOn)
                    {
                        DrawEventTileDiamondOutline(g, ev.TileX, ev.TileY, tw, accent);
                    }
                    else
                    {
                        DrawEventTileOutline(g, ev.TileX, ev.TileY, tw, accent);
                    }
                }
            }
            finally
            {
                g.SmoothingMode = prevEvSmooth;
            }
        }

        WeatherOverlayRenderer.Draw(g, bmp.Size, weatherPlan, weatherTickMs);
        DrawNameplates(g, tw, actors, mapEvents, npcCentersPx, playtestPlacedEntities, map.Width, map.Height);
        return bmp;
    }

    /// <summary>Filets 1 px alignés pixels (évite <c>DrawRectangle</c> + <c>PixelOffsetMode.Half</c> qui rate les coutures).</summary>
    private static void DrawDebugTileGrid(Graphics g, Rectangle rect)
    {
        var previous = g.PixelOffsetMode;
        g.PixelOffsetMode = PixelOffsetMode.None;
        try
        {
            using var brush = new SolidBrush(Color.FromArgb(40, 0, 0, 0));
            g.FillRectangle(brush, rect.X, rect.Y, rect.Width, 1);
            g.FillRectangle(brush, rect.X, rect.Y, 1, rect.Height);
        }
        finally
        {
            g.PixelOffsetMode = previous;
        }
    }

    internal static void DrawPlaytestPlacedEntities(
        Graphics g,
        int tileSize,
        int row,
        IReadOnlyList<MapPlacedEntity>? entities)
    {
        if (g is null || entities is not { Count: > 0 } || tileSize <= 0)
        {
            return;
        }

        foreach (var entity in entities)
        {
            if (entity is null || entity.TileY != row)
            {
                continue;
            }

            var inset = Math.Max(2, tileSize / 6);
            var rect = new Rectangle(
                entity.TileX * tileSize + inset,
                entity.TileY * tileSize + inset,
                Math.Max(1, tileSize - (inset * 2)),
                Math.Max(1, tileSize - (inset * 2)));
            var color = entity.Kind switch
            {
                MapPlacedKind.Spawn => Color.FromArgb(230, 80, 200, 255),
                MapPlacedKind.Npc => Color.FromArgb(230, 70, 120, 255),
                _ => Color.FromArgb(230, 196, 132, 64),
            };
            using var brush = new SolidBrush(color);
            g.FillRectangle(brush, rect);
        }
    }

    internal static void DrawPlacedPrefabs(
        Graphics g,
        Map map,
        int tileSize,
        IReadOnlyList<PrefabPlacement>? placements,
        PrefabCatalog? catalog,
        IReadOnlyDictionary<string, Bitmap>? bitmaps,
        int baselineRow = int.MinValue,
        bool outsideMap = false)
    {
        if (g is null || map is null || placements is not { Count: > 0 } || catalog is null)
        {
            return;
        }

        foreach (var placement in placements)
        {
            if (placement is null
                || !PrefabPlacementService.TryGetDefinition(catalog, placement.PrefabId, out var definition)
                || !PrefabPlacementService.TryResolveVariant(definition, placement.Facing, out var variant)
                || !PrefabPlacementService.TryResolveFootprint(definition, variant, out var wTiles, out var hTiles))
            {
                continue;
            }

            if (baselineRow != int.MinValue || outsideMap)
            {
                var row = WorldDepth.FootprintBaselineRow(placement.TileY, hTiles);
                if (outsideMap)
                {
                    var north = baselineRow < 0;
                    if (north && row >= 0)
                    {
                        continue;
                    }

                    if (!north && row < map.Height)
                    {
                        continue;
                    }
                }
                else if (row != baselineRow)
                {
                    continue;
                }
            }

            var dest = new Rectangle(
                placement.TileX * tileSize,
                placement.TileY * tileSize,
                Math.Max(tileSize, wTiles * tileSize),
                Math.Max(tileSize, hTiles * tileSize));

            var fileName = Path.GetFileName(variant.SpriteFileName ?? string.Empty);
            if (!string.IsNullOrEmpty(fileName)
                && bitmaps is not null
                && bitmaps.TryGetValue(fileName, out var bmp)
                && bmp is not null)
            {
                var src = new Rectangle(0, 0, bmp.Width, bmp.Height);
                g.DrawImage(bmp, dest, src, GraphicsUnit.Pixel);
                continue;
            }

            using var fill = new SolidBrush(Color.FromArgb(180, 132, 86, 48));
            using var pen = new Pen(Color.FromArgb(220, 32, 20, 14), 2f);
            g.FillRectangle(fill, dest);
            g.DrawRectangle(pen, dest);
        }
    }

    /// <summary>
    /// Carte feuille : <see cref="WorldMetrics.DefaultTileSizePixels"/> (32).
    /// Carte TileAsset : <c>tileSizePixels</c> de l’en-tête v6 (48). La constante monde ne change pas.
    /// </summary>
    internal static int MapTileSizePixels(Map map) => TileAssetDisplayPixels.MapPixelSize(map);

    private static bool TryDrawTileAsset(
        Graphics g,
        Tile tile,
        Rectangle dst,
        ITileAssetLookup? tileAssets,
        IDictionary<TileAssetId, Bitmap>? cache)
    {
        if (tile.AssetId.IsNone || tileAssets is null || !tileAssets.TryGet(tile.AssetId, out var asset) || asset is null)
        {
            return false;
        }

        if (asset.NormalizedRgba.Length != TileAssetMetrics.CanonicalPixelByteCount)
        {
            return false;
        }

        Bitmap? created = null;
        Bitmap bmp;
        if (cache is not null && cache.TryGetValue(tile.AssetId, out var cached) && cached is not null)
        {
            bmp = cached;
        }
        else
        {
            created = ClientTileAssetImages.Create(asset.NormalizedRgba);
            bmp = created;
            if (cache is not null)
            {
                cache[tile.AssetId] = created;
                created = null;
            }
        }

        try
        {
            if (bmp.Width != TileAssetMetrics.TargetTileSizePixels || bmp.Height != TileAssetMetrics.TargetTileSizePixels)
            {
                return false;
            }

            g.DrawImage(bmp, dst);
            return true;
        }
        finally
        {
            created?.Dispose();
        }
    }

    private static bool TryDrawGraphicTile(
        Graphics g,
        Tile t,
        Rectangle dst,
        int tw,
        IReadOnlyDictionary<int, Bitmap>? tilesetBitmaps)
    {
        if (tilesetBitmaps is null || t.TilesetId <= 0 || !tilesetBitmaps.TryGetValue(t.TilesetId, out var bmp) || bmp is null)
        {
            return false;
        }

        var src = new Rectangle(t.SrcX, t.SrcY, tw, tw);
        if (src.Right > bmp.Width || src.Bottom > bmp.Height || src.X < 0 || src.Y < 0)
        {
            return false;
        }

        g.DrawImage(bmp, dst, src, GraphicsUnit.Pixel);
        return true;
    }

    private static void FillFallbackType(Graphics g, Rectangle rect, TileType type)
    {
        var color = type switch
        {
            TileType.Block => BlockTile,
            TileType.Warp => WarpTile,
            TileType.Ground or TileType.Unknown => GroundTile,
            _ => GroundTile,
        };

        using var brush = new SolidBrush(color);
        g.FillRectangle(brush, rect);
    }

    private static Tile? FindTile(Layer? layer, int tx, int ty)
    {
        if (layer is null)
        {
            return null;
        }

        foreach (var t in layer.Tiles)
        {
            if (t.X == tx && t.Y == ty)
            {
                return t;
            }
        }

        return null;
    }

    private static void DrawEventTileOutline(Graphics g, int tileX, int tileY, int tw, Color stroke)
    {
        var rect = new Rectangle(tileX * tw + 2, tileY * tw + 2, tw - 4, tw - 4);
        using var pen = new Pen(stroke, 3f);
        g.DrawRectangle(pen, rect);
    }

    private static void DrawEventTileDashedOutline(Graphics g, int tileX, int tileY, int tw, Color stroke)
    {
        var rect = new Rectangle(tileX * tw + 2, tileY * tw + 2, tw - 4, tw - 4);
        using var pen = new Pen(stroke, 2.8f) { DashStyle = DashStyle.Dash };
        g.DrawRectangle(pen, rect);
    }

    private static void DrawEventTilePageDotOutline(Graphics g, int tileX, int tileY, int tw, Color stroke)
    {
        var pad = 5f;
        var r = Math.Max(2.5f, tw * 0.14f);
        var cx = tileX * tw + pad + r;
        var cy = tileY * tw + tw - pad - r;
        using var pen = new Pen(stroke, 2.6f);
        g.DrawEllipse(pen, cx - r, cy - r, r * 2f, r * 2f);
    }

    /// <summary>Marqueur visuel aligné éditeur : <c>step_on</c> = losange sur la tuile.</summary>
    private static void DrawEventTileDiamondOutline(Graphics g, int tileX, int tileY, int tw, Color stroke)
    {
        var pad = 4;
        var x0 = tileX * tw + pad;
        var y0 = tileY * tw + pad;
        var x1 = tileX * tw + tw - pad;
        var y1 = tileY * tw + tw - pad;
        var cx = (x0 + x1) * 0.5f;
        var cy = (y0 + y1) * 0.5f;
        var pts = new[]
        {
            new PointF(cx, y0),
            new PointF(x1, cy),
            new PointF(cx, y1),
            new PointF(x0, cy),
        };
        using var pen = new Pen(stroke, 2.8f);
        g.DrawPolygon(pen, pts);
    }

    /// <summary>Pieds / centre bas du sprite sur (Cx, Cy) ; nearest, scale from native 32 (tileSize stays 32).</summary>
    private static void DrawPlayerSpriteAtPixelCenter(
        Graphics g,
        float centerXPx,
        float centerYPx,
        bool other,
        PlayerSpritePose pose,
        PaperdollOverlaySet appearance = default,
        CharacterLook look = default)
        => PlayerWorldAssets.DrawFeetAnchored(g, centerXPx, centerYPx, other, pose, appearance, look);

    private struct DepthActor
    {
        public WorldDepth.ActorKey Key;
        public float X;
        public float Y;
        public int LootX;
        public int LootY;
        public PlayerSpritePose PlayerPose;
        public WorldSpritePose WorldPose;
        public string? Name;
    }

    private static List<DepthActor> CollectDepthActors(
        int tileSize,
        IReadOnlyList<(int PixelX, int PixelY)>? groundLootCentersPx,
        IReadOnlyDictionary<string, (float CxPx, float CyPx)>? monsterCentersPx,
        IReadOnlyDictionary<string, WorldSpritePose>? monsterPoses,
        IReadOnlyDictionary<string, (float CxPx, float CyPx)>? npcCentersPx,
        IReadOnlyDictionary<string, WorldSpritePose>? npcPoses,
        IReadOnlyDictionary<string, (float CxPx, float CyPx)> otherPlayerCentersPx,
        IReadOnlyDictionary<string, PlayerSpritePose>? otherPoses,
        string? localUsername,
        float localCenterXPx,
        float localCenterYPx,
        PlayerSpritePose localPose,
        string? localDisplayName)
    {
        var actors = new List<DepthActor>();
        var sequence = 0;

        if (groundLootCentersPx is not null)
        {
            foreach (var (pixelX, pixelY) in groundLootCentersPx)
            {
                actors.Add(new DepthActor
                {
                    Key = new WorldDepth.ActorKey(
                        WorldDepth.FloorTileIndex(pixelY, tileSize),
                        pixelY,
                        WorldDepth.ActorSlot.Loot,
                        sequence++),
                    X = pixelX,
                    Y = pixelY,
                    LootX = pixelX,
                    LootY = pixelY,
                });
            }
        }

        AddWorldActors(actors, ref sequence, tileSize, monsterCentersPx, monsterPoses, WorldDepth.ActorSlot.Monster, includeName: false);
        AddWorldActors(actors, ref sequence, tileSize, npcCentersPx, npcPoses, WorldDepth.ActorSlot.Npc, includeName: true);

        foreach (var kv in otherPlayerCentersPx)
        {
            if (localUsername is not null && string.Equals(kv.Key, localUsername, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var otherPose = PlayerSpritePose.IdleDown;
            if (otherPoses is not null && otherPoses.TryGetValue(kv.Key, out var posed))
            {
                otherPose = posed;
            }

            actors.Add(ActorAt(
                tileSize,
                kv.Value.CxPx,
                kv.Value.CyPx,
                WorldDepth.ActorSlot.RemotePlayer,
                sequence++,
                otherPose,
                default,
                NameplatePainter.FormatLabel(kv.Key)));
        }

        var localLabel = NameplatePainter.FormatLabel(localDisplayName)
            ?? NameplatePainter.FormatLabel(localUsername);
        actors.Add(ActorAt(
            tileSize,
            localCenterXPx,
            localCenterYPx,
            WorldDepth.ActorSlot.LocalPlayer,
            sequence,
            localPose,
            default,
            localLabel));
        return actors;
    }

    private static void AddWorldActors(
        List<DepthActor> actors,
        ref int sequence,
        int tileSize,
        IReadOnlyDictionary<string, (float CxPx, float CyPx)>? centers,
        IReadOnlyDictionary<string, WorldSpritePose>? poses,
        WorldDepth.ActorSlot slot,
        bool includeName)
    {
        if (centers is not { Count: > 0 })
        {
            return;
        }

        foreach (var kv in centers)
        {
            var pose = WorldSpritePose.IdleDown;
            if (poses is not null && poses.TryGetValue(kv.Key, out var posed))
            {
                pose = posed;
            }

            actors.Add(ActorAt(
                tileSize,
                kv.Value.CxPx,
                kv.Value.CyPx,
                slot,
                sequence++,
                default,
                pose,
                includeName ? NameplatePainter.FormatLabel(kv.Key) : null));
        }
    }

    private static DepthActor ActorAt(
        int tileSize,
        float x,
        float y,
        WorldDepth.ActorSlot slot,
        int sequence,
        PlayerSpritePose playerPose,
        WorldSpritePose worldPose,
        string? name = null)
        => new()
        {
            Key = new WorldDepth.ActorKey(
                WorldDepth.FloorTileIndex(y, tileSize),
                (int)MathF.Round(y),
                slot,
                sequence),
            X = x,
            Y = y,
            PlayerPose = playerPose,
            WorldPose = worldPose,
            Name = name,
        };

    private static int DrawActorsWhile(
        Graphics g,
        List<DepthActor> actors,
        int index,
        Func<int, bool> rowMatches,
        PlayerSpritePose localPose,
        PaperdollOverlaySet localAppearance,
        CharacterLook localLook)
    {
        while (index < actors.Count && rowMatches(actors[index].Key.Row))
        {
            DrawDepthActor(g, actors[index], localPose, localAppearance, localLook);
            index++;
        }

        return index;
    }

    private static void DrawDepthActor(
        Graphics g,
        in DepthActor actor,
        PlayerSpritePose localPose,
        PaperdollOverlaySet localAppearance,
        CharacterLook localLook)
    {
        switch (actor.Key.Slot)
        {
            case WorldDepth.ActorSlot.Loot:
                DrawGroundLootAt(g, actor.LootX, actor.LootY);
                break;
            case WorldDepth.ActorSlot.Monster:
                WorldEntityAssets.DrawFeetAnchored(g, actor.X, actor.Y, WorldEntityKind.Monster, actor.WorldPose);
                break;
            case WorldDepth.ActorSlot.Npc:
                WorldEntityAssets.DrawFeetAnchored(g, actor.X, actor.Y, WorldEntityKind.Npc, actor.WorldPose);
                break;
            case WorldDepth.ActorSlot.RemotePlayer:
                // Pas d'équipement sur PositionUpdate : les autres restent corps + tête.
                DrawPlayerSpriteAtPixelCenter(g, actor.X, actor.Y, other: true, actor.PlayerPose);
                break;
            case WorldDepth.ActorSlot.LocalPlayer:
                DrawPlayerSpriteAtPixelCenter(g, actor.X, actor.Y, other: false, localPose, localAppearance, localLook);
                break;
        }
    }

    /// <summary>
    /// Sac coloré (jetons <see cref="UiTheme"/> déjà utilisés par l'UI). Pas de nouvel art.
    /// Centre du rectangle = ancre, pour que le pixel (PixelX, PixelY) reste la toile.
    /// </summary>
    private static void DrawGroundLootAt(Graphics g, int pixelX, int pixelY)
    {
        var previous = g.PixelOffsetMode;
        g.PixelOffsetMode = PixelOffsetMode.None;
        try
        {
            using var fill = new SolidBrush(UiTheme.AccentGoldDim);
            using var knot = new SolidBrush(UiTheme.AccentGoldHi);
            using var outline = new Pen(UiTheme.AccentGold);
            const int bagW = 14;
            const int bagH = 10;
            var body = new Rectangle(pixelX - (bagW / 2), pixelY - (bagH / 2), bagW, bagH);
            g.FillRectangle(fill, body);
            g.DrawRectangle(outline, body);
            g.FillRectangle(knot, pixelX - 2, body.Y - 3, 4, 3);
        }
        finally
        {
            g.PixelOffsetMode = previous;
        }
    }

    private static void DrawTileRow(
        Graphics g,
        Map map,
        List<Layer> layers,
        List<Layer>? attributes,
        int ty,
        int tw,
        IReadOnlyDictionary<int, Bitmap>? tilesetBitmaps,
        ITileAssetLookup? tileAssets,
        IDictionary<TileAssetId, Bitmap>? tileAssetBitmaps,
        bool showTileGrid)
    {
        if (ty < 0 || ty >= map.Height
            || (layers.Count == 0 && attributes is not { Count: > 0 } && !showTileGrid))
        {
            return;
        }

        for (var tx = 0; tx < map.Width; tx++)
        {
            var rect = new Rectangle(tx * tw, ty * tw, tw, tw);
            foreach (var layer in layers)
            {
                var tile = FindTile(layer, tx, ty);
                if (tile is null)
                {
                    continue;
                }

                if (!tile.AssetId.IsNone || map.GraphicIdentity == TileGraphicIdentity.TileAsset)
                {
                    if (!tile.AssetId.IsNone && TryDrawTileAsset(g, tile, rect, tileAssets, tileAssetBitmaps))
                    {
                        continue;
                    }

                    FillFallbackType(g, rect, tile.Type);
                    continue;
                }

                if (TryDrawGraphicTile(g, tile, rect, tw, tilesetBitmaps))
                {
                    continue;
                }

                FillFallbackType(g, rect, tile.Type);
            }

            if (attributes is { Count: > 0 })
            {
                DrawAttributeTint(g, attributes, tx, ty, rect);
            }

            if (showTileGrid)
            {
                DrawDebugTileGrid(g, rect);
            }
        }
    }

    /// <summary>
    /// Plaques après la météo : lisibles, sans nouvel art. Les monstres n’ont pas de nom.
    /// Un PNJ sprite sur la tuile remplace le libellé d’événement (une seule plaque).
    /// </summary>
    private static void DrawNameplates(
        Graphics g,
        int tileSize,
        List<DepthActor> actors,
        IReadOnlyList<MapEventWireEntry>? mapEvents,
        IReadOnlyDictionary<string, (float CxPx, float CyPx)>? npcCentersPx,
        IReadOnlyList<MapPlacedEntity>? playtestPlacedEntities,
        int mapWidth,
        int mapHeight)
    {
        if (tileSize <= 0)
        {
            return;
        }

        if (mapEvents is { Count: > 0 })
        {
            foreach (var ev in mapEvents)
            {
                if (ev.TileX < 0 || ev.TileY < 0 || ev.TileX >= mapWidth || ev.TileY >= mapHeight)
                {
                    continue;
                }

                var label = NameplatePainter.LabelForNpcMapEvent(ev.Slug, ev.DisplayName);
                if (label is null
                    || NpcSpriteOwnsTile(npcCentersPx, tileSize, ev.TileX, ev.TileY)
                    || NamedPlaytestNpcOnTile(playtestPlacedEntities, ev.TileX, ev.TileY))
                {
                    continue;
                }

                NameplatePainter.DrawAbove(
                    g,
                    ev.TileX * tileSize + (tileSize / 2f),
                    ev.TileY * tileSize,
                    label);
            }
        }

        if (playtestPlacedEntities is { Count: > 0 })
        {
            foreach (var entity in playtestPlacedEntities)
            {
                if (entity is null
                    || entity.Kind != MapPlacedKind.Npc
                    || entity.TileX < 0
                    || entity.TileY < 0
                    || entity.TileX >= mapWidth
                    || entity.TileY >= mapHeight)
                {
                    continue;
                }

                NameplatePainter.DrawAbove(
                    g,
                    entity.TileX * tileSize + (tileSize / 2f),
                    entity.TileY * tileSize,
                    entity.Name);
            }
        }

        foreach (var actor in actors)
        {
            var height = NameplateSpriteHeight(actor.Key.Slot);
            if (height <= 0 || string.IsNullOrEmpty(actor.Name))
            {
                continue;
            }

            NameplatePainter.DrawAbove(g, actor.X, actor.Y - height + 1f, actor.Name);
        }
    }

    private static int NameplateSpriteHeight(WorldDepth.ActorSlot slot) =>
        slot switch
        {
            WorldDepth.ActorSlot.Npc => WorldEntityAssets.DrawnSizePixels,
            WorldDepth.ActorSlot.RemotePlayer or WorldDepth.ActorSlot.LocalPlayer =>
                PlayerWorldAssets.NativeSize * PlayerWorldAssets.DrawScale,
            _ => 0,
        };

    private static bool NpcSpriteOwnsTile(
        IReadOnlyDictionary<string, (float CxPx, float CyPx)>? npcCentersPx,
        int tileSize,
        int tileX,
        int tileY)
    {
        if (npcCentersPx is not { Count: > 0 } || tileSize <= 0)
        {
            return false;
        }

        foreach (var kv in npcCentersPx)
        {
            if (NameplatePainter.FormatLabel(kv.Key) is null)
            {
                continue;
            }

            var tx = (int)MathF.Floor(kv.Value.CxPx / tileSize);
            var ty = (int)MathF.Floor(kv.Value.CyPx / tileSize);
            if (tx == tileX && ty == tileY)
            {
                return true;
            }
        }

        return false;
    }

    private static bool NamedPlaytestNpcOnTile(IReadOnlyList<MapPlacedEntity>? entities, int tileX, int tileY)
    {
        if (entities is not { Count: > 0 })
        {
            return false;
        }

        foreach (var entity in entities)
        {
            if (entity is not null
                && entity.Kind == MapPlacedKind.Npc
                && entity.TileX == tileX
                && entity.TileY == tileY
                && NameplatePainter.FormatLabel(entity.Name) is not null)
            {
                return true;
            }
        }

        return false;
    }

    private static void DrawAttributeTint(Graphics g, List<Layer> attributes, int tx, int ty, Rectangle rect)
    {
        foreach (var layer in attributes)
        {
            var at = FindTile(layer, tx, ty);
            if (at is null)
            {
                continue;
            }

            if (at.Type == TileType.Block)
            {
                using var b2 = new SolidBrush(Color.FromArgb(110, BlockTile));
                g.FillRectangle(b2, rect);
            }
            else if (at.Type == TileType.Warp)
            {
                using var b2 = new SolidBrush(Color.FromArgb(110, WarpTile));
                g.FillRectangle(b2, rect);
            }
        }
    }
}
