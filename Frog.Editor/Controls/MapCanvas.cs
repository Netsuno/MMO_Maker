using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using Frog.Application.Maps;
using Frog.Application.Playtest;
using Frog.Application.Prefabs;
using Frog.Core.Enums;
using Frog.Core.Models;
using Frog.Core.Protocol;
using Frog.Editor.Assets;
using Frog.Editor.Enums;
using Frog.Editor.Services;
using Frog.Editor.Ui;

namespace Frog.Editor.Controls;

/// <summary>
/// Canvas carte : vue culling pour grandes surfaces, sélection rectangle, Ctrl+C/X/V,
/// undo intégré (objectifs type RPG Maker, par étapes).
/// </summary>
public sealed class MapCanvas : Control
{
    public readonly MapUndoController History = new();

    private const int ViewportPadTiles = 1;

    public int TileSize { get; set; } = 32;
    public float Zoom { get; private set; } = 1f;
    public PointF Pan { get; private set; } = new(0, 0);

    private Map? _map;

    /// <summary>Carte affichée ; notifier les abonnés (mini-carte) lors d’un changement d’instance.</summary>
    public Map? Map
    {
        get => _map;
        set
        {
            if (ReferenceEquals(_map, value))
            {
                return;
            }

            _map = value;
            NotifyViewTransformChanged();
            Invalidate();
        }
    }

    /// <summary>Pan, zoom ou carte changés — pour synchroniser la mini-carte.</summary>
    public event Action? ViewTransformChanged;

    public int ActiveTilesetId { get; set; } = 0;
    public Point SelectedSrc { get; set; } = new(0, 0);

    /// <summary>Tampon pinceau en tuiles (largeur × hauteur), aligné sur <see cref="SelectedSrc"/> dans le tileset.</summary>
    public Size SelectedStampInTiles { get; set; } = new(1, 1);

    public int ActiveLayerIndex { get; set; } = 0;
    public event Action<Point>? HoveredTileChanged;
    public TileType SelectedTileType { get; set; } = TileType.Ground;
    public event Action<Tile?>? TileClicked;

    /// <summary>Ctrl+clic droit sur une tuile (sans gommage) — menu contextuel éditeur.</summary>
    public event Action<Point>? TileContextMenuRequested;
    public event Action? MapReplaced;
    public event Action? UndoHistoryChanged;
    /// <summary>Carte modifiée par une action d’édition (peinture, undo, etc.).</summary>
    public event Action? MapEdited;

    /// <summary>Pipette : le pinceau a échantillonné tileset + source depuis la carte.</summary>
    public event Action<BrushSample>? BrushSampled;

    public readonly record struct BrushSample(int TilesetId, int SrcX, int SrcY, TileType Type, bool SwitchToBrush);

    /// <summary>Carte cible par défaut pour les nouvelles tuiles warp (souvent la carte courante).</summary>
    public Guid? DefaultWarpTargetMapId { get; set; }

    /// <summary>Marqueurs événements ou visibilité overlay ont changé (mini-carte, etc.).</summary>
    public event Action? MapEventOverlayChanged;

    private bool _showMapEventMarkers = true;

    /// <summary>Affiche les losanges d’événements placés (<see cref="MapEventMarkers"/>) sur le canevas.</summary>
    public bool ShowMapEventMarkers
    {
        get => _showMapEventMarkers;
        set
        {
            if (_showMapEventMarkers == value)
            {
                return;
            }

            _showMapEventMarkers = value;
            if (!value)
            {
                _hoveredMapEventMarker = null;
            }

            MapEventOverlayChanged?.Invoke();
            MapEventMarkerInteractionChanged?.Invoke();
            Invalidate();
        }
    }

    private bool _showMapEventNames = true;

    /// <summary>Affiche le nom du type d’événement (zoom suffisant, survol ou sélection). Défaut : oui.</summary>
    public bool ShowMapEventNames
    {
        get => _showMapEventNames;
        set
        {
            if (_showMapEventNames == value)
            {
                return;
            }

            _showMapEventNames = value;
            Invalidate();
        }
    }

    private IReadOnlyList<MapEventMarkerView>? _mapEventMarkers;

    /// <summary>Marqueurs agrégés par tuile (null = aucun overlay).</summary>
    public IReadOnlyList<MapEventMarkerView>? MapEventMarkers
    {
        get => _mapEventMarkers;
        set
        {
            _mapEventMarkers = value;
            var interaction = false;
            if (_hasSelectedMapEvent && !MarkerListContainsSelection(value))
            {
                _hasSelectedMapEvent = false;
                _selectedMapEventPlacementKey = "";
                interaction = true;
            }

            if (_hoveredMapEventMarker is { } hovered && !MarkerListContains(value, hovered))
            {
                _hoveredMapEventMarker = null;
                interaction = true;
            }

            MapEventOverlayChanged?.Invoke();
            if (interaction)
            {
                MapEventMarkerInteractionChanged?.Invoke();
            }

            Invalidate();
        }
    }

    /// <summary>Clic sur un losange ou son libellé (sélection canevas déjà appliquée).</summary>
    public event Action<MapEventMarkerView>? MapEventMarkerPicked;

    /// <summary>Survol, sélection ou visibilité des noms ont changé (barre d’état).</summary>
    public event Action? MapEventMarkerInteractionChanged;

    private MapEventMarkerView? _hoveredMapEventMarker;
    private bool _hasSelectedMapEvent;
    private int _selectedMapEventTileX;
    private int _selectedMapEventTileY;
    private string _selectedMapEventPlacementKey = "";
    private bool _mapEventMarkerGesture;

    /// <summary>Nom tronqué de l’événement survolé, sinon de l’événement sélectionné.</summary>
    public string? ActiveMapEventCaption
    {
        get
        {
            if (!ShowMapEventMarkers)
            {
                return null;
            }

            if (_hoveredMapEventMarker is { } hovered)
            {
                return MapEventMarkerLayout.FormatLabel(hovered.PrimaryDisplayName, hovered.PrimarySlug, hovered.PlacementCount);
            }

            if (_hasSelectedMapEvent && TryFindSelectedMarker(out var selected))
            {
                return MapEventMarkerLayout.FormatLabel(selected.PrimaryDisplayName, selected.PrimarySlug, selected.PlacementCount);
            }

            return null;
        }
    }

    /// <summary>Met en évidence un placement (liste événements) sans redéclencher <see cref="MapEventMarkerPicked"/>.</summary>
    public void HighlightMapEventMarker(int tileX, int tileY, string? placementKey)
    {
        _hasSelectedMapEvent = true;
        _selectedMapEventTileX = tileX;
        _selectedMapEventTileY = tileY;
        _selectedMapEventPlacementKey = placementKey ?? "";
        Invalidate();
        MapEventMarkerInteractionChanged?.Invoke();
    }

    public EditorTool ActiveTool { get; set; } = EditorTool.Brush;

    /// <summary>Tuile de spawn playtest / départ affichée sur le canevas (mémo éditeur).</summary>
    public Point? PlaytestSpawnTile { get; private set; }

    /// <summary>Le spawn a été posé ou restauré (UI / workstate).</summary>
    public event Action<Point>? PlaytestSpawnChanged;

    /// <summary>Catalogue utilisé pour poser / dessiner les prefabs.</summary>
    public PrefabCatalog PrefabCatalog { get; set; } = BuiltInPrefabCatalog.Create();

    public string SelectedPrefabId { get; set; } = BuiltInPrefabCatalog.SofaId;

    public PrefabFacing SelectedPrefabFacing { get; set; } = PrefabFacing.South;

    private readonly List<PrefabPlacement> _prefabPlacements = new();

    public IReadOnlyList<PrefabPlacement> PrefabPlacements => _prefabPlacements;

    /// <summary>Placements prefab ajoutés, retirés ou remplacés.</summary>
    public event Action? PrefabPlacementsChanged;

    /// <summary>Pipette : l’outil prefab a copié id / facing depuis une instance.</summary>
    public event Action<string, PrefabFacing>? PrefabSelectionPicked;

    private PrefabPlacement? _draggingPrefab;
    private bool _prefabDragMoved;

    private bool _panning;
    private Point _lastMouse;
    private bool _paintStroke;
    private Point? _rectPaintOrigin;
    private Point _hoverTile;

    /// <summary>Dernière tuile sous le curseur (coordonnées carte).</summary>
    public Point HoveredTile => _hoverTile;
    private Point? _selectionMarqueeAnchor;
    private Rectangle? _committedSelectionTiles;

    /// <summary>Bloque le gommage au clic droit tant que le bouton n’est pas relâché (Ctrl+clic droit = menu).</summary>
    private bool _suppressRightButtonErase;

    public MapCanvas()
    {
        TabStop = true;
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = EditorChrome.MapCanvasBg;
        Cursor = Cursors.Cross;
        Dock = DockStyle.Fill;

        MouseWheel += OnMouseWheelZoom;
        MouseDown += OnMouseDown;
        MouseMove += OnMouseMove;
        MouseUp += OnMouseUp;
        MouseLeave += (_, _) => ClearMapEventMarkerHover();
    }

    /// <summary>Coin haut-gauche et coin bas-droit visibles, en coordonnées « monde » (pixels carte avant zoom).</summary>
    public void GetViewportWorldBounds(out PointF topLeft, out PointF bottomRight)
    {
        var c = ClientSize;
        topLeft = ScreenToWorld(Point.Empty);
        bottomRight = ScreenToWorld(new Point(c.Width, c.Height));
    }

    /// <summary>Tuiles visibles (indices carte), avec marge <see cref="ViewportPadTiles"/>.</summary>
    public void GetViewportTileBounds(out int tx0, out int ty0, out int tx1, out int ty1)
        => ComputeVisibleTileRange(out tx0, out ty0, out tx1, out ty1);

    /// <summary>Centre la vue sur le centre de la tuile (<paramref name="tileX"/>, <paramref name="tileY"/>).</summary>
    public void CenterViewOnTile(int tileX, int tileY)
    {
        if (Map is null)
        {
            return;
        }

        tileX = Math.Clamp(tileX, 0, Map.Width - 1);
        tileY = Math.Clamp(tileY, 0, Map.Height - 1);
        var wx = tileX * TileSize + TileSize * 0.5f;
        var wy = tileY * TileSize + TileSize * 0.5f;
        var cx = ClientSize.Width * 0.5f;
        var cy = ClientSize.Height * 0.5f;
        Pan = new PointF(cx - wx * Zoom, cy - wy * Zoom);
        NotifyViewTransformChanged();
        Invalidate();
    }

    private void NotifyViewTransformChanged() => ViewTransformChanged?.Invoke();

    /// <summary>Zoom 100 % et coin haut-gauche de la carte aligné sur le coin haut-gauche du canevas.</summary>
    public void ResetViewTransform()
    {
        Zoom = 1f;
        Pan = new PointF(0f, 0f);
        NotifyViewTransformChanged();
        Invalidate();
    }

    protected override void OnResize(EventArgs e)
    {
        base.OnResize(e);
        NotifyViewTransformChanged();
    }

    public bool HasCommittedSelection => _committedSelectionTiles is { Width: > 0, Height: > 0 };

    public void ClearSelection()
    {
        _selectionMarqueeAnchor = null;
        _committedSelectionTiles = null;
        Invalidate();
    }

    public bool TryCopyTileSelection()
    {
        if (Map is null || !TryGetCommittedSelectionNormalized(out var rect))
        {
            return false;
        }

        EditorTileClipboard.CopyFromLayer(Map, ActiveLayerIndex, rect);
        return EditorTileClipboard.HasContent;
    }

    public bool TryCutTileSelection()
    {
        if (Map is null || !TryGetCommittedSelectionNormalized(out var rect) || !IsActiveLayerEditable())
        {
            return false;
        }

        EditorTileClipboard.CopyFromLayer(Map, ActiveLayerIndex, rect);
        BeginEditTransaction();
        DeleteTilesInRectangle(rect);
        Invalidate();
        return true;
    }

    public bool TryPasteAtHover()
    {
        if (Map is null || !EditorTileClipboard.HasContent || !IsActiveLayerEditable())
        {
            return false;
        }

        EnsureLayerExists();
        BeginEditTransaction();
        var n = EditorTileClipboard.PasteToLayer(Map, ActiveLayerIndex, _hoverTile.X, _hoverTile.Y, Map.Width, Map.Height);
        Invalidate();
        RaiseTileClicked(_hoverTile.X, _hoverTile.Y);
        return n > 0;
    }

    public bool TryDeleteSelectedTiles()
    {
        if (Map is null || !TryGetCommittedSelectionNormalized(out var rect) || !IsActiveLayerEditable())
        {
            return false;
        }

        BeginEditTransaction();
        DeleteTilesInRectangle(rect);
        Invalidate();
        return true;
    }

    public bool HandleEditorShortcuts(Keys keyData)
    {
        var ctrl = (keyData & Keys.Control) == Keys.Control;
        var alt = (keyData & Keys.Alt) == Keys.Alt;
        var shift = (keyData & Keys.Shift) == Keys.Shift;
        var code = keyData & Keys.KeyCode;

        switch (code)
        {
            case Keys.C when ctrl && !alt:
                return TryCopyTileSelection();
            case Keys.X when ctrl && !alt:
                return TryCutTileSelection();
            case Keys.V when ctrl && !alt:
                return TryPasteAtHover();
            case Keys.V when !ctrl && !alt && !shift:
                return TryTransformSelection(TileSelectionTransformKind.MirrorVertical);
            case Keys.Delete when !ctrl && !alt:
                return TryDeleteSelectedTiles();
            case Keys.Q when !ctrl && !alt && !shift:
                return TryTransformSelection(TileSelectionTransformKind.Rotate90Clockwise);
            case Keys.H when !ctrl && !alt && !shift:
                return TryTransformSelection(TileSelectionTransformKind.MirrorHorizontal);
            case Keys.I when !ctrl && !alt && !shift:
                return TryPipetteAtHover(switchToBrush: true);
            default:
                return false;
        }
    }

    public bool TryTransformSelection(TileSelectionTransformKind kind)
    {
        if (Map is null)
        {
            return false;
        }

        if (TryGetCommittedSelectionNormalized(out var rect)
            && IsActiveLayerEditable()
            && MapEditOperations.CountTilesInRect(Map, ActiveLayerIndex, rect.X, rect.Y, rect.Width, rect.Height) > 0)
        {
            BeginEditTransaction();
            if (!TileSelectionService.TryTransform(Map, ActiveLayerIndex, rect, kind, out var next))
            {
                return false;
            }

            _committedSelectionTiles = next;
            Invalidate();
            return true;
        }

        if (!EditorTileClipboard.TryTransform(kind))
        {
            return false;
        }

        Invalidate();
        return true;
    }

    public bool TryPipetteAtHover(bool switchToBrush) => TryPipetteAt(_hoverTile.X, _hoverTile.Y, switchToBrush);

    public bool TryPipetteAt(int tileX, int tileY, bool switchToBrush)
    {
        if (Map is null)
        {
            return false;
        }

        if (!MapTilePipette.TrySample(Map, tileX, tileY, ActiveLayerIndex, out var sample))
        {
            return false;
        }

        ActiveTilesetId = sample.TilesetId;
        SelectedSrc = new Point(sample.SrcX, sample.SrcY);
        SelectedStampInTiles = new Size(1, 1);
        SelectedTileType = sample.Type;
        if (switchToBrush)
        {
            ActiveTool = EditorTool.Brush;
        }

        BrushSampled?.Invoke(new BrushSample(sample.TilesetId, sample.SrcX, sample.SrcY, sample.Type, switchToBrush));
        Invalidate();
        return true;
    }

    public void PerformUndo()
    {
        if (Map is null)
        {
            return;
        }

        var restored = History.TryUndo(Map);
        if (restored is null)
        {
            return;
        }

        Map = restored;
        MapEdited?.Invoke();
        MapReplaced?.Invoke();
        Invalidate();
    }

    public void PerformRedo()
    {
        if (Map is null)
        {
            return;
        }

        var restored = History.TryRedo(Map);
        if (restored is null)
        {
            return;
        }

        Map = restored;
        MapEdited?.Invoke();
        MapReplaced?.Invoke();
        Invalidate();
    }

    public void ClearHistory() => History.Clear();

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.Clear(BackColor);

        ComputeVisibleTileRange(out var tx0, out var ty0, out var tx1, out var ty1);

        var mw = Math.Max(1, Map?.Width ?? 20);
        var mh = Math.Max(1, Map?.Height ?? 15);

        var state = g.Save();
        try
        {
            g.TranslateTransform(Pan.X, Pan.Y);
            g.ScaleTransform(Zoom, Zoom);

            DrawGridCells(g, mw, mh, tx0, ty0, tx1, ty1);

            if (Map is not null)
            {
                foreach (var layer in Map.Layers)
                {
                    if (!layer.Visible)
                    {
                        continue;
                    }

                    DrawLayer(g, layer, tx0, ty0, tx1, ty1);
                }

                DrawPlacedPrefabs(g);
                DrawTileTypeOverlay(g, tx0, ty0, tx1, ty1);
                DrawMapEventMarkerOverlay(g, tx0, ty0, tx1, ty1);
                DrawPlaytestSpawnMarker(g, tx0, ty0, tx1, ty1);
            }

            if (Map is not null && ActiveTool == EditorTool.Spawn)
            {
                DrawTileRectPixels(g, _hoverTile.X, _hoverTile.Y, _hoverTile.X, _hoverTile.Y, Color.DeepSkyBlue, dash: true);
            }

            if (Map is not null && ActiveTool == EditorTool.Prefab)
            {
                DrawPrefabGhost(g, mw, mh);
            }

            if (ActiveTool == EditorTool.Selection && Map is not null && _selectionMarqueeAnchor is { } sa)
            {
                var xa = Math.Min(sa.X, _hoverTile.X);
                var ya = Math.Min(sa.Y, _hoverTile.Y);
                var xb = Math.Max(sa.X, _hoverTile.X);
                var yb = Math.Max(sa.Y, _hoverTile.Y);
                DrawTileRectPixels(g, xa, ya, xb, yb, Color.LimeGreen, dash: true);
            }

            if (Map is not null && _committedSelectionTiles is { Width: > 0, Height: > 0 } sel)
            {
                DrawTileRectPixels(g,
                    sel.Left,
                    sel.Top,
                    sel.Left + sel.Width - 1,
                    sel.Top + sel.Height - 1,
                    Color.LightGreen,
                    dash: true);
            }

            if (Map is not null && ActiveTool == EditorTool.Rectangle && _rectPaintOrigin is { } ro)
            {
                DrawTileRectPixels(g, ro.X, ro.Y, _hoverTile.X, _hoverTile.Y, Color.Cyan, dash: false);
            }

            if (BrushGhostVisible() && TilesetCache.TryGet(ActiveTilesetId, out var bmpG) && bmpG is not null)
            {
                var tx = _hoverTile.X;
                var ty = _hoverTile.Y;
                var ts = TileSize;
                var sw = Math.Max(1, SelectedStampInTiles.Width);
                var sh = Math.Max(1, SelectedStampInTiles.Height);
                using var attrs = new System.Drawing.Imaging.ImageAttributes();
                attrs.SetColorMatrix(
                    new System.Drawing.Imaging.ColorMatrix { Matrix33 = 0.45f },
                    System.Drawing.Imaging.ColorMatrixFlag.Default,
                    System.Drawing.Imaging.ColorAdjustType.Bitmap);
                for (var dy = 0; dy < sh; dy++)
                {
                    for (var dx = 0; dx < sw; dx++)
                    {
                        var sx = SelectedSrc.X + dx * ts;
                        var sy = SelectedSrc.Y + dy * ts;
                        if (sx < 0 || sy < 0 || sx + ts > bmpG.Width || sy + ts > bmpG.Height)
                        {
                            continue;
                        }

                        var mtx = tx + dx;
                        var mty = ty + dy;
                        if (mtx < 0 || mty < 0 || mtx >= mw || mty >= mh)
                        {
                            continue;
                        }

                        var dst = new Rectangle(mtx * ts, mty * ts, ts, ts);
                        g.DrawImage(bmpG, dst, sx, sy, ts, ts, GraphicsUnit.Pixel, attrs);
                    }
                }
            }
        }
        finally
        {
            g.Restore(state);
        }
    }

    private bool BrushGhostVisible() =>
        Map is not null && ActiveTilesetId > 0 && IsActiveLayerEditable() &&
        ActiveTool is EditorTool.Brush or EditorTool.Rectangle or EditorTool.Fill;

    private void DrawGridCells(Graphics g, int mapW, int mapH, int tx0, int ty0, int tx1, int ty1)
    {
        var ts = TileSize;
        using var penMajor = new Pen(Color.FromArgb(92, 98, 108), 1f);
        using var penMinor = new Pen(Color.FromArgb(58, 62, 74), 1f);
        using var light = new SolidBrush(Color.FromArgb(48, 52, 60));
        using var dark = new SolidBrush(Color.FromArgb(54, 58, 66));

        var yTop = ty0 * ts;
        var yBot = Math.Min(mapH * ts, (ty1 + 1) * ts);
        var xLeft = tx0 * ts;
        var xRight = Math.Min(mapW * ts, (tx1 + 1) * ts);

        for (var y = ty0; y <= ty1 && y < mapH; y++)
        {
            for (var x = tx0; x <= tx1 && x < mapW; x++)
            {
                var r = new Rectangle(x * ts, y * ts, ts, ts);
                g.FillRectangle(((x + y) % 2 == 0) ? light : dark, r);
            }
        }

        for (var x = tx0; x <= mapW && x <= tx1 + 1; x++)
        {
            var px = x * ts;
            g.DrawLine(penMinor, px, yTop, px, yBot);
        }

        for (var y = ty0; y <= mapH && y <= ty1 + 1; y++)
        {
            var py = y * ts;
            g.DrawLine(penMinor, xLeft, py, xRight, py);
        }

        g.DrawRectangle(penMajor, 0, 0, mapW * ts, mapH * ts);
    }

    private void DrawLayer(Graphics g, Layer layer, int tx0, int ty0, int tx1, int ty1)
    {
        foreach (var t in layer.Tiles)
        {
            if (t.X < tx0 || t.X > tx1 || t.Y < ty0 || t.Y > ty1)
            {
                continue;
            }

            if (!TilesetCache.TryGet(t.TilesetId, out var bmp) || bmp is null)
            {
                continue;
            }

            var src = new Rectangle(t.SrcX, t.SrcY, TileSize, TileSize);
            var dst = new Rectangle(t.X * TileSize, t.Y * TileSize, TileSize, TileSize);
            if (src.Right > bmp.Width || src.Bottom > bmp.Height)
            {
                continue;
            }

            g.DrawImage(bmp, dst, src, GraphicsUnit.Pixel);
        }
    }

    private void DrawTileTypeOverlay(Graphics g, int tx0, int ty0, int tx1, int ty1)
    {
        if (Map is null)
        {
            return;
        }

        foreach (var layer in Map.Layers)
        {
            if (!layer.Visible)
            {
                continue;
            }

            foreach (var t in layer.Tiles)
            {
                if (t.X < tx0 || t.X > tx1 || t.Y < ty0 || t.Y > ty1)
                {
                    continue;
                }

                var rect = new Rectangle(t.X * TileSize, t.Y * TileSize, TileSize, TileSize);
                switch (t.Type)
                {
                    case TileType.Block:
                        using (var b = new SolidBrush(Color.FromArgb(80, Color.Red)))
                        {
                            g.FillRectangle(b, rect);
                        }

                        break;

                    case TileType.Warp:
                    {
                        using var p = new Pen(Color.Lime, 2);
                        g.DrawRectangle(p, rect);
                        break;
                    }

                    case TileType.Resource:
                        using (var br = new SolidBrush(Color.FromArgb(160, Color.Gold)))
                        {
                            var cx = rect.X + TileSize / 4;
                            var cy = rect.Y + TileSize / 4;
                            var d = TileSize / 2;
                            g.FillEllipse(br, cx, cy, d, d);
                        }

                        break;
                }
            }
        }
    }

    private static void FillDiamond(Graphics g, Brush brush, Rectangle r)
    {
        var pts = new[]
        {
            new Point(r.X + r.Width / 2, r.Y),
            new Point(r.Right, r.Y + r.Height / 2),
            new Point(r.X + r.Width / 2, r.Bottom),
            new Point(r.Left, r.Y + r.Height / 2),
        };
        g.FillPolygon(brush, pts);
    }

    private static void DrawDiamond(Graphics g, Pen pen, Rectangle r)
    {
        var pts = new[]
        {
            new Point(r.X + r.Width / 2, r.Y),
            new Point(r.Right, r.Y + r.Height / 2),
            new Point(r.X + r.Width / 2, r.Bottom),
            new Point(r.Left, r.Y + r.Height / 2),
        };
        g.DrawPolygon(pen, pts);
    }

    private void DrawMapEventMarkerOverlay(Graphics g, int tx0, int ty0, int tx1, int ty1)
    {
        if (!ShowMapEventMarkers || _mapEventMarkers is null || Map is null || _mapEventMarkers.Count == 0)
        {
            return;
        }

        var ts = TileSize;
        var visible = new List<MapEventMarkerView>();
        foreach (var m in _mapEventMarkers)
        {
            if (m.TileX < tx0 || m.TileX > tx1 || m.TileY < ty0 || m.TileY > ty1)
            {
                continue;
            }

            if (m.TileX < 0 || m.TileX >= Map.Width || m.TileY < 0 || m.TileY >= Map.Height)
            {
                continue;
            }

            visible.Add(m);
        }

        if (visible.Count == 0)
        {
            return;
        }

        var prevSmooth = g.SmoothingMode;
        var prevText = g.TextRenderingHint;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        try
        {
            foreach (var m in visible)
            {
                DrawMapEventMarkerBody(g, m, ts);
            }

            foreach (var m in visible)
            {
                if (!ShouldDrawNameFor(m))
                {
                    continue;
                }

                DrawMapEventMarkerName(g, m, ts);
            }
        }
        finally
        {
            g.SmoothingMode = prevSmooth;
            g.TextRenderingHint = prevText;
        }
    }

    private void DrawMapEventMarkerBody(Graphics g, MapEventMarkerView marker, int ts)
    {
        var selected = MarkerMatchesSelection(marker);
        var hovered = _hoveredMapEventMarker is { } h && h.Equals(marker);
        var fill = MapEventMarkerColors.TintFromSlug(marker.PrimarySlug);
        var tile = new Rectangle(marker.TileX * ts, marker.TileY * ts, ts, ts);
        var washAlpha = selected ? 92 : hovered ? 68 : 42;
        using (var wash = new SolidBrush(Color.FromArgb(washAlpha, fill)))
        {
            g.FillRectangle(wash, tile);
        }

        var diamond = MapEventMarkerLayout.DiamondBounds(marker.TileX, marker.TileY, ts);
        var fillAlpha = selected || hovered ? 230 : 200;
        using (var brush = new SolidBrush(Color.FromArgb(fillAlpha, fill)))
        {
            FillDiamond(g, brush, diamond);
        }

        var autorun = MapEventMarkerColors.IsAutorunTrigger(marker.PrimaryTriggerKind);
        var parallel = MapEventMarkerColors.IsParallelTrigger(marker.PrimaryTriggerKind);
        var legacyPage = MapEventMarkerColors.IsLegacyPageTrigger(marker.PrimaryTriggerKind);
        var penWidth = selected ? Math.Max(2.2f, ts / 10f) : Math.Max(1.4f, ts / 16f);
        var edgeColor = selected ? Color.Gold : Color.White;
        using (var shadow = new Pen(Color.FromArgb(190, 16, 14, 22), penWidth + 1.6f))
        {
            DrawDiamond(g, shadow, diamond);
        }

        using (var edge = new Pen(Color.FromArgb(235, edgeColor), penWidth))
        {
            if (autorun)
            {
                edge.DashStyle = DashStyle.Dash;
            }

            DrawDiamond(g, edge, diamond);
        }

        if (parallel)
        {
            var inner = InsetRectangle(diamond, Math.Max(2, diamond.Width / 5));
            using var innerPen = new Pen(Color.FromArgb(230, edgeColor), Math.Max(1f, penWidth * 0.65f));
            DrawDiamond(g, innerPen, inner);
        }
        else if (legacyPage)
        {
            var r = Math.Max(2f, ts * 0.08f);
            var cx = diamond.X + diamond.Width / 2f;
            var cy = diamond.Y + diamond.Height / 2f;
            using var dot = new SolidBrush(Color.FromArgb(230, Color.White));
            g.FillEllipse(dot, cx - r, cy - r, r * 2f, r * 2f);
        }

        if (marker.PlacementCount > 1 && !ShouldDrawNameFor(marker))
        {
            var label = marker.PlacementCount > 9 ? "9+" : marker.PlacementCount.ToString();
            using var f = new Font(Font.FontFamily, Math.Max(6f, ts * 0.28f), FontStyle.Bold, GraphicsUnit.Pixel);
            using var tb = new SolidBrush(Color.White);
            using var sf = new StringFormat
            {
                Alignment = StringAlignment.Center,
                LineAlignment = StringAlignment.Center,
            };
            g.DrawString(label, f, tb, diamond, sf);
        }
    }

    private void DrawMapEventMarkerName(Graphics g, MapEventMarkerView marker, int ts)
    {
        var label = MapEventMarkerLayout.FormatLabel(marker.PrimaryDisplayName, marker.PrimarySlug, marker.PlacementCount);
        var bounds = MapEventMarkerLayout.NameLabelBounds(marker.TileX, marker.TileY, ts, label);
        var fill = MapEventMarkerColors.TintFromSlug(marker.PrimarySlug);
        using (var bg = new SolidBrush(Color.FromArgb(225, 16, 18, 24)))
        {
            g.FillRectangle(bg, bounds);
        }

        var accentW = Math.Max(2f, ts / 12f);
        using (var accent = new SolidBrush(fill))
        {
            g.FillRectangle(accent, bounds.X, bounds.Y, accentW, bounds.Height);
        }

        if (MarkerMatchesSelection(marker))
        {
            using var gold = new Pen(Color.Gold, Math.Max(1f, ts / 18f));
            g.DrawRectangle(gold, bounds.X, bounds.Y, bounds.Width, bounds.Height);
        }

        var textBounds = new RectangleF(bounds.X + accentW + 1f, bounds.Y, Math.Max(1f, bounds.Width - accentW - 2f), bounds.Height);
        using var font = new Font(Font.FontFamily, Math.Max(7f, bounds.Height * 0.62f), FontStyle.Bold, GraphicsUnit.Pixel);
        using var tb = new SolidBrush(Color.White);
        using var sf = new StringFormat
        {
            Alignment = StringAlignment.Center,
            LineAlignment = StringAlignment.Center,
            Trimming = StringTrimming.EllipsisCharacter,
            FormatFlags = StringFormatFlags.NoWrap,
        };
        g.DrawString(label, font, tb, textBounds, sf);
    }

    private static Rectangle InsetRectangle(Rectangle r, int inset)
    {
        var side = Math.Max(4, Math.Min(r.Width, r.Height) - inset * 2);
        var x = r.X + (r.Width - side) / 2;
        var y = r.Y + (r.Height - side) / 2;
        return new Rectangle(x, y, side, side);
    }

    private void DrawPlaytestSpawnMarker(Graphics g, int tx0, int ty0, int tx1, int ty1)
    {
        if (Map is null || PlaytestSpawnTile is not { } sp)
        {
            return;
        }

        if (sp.X < tx0 || sp.X > tx1 || sp.Y < ty0 || sp.Y > ty1)
        {
            return;
        }

        if (sp.X < 0 || sp.X >= Map.Width || sp.Y < 0 || sp.Y >= Map.Height)
        {
            return;
        }

        var ts = TileSize;
        var rect = new Rectangle(sp.X * ts, sp.Y * ts, ts, ts);
        var accent = Color.FromArgb(255, 80, 200, 255);
        using (var fill = new SolidBrush(Color.FromArgb(70, accent)))
        using (var pen = new Pen(accent, Math.Max(1.5f, ts / 16f)) { DashStyle = DashStyle.Dash })
        {
            g.FillRectangle(fill, rect);
            g.DrawRectangle(pen, rect);
        }

        var inset = Math.Max(3, ts / 6);
        var diamond = Rectangle.Inflate(rect, -inset, -inset);
        var prev = g.SmoothingMode;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        try
        {
            using (var brush = new SolidBrush(Color.FromArgb(210, accent)))
            {
                FillDiamond(g, brush, diamond);
            }

            using var edge = new Pen(Color.White, Math.Max(1f, ts / 20f));
            DrawDiamond(g, edge, diamond);
        }
        finally
        {
            g.SmoothingMode = prev;
        }
    }

    public bool TrySetPlaytestSpawn(int tileX, int tileY)
    {
        if (Map is null || !MapPlaytestSpawn.TryClamp(Map, tileX, tileY, out var x, out var y))
        {
            return false;
        }

        var next = new Point(x, y);
        if (PlaytestSpawnTile == next)
        {
            return true;
        }

        PlaytestSpawnTile = next;
        PlaytestSpawnChanged?.Invoke(next);
        Invalidate();
        return true;
    }

    public void ClearPlaytestSpawn()
    {
        if (PlaytestSpawnTile is null)
        {
            return;
        }

        PlaytestSpawnTile = null;
        Invalidate();
    }

    internal bool TryHandleSpawnToolRightClickForTest(int tileX, int tileY, bool control)
    {
        if (Map is null || tileX < 0 || tileY < 0 || tileX >= Map.Width || tileY >= Map.Height)
        {
            return false;
        }

        ActiveTool = EditorTool.Spawn;
        if (control)
        {
            _suppressRightButtonErase = true;
            TileContextMenuRequested?.Invoke(new Point(tileX, tileY));
            return true;
        }

        return false;
    }

    internal bool TryApplySpawnToolAtTileForTest(int tileX, int tileY)
    {
        if (Map is null)
        {
            return false;
        }

        ActiveTool = EditorTool.Spawn;
        return TrySetPlaytestSpawn(tileX, tileY);
    }

    public void ReplacePrefabPlacements(IEnumerable<PrefabPlacement>? placements)
    {
        _prefabPlacements.Clear();
        _prefabPlacements.AddRange(PrefabPlacementService.ClonePlacements(placements));
        PrefabPlacementsChanged?.Invoke();
        Invalidate();
    }

    public bool TryPlaceSelectedPrefab(int tileX, int tileY)
    {
        if (Map is null)
        {
            return false;
        }

        if (_draggingPrefab is not null)
        {
            return false;
        }

        var before = _prefabPlacements.Count;
        var ok = PrefabPlacementService.TryPlace(
            _prefabPlacements,
            PrefabCatalog,
            SelectedPrefabId,
            SelectedPrefabFacing,
            tileX,
            tileY,
            Map.Width,
            Map.Height,
            out _,
            out _);
        if (ok)
        {
            PrefabPlacementsChanged?.Invoke();
            Invalidate();
        }

        return ok || _prefabPlacements.Count != before;
    }

    public int TryErasePrefabAt(int tileX, int tileY)
    {
        var removed = PrefabPlacementService.EraseAt(_prefabPlacements, PrefabCatalog, tileX, tileY);
        if (removed > 0)
        {
            PrefabPlacementsChanged?.Invoke();
            Invalidate();
        }

        return removed;
    }

    public bool TryPipettePrefabAt(int tileX, int tileY)
    {
        var hit = PrefabPlacementService.TryFindAt(_prefabPlacements, PrefabCatalog, tileX, tileY);
        if (hit is null)
        {
            return false;
        }

        SelectedPrefabId = hit.PrefabId;
        SelectedPrefabFacing = hit.Facing;
        PrefabSelectionPicked?.Invoke(hit.PrefabId, hit.Facing);
        return true;
    }

    public bool TryBeginPrefabMoveAt(int tileX, int tileY)
    {
        var hit = PrefabPlacementService.TryFindAt(_prefabPlacements, PrefabCatalog, tileX, tileY);
        if (hit is null)
        {
            return false;
        }

        _draggingPrefab = hit;
        _prefabDragMoved = false;
        return true;
    }

    public bool TryMoveDraggingPrefabTo(int tileX, int tileY)
    {
        if (Map is null || _draggingPrefab is null)
        {
            return false;
        }

        if (!PrefabPlacementService.TryMove(
                _prefabPlacements,
                PrefabCatalog,
                _draggingPrefab,
                tileX,
                tileY,
                Map.Width,
                Map.Height,
                out _))
        {
            return false;
        }

        _prefabDragMoved = true;
        Invalidate();
        return true;
    }

    public void EndPrefabMove()
    {
        if (_draggingPrefab is null)
        {
            return;
        }

        _draggingPrefab = null;
        if (_prefabDragMoved)
        {
            PrefabPlacementsChanged?.Invoke();
        }

        _prefabDragMoved = false;
        Invalidate();
    }

    internal bool TryApplyPrefabToolAtTileForTest(int tileX, int tileY)
    {
        if (Map is null)
        {
            return false;
        }

        ActiveTool = EditorTool.Prefab;
        return TryPlaceSelectedPrefab(tileX, tileY);
    }

    internal int TryErasePrefabAtForTest(int tileX, int tileY)
    {
        ActiveTool = EditorTool.Prefab;
        return TryErasePrefabAt(tileX, tileY);
    }

    private void DrawPlacedPrefabs(Graphics g)
    {
        if (Map is null || _prefabPlacements.Count == 0)
        {
            return;
        }

        var ts = TileSize;
        foreach (var placement in _prefabPlacements)
        {
            if (!PrefabPlacementService.TryGetDefinition(PrefabCatalog, placement.PrefabId, out var definition)
                || !PrefabPlacementService.TryResolveVariant(definition, placement.Facing, out var variant)
                || !PrefabPlacementService.TryResolveFootprint(definition, variant, out var w, out var h))
            {
                continue;
            }

            var dest = new Rectangle(placement.TileX * ts, placement.TileY * ts, w * ts, h * ts);
            if (PrefabSpriteCache.TryGet(variant.SpriteFileName, out var bmp) && bmp is not null)
            {
                g.DrawImage(bmp, dest, new Rectangle(0, 0, bmp.Width, bmp.Height), GraphicsUnit.Pixel);
            }
            else
            {
                using var fill = new SolidBrush(Color.FromArgb(170, 168, 92, 58));
                using var pen = new Pen(Color.FromArgb(230, 80, 48, 28), 2f);
                g.FillRectangle(fill, dest);
                g.DrawRectangle(pen, dest);
            }
        }
    }

    private void DrawPrefabGhost(Graphics g, int mapW, int mapH)
    {
        if (!PrefabPlacementService.TryGetDefinition(PrefabCatalog, SelectedPrefabId, out var definition)
            || !PrefabPlacementService.TryResolveVariant(definition, SelectedPrefabFacing, out var variant)
            || !PrefabPlacementService.TryResolveFootprint(definition, variant, out var w, out var h))
        {
            DrawTileRectPixels(g, _hoverTile.X, _hoverTile.Y, _hoverTile.X, _hoverTile.Y, Color.SandyBrown, dash: true);
            return;
        }

        var x1 = Math.Min(mapW - 1, _hoverTile.X + w - 1);
        var y1 = Math.Min(mapH - 1, _hoverTile.Y + h - 1);
        DrawTileRectPixels(g, _hoverTile.X, _hoverTile.Y, x1, y1, Color.SandyBrown, dash: true);

        if (PrefabSpriteCache.TryGet(variant.SpriteFileName, out var bmp) && bmp is not null)
        {
            var ts = TileSize;
            var dest = new Rectangle(_hoverTile.X * ts, _hoverTile.Y * ts, w * ts, h * ts);
            using var attrs = new System.Drawing.Imaging.ImageAttributes();
            attrs.SetColorMatrix(
                new System.Drawing.Imaging.ColorMatrix { Matrix33 = 0.45f },
                System.Drawing.Imaging.ColorMatrixFlag.Default,
                System.Drawing.Imaging.ColorAdjustType.Bitmap);
            g.DrawImage(bmp, dest, 0, 0, bmp.Width, bmp.Height, GraphicsUnit.Pixel, attrs);
        }
    }

    private void DrawTileRectPixels(Graphics g, int ax, int ay, int bx, int by, Color color, bool dash)
    {
        var x0 = Math.Min(ax, bx);
        var y0 = Math.Min(ay, by);
        var x1 = Math.Max(ax, bx);
        var y1 = Math.Max(ay, by);
        var ts = TileSize;
        var r = new Rectangle(x0 * ts, y0 * ts, (x1 - x0 + 1) * ts, (y1 - y0 + 1) * ts);
        using var b = new SolidBrush(Color.FromArgb(dash ? 50 : 55, color));
        using var p = new Pen(color, 2) { DashStyle = dash ? DashStyle.Dash : DashStyle.Solid };
        g.FillRectangle(b, r);
        g.DrawRectangle(p, r);
    }

    private void ComputeVisibleTileRange(out int tx0, out int ty0, out int tx1, out int ty1)
    {
        tx0 = ty0 = 0;
        tx1 = Math.Max(0, (Map?.Width ?? 20) - 1);
        ty1 = Math.Max(0, (Map?.Height ?? 15) - 1);
        var mw = Map?.Width ?? 20;
        var mh = Map?.Height ?? 15;

        var c = ClientSize;
        if (mw <= 0 || mh <= 0 || c.Width <= 0 || c.Height <= 0 || Zoom <= 0 || TileSize <= 0)
        {
            return;
        }

        var corners = new[]
        {
            ScreenToWorld(Point.Empty),
            ScreenToWorld(new Point(c.Width, 0)),
            ScreenToWorld(new Point(0, c.Height)),
            ScreenToWorld(new Point(c.Width, c.Height)),
        };

        float minWx = corners[0].X, maxWx = corners[0].X;
        float minWy = corners[0].Y, maxWy = corners[0].Y;
        foreach (var p in corners)
        {
            minWx = Math.Min(minWx, p.X);
            maxWx = Math.Max(maxWx, p.X);
            minWy = Math.Min(minWy, p.Y);
            maxWy = Math.Max(maxWy, p.Y);
        }

        var ts = TileSize;
        tx0 = (int)Math.Floor(minWx / ts) - ViewportPadTiles;
        ty0 = (int)Math.Floor(minWy / ts) - ViewportPadTiles;
        tx1 = (int)Math.Floor(maxWx / ts) + ViewportPadTiles;
        ty1 = (int)Math.Floor(maxWy / ts) + ViewportPadTiles;
        tx0 = Math.Clamp(tx0, 0, mw - 1);
        ty0 = Math.Clamp(ty0, 0, mh - 1);
        tx1 = Math.Clamp(tx1, 0, mw - 1);
        ty1 = Math.Clamp(ty1, 0, mh - 1);
        if (tx1 < tx0)
        {
            (tx0, tx1) = (tx1, tx0);
        }

        if (ty1 < ty0)
        {
            (ty0, ty1) = (ty1, ty0);
        }
    }

    private const float MinZoom = 0.125f;
    private const float MaxZoom = 16f;

    private void OnMouseWheelZoom(object? sender, MouseEventArgs e)
    {
        var factor = e.Delta > 0 ? 1.1f : 1f / 1.1f;
        ApplyZoomFactorAtScreenPoint(factor, e.Location);
    }

    /// <summary>Zoom avant (centre du contrôle), pour menu / raccourcis.</summary>
    public void ZoomInTowardCenter()
        => ApplyZoomFactorAtScreenPoint(1.1f, new Point(ClientSize.Width / 2, Math.Max(0, ClientSize.Height / 2)));

    /// <summary>Zoom arrière (centre du contrôle).</summary>
    public void ZoomOutTowardCenter()
        => ApplyZoomFactorAtScreenPoint(1f / 1.1f, new Point(ClientSize.Width / 2, Math.Max(0, ClientSize.Height / 2)));

    private void ApplyZoomFactorAtScreenPoint(float factor, Point screenPt)
    {
        if (ClientSize.Width <= 0 || ClientSize.Height <= 0)
        {
            return;
        }

        var before = ScreenToWorld(screenPt);
        var newZoom = Math.Clamp(Zoom * factor, MinZoom, MaxZoom);
        if (Math.Abs(newZoom - Zoom) < 0.0001f)
        {
            return;
        }

        Zoom = newZoom;
        var after = ScreenToWorld(screenPt);
        Pan = new PointF(
            Pan.X + (screenPt.X - (after.X - before.X)),
            Pan.Y + (screenPt.Y - (after.Y - before.Y)));
        NotifyViewTransformChanged();
        Invalidate();
    }

    private void OnMouseDown(object? sender, MouseEventArgs e)
    {
        if (e.Button != MouseButtons.Middle)
        {
            Focus();
        }

        if (e.Button == MouseButtons.Middle)
        {
            _panning = true;
            _lastMouse = e.Location;
            Cursor = Cursors.SizeAll;
            return;
        }

        if (Map is null)
        {
            return;
        }

        var world = ScreenToWorld(e.Location);
        var tx = (int)Math.Floor(world.X / TileSize);
        var ty = (int)Math.Floor(world.Y / TileSize);
        var inMap = tx >= 0 && ty >= 0 && tx < Map.Width && ty < Map.Height;

        if (e.Button == MouseButtons.Left && inMap && (ModifierKeys & Keys.Alt) == Keys.Alt)
        {
            TryPipetteAt(tx, ty, switchToBrush: false);
            return;
        }

        if (e.Button == MouseButtons.Left && TryPickMapEventMarker(world.X, world.Y))
        {
            _mapEventMarkerGesture = true;
            Capture = true;
            return;
        }

        if (!inMap)
        {
            return;
        }

        if (e.Button == MouseButtons.Right)
        {
            if (ActiveTool == EditorTool.Spawn)
            {
                if ((ModifierKeys & Keys.Control) == Keys.Control)
                {
                    _suppressRightButtonErase = true;
                    TileContextMenuRequested?.Invoke(new Point(tx, ty));
                }

                return;
            }

            if (ActiveTool == EditorTool.Prefab)
            {
                if ((ModifierKeys & Keys.Control) == Keys.Control)
                {
                    _suppressRightButtonErase = true;
                    TileContextMenuRequested?.Invoke(new Point(tx, ty));
                    return;
                }

                TryErasePrefabAt(tx, ty);
                Capture = true;
                Invalidate();
                RaiseTileClicked(tx, ty);
                return;
            }

            if (ActiveTool == EditorTool.Rectangle)
            {
                _rectPaintOrigin = null;
                Invalidate();
                return;
            }

            if (ActiveTool == EditorTool.Selection)
            {
                ClearSelection();
                return;
            }

            if ((ModifierKeys & Keys.Control) == Keys.Control)
            {
                _suppressRightButtonErase = true;
                TileContextMenuRequested?.Invoke(new Point(tx, ty));
                return;
            }

            if (!IsActiveLayerEditable())
            {
                return;
            }

            BeginPaintStroke();
            EraseStamp(tx, ty);
            Capture = true;
            Invalidate();
            RaiseTileClicked(tx, ty);
            return;
        }

        if (e.Button == MouseButtons.Left)
        {
            switch (ActiveTool)
            {
                case EditorTool.Brush:
                    if (!IsActiveLayerEditable())
                    {
                        break;
                    }

                    BeginPaintStroke();
                    ApplyBrush(tx, ty);
                    Capture = true;
                    Invalidate();
                    RaiseTileClicked(tx, ty);
                    break;

                case EditorTool.Eraser:
                    if (!IsActiveLayerEditable())
                    {
                        break;
                    }

                    BeginPaintStroke();
                    EraseStamp(tx, ty);
                    Capture = true;
                    Invalidate();
                    RaiseTileClicked(tx, ty);
                    break;

                case EditorTool.Cursor:
                    RaiseTileClicked(tx, ty);
                    break;

                case EditorTool.Fill:
                    if (!IsActiveLayerEditable())
                    {
                        break;
                    }

                    BeginEditTransaction();
                    FloodFill(tx, ty);
                    Invalidate();
                    RaiseTileClicked(tx, ty);
                    break;

                case EditorTool.Rectangle:
                    if (!IsActiveLayerEditable())
                    {
                        break;
                    }

                    _rectPaintOrigin = new Point(tx, ty);
                    _hoverTile = new Point(tx, ty);
                    Capture = true;
                    Invalidate();
                    break;

                case EditorTool.Selection:
                    _selectionMarqueeAnchor = new Point(tx, ty);
                    _hoverTile = new Point(tx, ty);
                    Capture = true;
                    Invalidate();
                    break;

                case EditorTool.Spawn:
                    TrySetPlaytestSpawn(tx, ty);
                    RaiseTileClicked(tx, ty);
                    break;

                case EditorTool.Prefab:
                    if ((ModifierKeys & Keys.Alt) == Keys.Alt)
                    {
                        TryPipettePrefabAt(tx, ty);
                        RaiseTileClicked(tx, ty);
                        break;
                    }

                    if (TryBeginPrefabMoveAt(tx, ty))
                    {
                        Capture = true;
                        Invalidate();
                        RaiseTileClicked(tx, ty);
                        break;
                    }

                    TryPlaceSelectedPrefab(tx, ty);
                    Capture = true;
                    Invalidate();
                    RaiseTileClicked(tx, ty);
                    break;
            }
        }
    }

    private void BeginPaintStroke()
    {
        if (Map is null || _paintStroke)
        {
            return;
        }

        BeginEditTransaction();
        _paintStroke = true;
    }

    private void BeginEditTransaction()
    {
        if (Map is null)
        {
            return;
        }

        History.PushBeforeChange(Map);
        MapEdited?.Invoke();
        UndoHistoryChanged?.Invoke();
    }

    private void OnMouseMove(object? sender, MouseEventArgs e)
    {
        if (_panning)
        {
            Pan = new PointF(Pan.X + (e.Location.X - _lastMouse.X), Pan.Y + (e.Location.Y - _lastMouse.Y));
            _lastMouse = e.Location;
            NotifyViewTransformChanged();
            Invalidate();
            return;
        }

        if (Map is null)
        {
            Cursor = Cursors.Cross;
            return;
        }

        var w = ScreenToWorld(e.Location);
        UpdateMapEventMarkerHover(w.X, w.Y);
        var tx = (int)Math.Floor(w.X / TileSize);
        var ty = (int)Math.Floor(w.Y / TileSize);
        if (tx >= 0 && ty >= 0 && tx < Map.Width && ty < Map.Height)
        {
            HoveredTileChanged?.Invoke(new Point(tx, ty));
            _hoverTile = new Point(tx, ty);
        }

        UpdateEditCursorForHover();

        if (_mapEventMarkerGesture)
        {
            return;
        }

        if ((ModifierKeys & Keys.Alt) == Keys.Alt)
        {
            return;
        }

        if ((e.Button & MouseButtons.Left) != 0)
        {
            if (ActiveTool == EditorTool.Brush && tx >= 0 && ty >= 0 && tx < Map.Width && ty < Map.Height && IsActiveLayerEditable())
            {
                ApplyBrush(tx, ty);
                Invalidate();
            }
            else if (ActiveTool == EditorTool.Prefab && tx >= 0 && ty >= 0 && tx < Map.Width && ty < Map.Height)
            {
                if (_draggingPrefab is not null)
                {
                    TryMoveDraggingPrefabTo(tx, ty);
                }
                else
                {
                    TryPlaceSelectedPrefab(tx, ty);
                    Invalidate();
                }
            }
            else if (ActiveTool is EditorTool.Rectangle or EditorTool.Selection && (_rectPaintOrigin is not null || _selectionMarqueeAnchor is not null))
            {
                Invalidate();
            }
        }

        if (!_suppressRightButtonErase &&
            ActiveTool == EditorTool.Prefab &&
            (e.Button & MouseButtons.Right) != 0 &&
            tx >= 0 &&
            ty >= 0 &&
            tx < Map.Width &&
            ty < Map.Height)
        {
            TryErasePrefabAt(tx, ty);
            Invalidate();
        }

        if (!_suppressRightButtonErase &&
            ActiveTool != EditorTool.Spawn &&
            ActiveTool != EditorTool.Prefab &&
            (e.Button & MouseButtons.Right) != 0 &&
            tx >= 0 &&
            ty >= 0 &&
            tx < Map.Width &&
            ty < Map.Height &&
            IsActiveLayerEditable())
        {
            EraseStamp(tx, ty);
            Invalidate();
        }
    }

    private void OnMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Middle)
        {
            _panning = false;
            Cursor = Cursors.Cross;
            NotifyViewTransformChanged();
        }

            if (e.Button == MouseButtons.Left && _mapEventMarkerGesture)
            {
                _mapEventMarkerGesture = false;
                Capture = false;
                return;
            }

            if (e.Button == MouseButtons.Left || e.Button == MouseButtons.Right)
            {
            if (_paintStroke)
            {
                _paintStroke = false;
                Capture = false;
            }

            if (_draggingPrefab is not null && e.Button == MouseButtons.Left)
            {
                EndPrefabMove();
                Capture = false;
            }

            if (e.Button == MouseButtons.Right)
            {
                _suppressRightButtonErase = false;
            }

            if (Map is null)
            {
                return;
            }

            if (ActiveTool == EditorTool.Rectangle && e.Button == MouseButtons.Left && _rectPaintOrigin is { } ro)
            {
                var world = ScreenToWorld(e.Location);
                var ex = (int)Math.Floor(world.X / TileSize);
                var ey = (int)Math.Floor(world.Y / TileSize);
                ex = Math.Clamp(ex, 0, Map.Width - 1);
                ey = Math.Clamp(ey, 0, Map.Height - 1);
                if (IsActiveLayerEditable())
                {
                    BeginEditTransaction();
                    ApplyRectangle(ro.X, ro.Y, ex, ey);
                    RaiseTileClicked(ex, ey);
                }

                _rectPaintOrigin = null;
                Capture = false;
                Invalidate();
            }

            if (ActiveTool == EditorTool.Selection && e.Button == MouseButtons.Left && _selectionMarqueeAnchor is { } sa)
            {
                var world = ScreenToWorld(e.Location);
                var ex = (int)Math.Floor(world.X / TileSize);
                var ey = (int)Math.Floor(world.Y / TileSize);
                ex = Math.Clamp(ex, 0, Map.Width - 1);
                ey = Math.Clamp(ey, 0, Map.Height - 1);
                var x0 = Math.Min(sa.X, ex);
                var y0 = Math.Min(sa.Y, ey);
                var x1 = Math.Max(sa.X, ex);
                var y1 = Math.Max(sa.Y, ey);
                _committedSelectionTiles = new Rectangle(x0, y0, x1 - x0 + 1, y1 - y0 + 1);
                _selectionMarqueeAnchor = null;
                Capture = false;
                Invalidate();
                RaiseTileClicked(ex, ey);
            }
        }
    }

    private bool IsActiveLayerEditable()
    {
        if (Map is null || ActiveLayerIndex < 0 || ActiveLayerIndex >= Map.Layers.Count)
        {
            return false;
        }

        return !Map.Layers[ActiveLayerIndex].Locked;
    }

    private void UpdateEditCursorForHover()
    {
        if (_panning)
        {
            return;
        }

        if (Map is null)
        {
            Cursor = Cursors.Cross;
            return;
        }

        if (ShowMapEventMarkers && _hoveredMapEventMarker is not null)
        {
            Cursor = Cursors.Hand;
            return;
        }

        if (ActiveTool is EditorTool.Cursor or EditorTool.Selection or EditorTool.Spawn)
        {
            Cursor = Cursors.Cross;
            return;
        }

        if (ActiveTool == EditorTool.Prefab)
        {
            Cursor = _draggingPrefab is not null ? Cursors.SizeAll : Cursors.Cross;
            return;
        }

        Cursor = !IsActiveLayerEditable() ? Cursors.No : Cursors.Cross;
    }

    private void ApplyBrush(int tx, int ty)
    {
        if (Map is null || !IsActiveLayerEditable())
        {
            return;
        }

        if (!TilesetCache.TryGet(ActiveTilesetId, out var bmp) || bmp is null)
        {
            return;
        }

        EnsureLayerExists();
        var ts = TileSize;
        var sw = Math.Max(1, SelectedStampInTiles.Width);
        var sh = Math.Max(1, SelectedStampInTiles.Height);
        for (var dy = 0; dy < sh; dy++)
        {
            for (var dx = 0; dx < sw; dx++)
            {
                var sx = SelectedSrc.X + dx * ts;
                var sy = SelectedSrc.Y + dy * ts;
                if (sx < 0 || sy < 0 || sx + ts > bmp.Width || sy + ts > bmp.Height)
                {
                    continue;
                }

                var mx = tx + dx;
                var my = ty + dy;
                if (mx < 0 || my < 0 || mx >= Map.Width || my >= Map.Height)
                {
                    continue;
                }

                MapEditOperations.PaintTile(Map, ActiveLayerIndex, mx, my, CreateBrushTile(mx, my, sx, sy));
            }
        }
    }

    private Tile CreateBrushTile(int tx, int ty, int srcX, int srcY)
    {
        var tile = new Tile
        {
            X = tx,
            Y = ty,
            TilesetId = ActiveTilesetId,
            SrcX = srcX,
            SrcY = srcY,
            Type = SelectedTileType
        };
        if (SelectedTileType == TileType.Warp)
        {
            tile.WarpTargetMapId = DefaultWarpTargetMapId ?? Guid.Empty;
            tile.WarpTargetX = 0;
            tile.WarpTargetY = 0;
        }

        if (SelectedTileType == TileType.Script)
        {
            tile.ScriptId = string.Empty;
        }

        return tile;
    }

    private void EraseAt(int tx, int ty)
    {
        if (Map is null)
        {
            return;
        }

        MapEditOperations.EraseTile(Map, ActiveLayerIndex, tx, ty);
    }

    private void EraseStamp(int tx, int ty)
    {
        if (Map is null || ActiveLayerIndex < 0 || ActiveLayerIndex >= Map.Layers.Count || !IsActiveLayerEditable())
        {
            return;
        }

        var sw = Math.Max(1, SelectedStampInTiles.Width);
        var sh = Math.Max(1, SelectedStampInTiles.Height);
        for (var dy = 0; dy < sh; dy++)
        {
            for (var dx = 0; dx < sw; dx++)
            {
                var mx = tx + dx;
                var my = ty + dy;
                if (mx >= 0 && my >= 0 && mx < Map.Width && my < Map.Height)
                {
                    EraseAt(mx, my);
                }
            }
        }
    }

    private void ApplyRectangle(int x0, int y0, int x1, int y1)
    {
        if (Map is null || !IsActiveLayerEditable())
        {
            return;
        }

        if (!TilesetCache.TryGet(ActiveTilesetId, out var bmpR) || bmpR is null)
        {
            return;
        }

        EnsureLayerExists();
        var minX = Math.Min(x0, x1);
        var maxX = Math.Max(x0, x1);
        var minY = Math.Min(y0, y1);
        var maxY = Math.Max(y0, y1);
        var ts = TileSize;
        var stw = Math.Max(1, SelectedStampInTiles.Width);
        var sth = Math.Max(1, SelectedStampInTiles.Height);
        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                var dx = (x - minX) % stw;
                var dy = (y - minY) % sth;
                var sx = SelectedSrc.X + dx * ts;
                var sy = SelectedSrc.Y + dy * ts;
                if (sx < 0 || sy < 0 || sx + ts > bmpR.Width || sy + ts > bmpR.Height)
                {
                    continue;
                }

                MapEditOperations.PaintTile(Map, ActiveLayerIndex, x, y, CreateBrushTile(x, y, sx, sy));
            }
        }
    }

    private void FloodFill(int sx, int sy)
    {
        if (Map is null || !IsActiveLayerEditable())
        {
            return;
        }

        EnsureLayerExists();
        MapEditOperations.FloodFill(Map, ActiveLayerIndex, sx, sy, CreateBrushTile(sx, sy, SelectedSrc.X, SelectedSrc.Y));
    }

    internal bool TryPaintTileForTest(int x, int y)
    {
        if (Map is null || !IsActiveLayerEditable())
        {
            return false;
        }

        if (!TilesetCache.TryGet(ActiveTilesetId, out var bmp) || bmp is null)
        {
            return false;
        }

        BeginEditTransaction();
        ApplyBrush(x, y);
        Invalidate();
        return Map.Layers[ActiveLayerIndex].Tiles.Any(t => t.X == x && t.Y == y);
    }

    internal void SetHoverTileForTest(int x, int y) => _hoverTile = new Point(x, y);

    internal void CommitSelectionForTest(int x, int y, int width, int height)
        => _committedSelectionTiles = new Rectangle(x, y, width, height);

    internal Rectangle? GetCommittedSelectionForTest() => _committedSelectionTiles;

    internal bool TryPipetteAtForTest(int x, int y, bool switchToBrush) => TryPipetteAt(x, y, switchToBrush);

    internal bool TryHandleAltLeftClickForTest(int tileX, int tileY)
    {
        if (Map is null || tileX < 0 || tileY < 0 || tileX >= Map.Width || tileY >= Map.Height)
        {
            return false;
        }

        return TryPipetteAt(tileX, tileY, switchToBrush: false);
    }

    internal void SetBlockTileForTest(int x, int y)
    {
        if (Map is null || !IsActiveLayerEditable())
        {
            return;
        }

        BeginEditTransaction();
        MapEditOperations.SetBlockTile(Map, ActiveLayerIndex, x, y);
        Invalidate();
    }

    internal void SetWarpTileForTest(int x, int y, Guid targetMapId, int targetX, int targetY)
    {
        if (Map is null || !IsActiveLayerEditable())
        {
            return;
        }

        BeginEditTransaction();
        MapEditOperations.SetWarpDestination(Map, ActiveLayerIndex, x, y, targetMapId, targetX, targetY);
        Invalidate();
    }

    internal void SetLayerVisibilityForTest(int layerIndex, bool visible)
    {
        if (Map is null || layerIndex < 0 || layerIndex >= Map.Layers.Count)
        {
            return;
        }

        BeginEditTransaction();
        MapEditOperations.SetLayerVisibility(Map, layerIndex, visible);
        Invalidate();
    }

    internal void SetMapNameForTest(string name)
    {
        if (Map is null)
        {
            return;
        }

        BeginEditTransaction();
        Map.Name = name;
        Invalidate();
    }

    private void EnsureLayerExists()
    {
        if (Map is null)
        {
            return;
        }

        while (Map.Layers.Count <= ActiveLayerIndex)
        {
            Map.Layers.Add(new Layer { LayerType = LayerType.Ground });
        }
    }

    private bool TryGetCommittedSelectionNormalized(out Rectangle rect)
    {
        rect = default;
        if (_committedSelectionTiles is not { Width: > 0, Height: > 0 } r)
        {
            return false;
        }

        rect = r;
        return true;
    }

    private void DeleteTilesInRectangle(Rectangle tileRect)
    {
        if (Map is null || ActiveLayerIndex < 0 || ActiveLayerIndex >= Map.Layers.Count || !IsActiveLayerEditable())
        {
            return;
        }

        var layer = Map.Layers[ActiveLayerIndex];
        for (var y = tileRect.Top; y < tileRect.Top + tileRect.Height; y++)
        {
            for (var x = tileRect.Left; x < tileRect.Left + tileRect.Width; x++)
            {
                layer.Tiles.RemoveAll(t => t.X == x && t.Y == y);
            }
        }
    }

    private void RaiseTileClicked(int tileX, int tileY)
    {
        if (Map?.Layers is null || Map.Layers.Count == 0)
        {
            TileClicked?.Invoke(null);
            return;
        }

        if (ActiveLayerIndex < 0 || ActiveLayerIndex >= Map.Layers.Count)
        {
            TileClicked?.Invoke(null);
            return;
        }

        var layer = Map.Layers[ActiveLayerIndex];
        var tile = layer.Tiles.FirstOrDefault(t => t.X == tileX && t.Y == tileY);
        TileClicked?.Invoke(tile);
    }

    private bool TryPickMapEventMarker(float worldX, float worldY)
    {
        if (!TryHitMapEventMarker(worldX, worldY, out var hit))
        {
            return false;
        }

        HighlightMapEventMarker(hit.TileX, hit.TileY, hit.PrimaryPlacementKey);
        MapEventMarkerPicked?.Invoke(hit);
        return true;
    }

    private bool TryHitMapEventMarker(float worldX, float worldY, out MapEventMarkerView hit)
    {
        hit = default;
        if (!ShowMapEventMarkers || _mapEventMarkers is null || Map is null)
        {
            return false;
        }

        for (var i = _mapEventMarkers.Count - 1; i >= 0; i--)
        {
            var marker = _mapEventMarkers[i];
            if (marker.TileX < 0 || marker.TileY < 0 || marker.TileX >= Map.Width || marker.TileY >= Map.Height)
            {
                continue;
            }

            var label = MapEventMarkerLayout.FormatLabel(marker.PrimaryDisplayName, marker.PrimarySlug, marker.PlacementCount);
            if (MapEventMarkerLayout.HitTest(
                    marker.TileX,
                    marker.TileY,
                    TileSize,
                    worldX,
                    worldY,
                    ShouldDrawNameFor(marker),
                    label))
            {
                hit = marker;
                return true;
            }
        }

        return false;
    }

    private void UpdateMapEventMarkerHover(float worldX, float worldY)
    {
        MapEventMarkerView? next = null;
        if (TryHitMapEventMarker(worldX, worldY, out var hit))
        {
            next = hit;
        }

        if (Nullable.Equals(_hoveredMapEventMarker, next))
        {
            return;
        }

        _hoveredMapEventMarker = next;
        Invalidate();
        MapEventMarkerInteractionChanged?.Invoke();
    }

    private void ClearMapEventMarkerHover()
    {
        if (_hoveredMapEventMarker is null)
        {
            return;
        }

        _hoveredMapEventMarker = null;
        Invalidate();
        MapEventMarkerInteractionChanged?.Invoke();
        UpdateEditCursorForHover();
    }

    private bool ShouldDrawNameFor(MapEventMarkerView marker)
    {
        var hovered = _hoveredMapEventMarker is { } h && h.Equals(marker);
        return ShowMapEventMarkers && MapEventMarkerLayout.ShouldDrawName(
            ShowMapEventNames,
            Zoom,
            TileSize,
            hovered,
            MarkerMatchesSelection(marker));
    }

    private bool MarkerMatchesSelection(MapEventMarkerView marker)
    {
        if (!_hasSelectedMapEvent)
        {
            return false;
        }

        if (!string.IsNullOrEmpty(_selectedMapEventPlacementKey))
        {
            return string.Equals(_selectedMapEventPlacementKey, marker.PrimaryPlacementKey, StringComparison.OrdinalIgnoreCase);
        }

        return marker.TileX == _selectedMapEventTileX && marker.TileY == _selectedMapEventTileY;
    }

    private bool MarkerListContainsSelection(IReadOnlyList<MapEventMarkerView>? markers)
    {
        if (!_hasSelectedMapEvent || markers is null)
        {
            return false;
        }

        foreach (var marker in markers)
        {
            if (MarkerMatchesSelection(marker))
            {
                return true;
            }
        }

        return false;
    }

    private static bool MarkerListContains(IReadOnlyList<MapEventMarkerView>? markers, MapEventMarkerView marker)
    {
        if (markers is null)
        {
            return false;
        }

        foreach (var candidate in markers)
        {
            if (candidate.Equals(marker))
            {
                return true;
            }
        }

        return false;
    }

    private bool TryFindSelectedMarker(out MapEventMarkerView marker)
    {
        marker = default;
        if (!_hasSelectedMapEvent || _mapEventMarkers is null)
        {
            return false;
        }

        foreach (var candidate in _mapEventMarkers)
        {
            if (!MarkerMatchesSelection(candidate))
            {
                continue;
            }

            marker = candidate;
            return true;
        }

        return false;
    }

    internal string? SelectedMapEventPlacementKeyForTest =>
        _hasSelectedMapEvent ? _selectedMapEventPlacementKey : null;

    internal bool TryPickMapEventMarkerAtWorldForTest(float worldX, float worldY) =>
        TryPickMapEventMarker(worldX, worldY);

    internal void SetZoomForTest(float zoom)
    {
        Zoom = Math.Clamp(zoom, MinZoom, MaxZoom);
        Invalidate();
    }

    internal bool ShouldDrawMapEventNameForTest(MapEventMarkerView marker) => ShouldDrawNameFor(marker);

    internal void SetHoveredMapEventAtWorldForTest(float worldX, float worldY) =>
        UpdateMapEventMarkerHover(worldX, worldY);

    internal void RaiseMouseDownForTest(MouseButtons button, int x, int y) =>
        OnMouseDown(this, new MouseEventArgs(button, 1, x, y, 0));

    internal void RaiseMouseUpForTest(MouseButtons button, int x, int y) =>
        OnMouseUp(this, new MouseEventArgs(button, 1, x, y, 0));

    private PointF ScreenToWorld(Point p)
    {
        var x = (p.X - Pan.X) / Zoom;
        var y = (p.Y - Pan.Y) / Zoom;
        return new PointF(x, y);
    }
}

