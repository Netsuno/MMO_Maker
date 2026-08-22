using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
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

    /// <summary>Marqueurs événements ou visibilité overlay ont changé (mini-carte, etc.).</summary>
    public event Action? MapEventOverlayChanged;

    private bool _showMapEventMarkers = true;

    /// <summary>Affiche les pastilles d’événements MariaDB (<see cref="MapEventMarkers"/>) sur le canevas.</summary>
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
            MapEventOverlayChanged?.Invoke();
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
            MapEventOverlayChanged?.Invoke();
            Invalidate();
        }
    }

    public EditorTool ActiveTool { get; set; } = EditorTool.Brush;

    private bool _panning;
    private Point _lastMouse;
    private bool _paintStroke;
    private Point? _rectPaintOrigin;
    private Point _hoverTile;
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
        History.PushBeforeChange(Map);
        UndoHistoryChanged?.Invoke();
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
        History.PushBeforeChange(Map);
        UndoHistoryChanged?.Invoke();
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

        History.PushBeforeChange(Map);
        UndoHistoryChanged?.Invoke();
        DeleteTilesInRectangle(rect);
        Invalidate();
        return true;
    }

    public bool HandleEditorShortcuts(Keys keyData)
    {
        var ctrl = (keyData & Keys.Control) == Keys.Control;
        var code = keyData & Keys.KeyCode;

        switch (code)
        {
            case Keys.C when ctrl:
                return TryCopyTileSelection();
            case Keys.X when ctrl:
                return TryCutTileSelection();
            case Keys.V when ctrl:
                return TryPasteAtHover();
            case Keys.Delete when !ctrl:
                return TryDeleteSelectedTiles();
            default:
                return false;
        }
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

                DrawTileTypeOverlay(g, tx0, ty0, tx1, ty1);
                DrawMapEventMarkerOverlay(g, tx0, ty0, tx1, ty1);
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
        var prevSmooth = g.SmoothingMode;
        var prevText = g.TextRenderingHint;
        g.SmoothingMode = SmoothingMode.AntiAlias;
        g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;
        try
        {
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

                var rect = new Rectangle(m.TileX * ts, m.TileY * ts, ts, ts);
                var fill = MapEventMarkerColors.TintFromSlug(m.PrimarySlug);
                var badgeD = Math.Max(6, ts * 2 / 5);
                var pad = Math.Max(1, ts / 14);
                var badge = new Rectangle(rect.Right - badgeD - pad, rect.Y + pad, badgeD, badgeD);
                var stepOn = string.Equals(m.PrimaryTriggerKind, MapEventTriggerKinds.StepOn, StringComparison.Ordinal);
                var page = string.Equals(m.PrimaryTriggerKind, MapEventTriggerKinds.Page, StringComparison.Ordinal);
                var autoTile = string.Equals(m.PrimaryTriggerKind, MapEventTriggerKinds.AutoTile, StringComparison.Ordinal);
                if (page)
                {
                    var rDot = Math.Max(3f, ts * 0.17f);
                    var cx = rect.Left + pad + rDot;
                    var cy = rect.Bottom - pad - rDot;
                    using (var brush = new SolidBrush(Color.FromArgb(155, fill)))
                    {
                        g.FillEllipse(brush, cx - rDot, cy - rDot, rDot * 2f, rDot * 2f);
                    }

                    using (var edge = new Pen(Color.FromArgb(210, Color.White), Math.Max(1f, ts / 18f)))
                    {
                        g.DrawEllipse(edge, cx - rDot, cy - rDot, rDot * 2f, rDot * 2f);
                    }

                    if (m.PlacementCount > 1)
                    {
                        var countRect = new RectangleF(cx - rDot, cy - rDot, rDot * 2f, rDot * 2f);
                        var label = m.PlacementCount > 9 ? "9+" : m.PlacementCount.ToString();
                        using var f = new Font(Font.FontFamily, Math.Max(5f, ts * 0.16f), FontStyle.Bold, GraphicsUnit.Pixel);
                        using var tb = new SolidBrush(Color.White);
                        using var sf = new StringFormat
                        {
                            Alignment = StringAlignment.Center,
                            LineAlignment = StringAlignment.Center,
                        };
                        g.DrawString(label, f, tb, countRect, sf);
                    }
                }
                else if (autoTile)
                {
                    var inset = Math.Max(2, ts / 10);
                    var dashRect = new Rectangle(rect.X + inset, rect.Y + inset, rect.Width - inset * 2, rect.Height - inset * 2);
                    using var dashPen = new Pen(Color.FromArgb(220, fill), Math.Max(1.5f, ts / 20f))
                    {
                        DashStyle = DashStyle.Dash,
                    };
                    g.DrawRectangle(dashPen, dashRect);
                }
                else
                {
                    using (var brush = new SolidBrush(Color.FromArgb(150, fill)))
                    {
                        if (stepOn)
                        {
                            FillDiamond(g, brush, badge);
                        }
                        else
                        {
                            g.FillEllipse(brush, badge);
                        }
                    }

                    using (var edge = new Pen(Color.FromArgb(210, Color.White), Math.Max(1f, ts / 18f)))
                    {
                        if (stepOn)
                        {
                            DrawDiamond(g, edge, badge);
                        }
                        else
                        {
                            g.DrawEllipse(edge, badge);
                        }
                    }

                    if (m.PlacementCount > 1)
                    {
                        var label = m.PlacementCount > 9 ? "9+" : m.PlacementCount.ToString();
                        using var f = new Font(Font.FontFamily, Math.Max(6f, ts * 0.21f), FontStyle.Bold, GraphicsUnit.Pixel);
                        using var tb = new SolidBrush(Color.White);
                        using var sf = new StringFormat
                        {
                            Alignment = StringAlignment.Center,
                            LineAlignment = StringAlignment.Center,
                        };
                        g.DrawString(label, f, tb, badge, sf);
                    }
                }
            }
        }
        finally
        {
            g.SmoothingMode = prevSmooth;
            g.TextRenderingHint = prevText;
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
        if (tx < 0 || ty < 0 || tx >= Map.Width || ty >= Map.Height)
        {
            return;
        }

        if (e.Button == MouseButtons.Right)
        {
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

                    History.PushBeforeChange(Map);
                    UndoHistoryChanged?.Invoke();
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
            }
        }
    }

    private void BeginPaintStroke()
    {
        if (Map is null || _paintStroke)
        {
            return;
        }

        History.PushBeforeChange(Map);
        _paintStroke = true;
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
        var tx = (int)Math.Floor(w.X / TileSize);
        var ty = (int)Math.Floor(w.Y / TileSize);
        if (tx >= 0 && ty >= 0 && tx < Map.Width && ty < Map.Height)
        {
            HoveredTileChanged?.Invoke(new Point(tx, ty));
            _hoverTile = new Point(tx, ty);
        }

        UpdateEditCursorForHover();

        if ((e.Button & MouseButtons.Left) != 0)
        {
            if (ActiveTool == EditorTool.Brush && tx >= 0 && ty >= 0 && tx < Map.Width && ty < Map.Height && IsActiveLayerEditable())
            {
                ApplyBrush(tx, ty);
                Invalidate();
            }
            else if (ActiveTool is EditorTool.Rectangle or EditorTool.Selection && (_rectPaintOrigin is not null || _selectionMarqueeAnchor is not null))
            {
                Invalidate();
            }
        }

        if (!_suppressRightButtonErase &&
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

        if (e.Button == MouseButtons.Left || e.Button == MouseButtons.Right)
        {
            if (_paintStroke)
            {
                _paintStroke = false;
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
                    History.PushBeforeChange(Map);
                    UndoHistoryChanged?.Invoke();
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

        if (ActiveTool is EditorTool.Cursor or EditorTool.Selection)
        {
            Cursor = Cursors.Cross;
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
        var layer = Map.Layers[ActiveLayerIndex];
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

                layer.Tiles.RemoveAll(t => t.X == mx && t.Y == my);
                layer.Tiles.Add(CreateBrushTile(mx, my, sx, sy));
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
            tile.WarpTargetMapId = Guid.Empty;
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
        if (Map is null || ActiveLayerIndex < 0 || ActiveLayerIndex >= Map.Layers.Count || !IsActiveLayerEditable())
        {
            return;
        }

        Map.Layers[ActiveLayerIndex].Tiles.RemoveAll(t => t.X == tx && t.Y == ty);
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
        var layer = Map.Layers[ActiveLayerIndex];
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

                layer.Tiles.RemoveAll(t => t.X == x && t.Y == y);
                layer.Tiles.Add(CreateBrushTile(x, y, sx, sy));
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
        var layer = Map.Layers[ActiveLayerIndex];
        var start = layer.Tiles.FirstOrDefault(t => t.X == sx && t.Y == sy);
        var matchEmpty = start is null;

        var q = new Queue<(int x, int y)>();
        var seen = new HashSet<(int, int)>();
        q.Enqueue((sx, sy));
        var toPaint = new List<(int x, int y)>();

        while (q.Count > 0)
        {
            var (x, y) = q.Dequeue();
            if (!seen.Add((x, y)))
            {
                continue;
            }

            if (x < 0 || y < 0 || x >= Map.Width || y >= Map.Height)
            {
                continue;
            }

            var here = layer.Tiles.FirstOrDefault(t => t.X == x && t.Y == y);
            if (matchEmpty)
            {
                if (here is not null)
                {
                    continue;
                }
            }
            else if (start is null || !SameVisualTile(start, here))
            {
                continue;
            }

            toPaint.Add((x, y));
            q.Enqueue((x - 1, y));
            q.Enqueue((x + 1, y));
            q.Enqueue((x, y - 1));
            q.Enqueue((x, y + 1));
        }

        foreach (var (x, y) in toPaint)
        {
            layer.Tiles.RemoveAll(t => t.X == x && t.Y == y);
            layer.Tiles.Add(CreateBrushTile(x, y, SelectedSrc.X, SelectedSrc.Y));
        }
    }

    private static bool SameVisualTile(Tile a, Tile? b)
    {
        if (b is null)
        {
            return false;
        }

        if (a.TilesetId != b.TilesetId || a.SrcX != b.SrcX || a.SrcY != b.SrcY || a.Type != b.Type)
        {
            return false;
        }

        return a.Type != TileType.Warp
            || (a.WarpTargetMapId == b.WarpTargetMapId && a.WarpTargetX == b.WarpTargetX && a.WarpTargetY == b.WarpTargetY);
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

    private PointF ScreenToWorld(Point p)
    {
        var x = (p.X - Pan.X) / Zoom;
        var y = (p.Y - Pan.Y) / Zoom;
        return new PointF(x, y);
    }
}

