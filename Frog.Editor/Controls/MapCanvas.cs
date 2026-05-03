using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using Frog.Core.Enums;
using Frog.Core.Models;
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
    public Map? Map { get; set; }

    public int ActiveTilesetId { get; set; } = 0;
    public Point SelectedSrc { get; set; } = new(0, 0);

    public int ActiveLayerIndex { get; set; } = 0;
    public event Action<Point>? HoveredTileChanged;
    public TileType SelectedTileType { get; set; } = TileType.Ground;
    public event Action<Tile?>? TileClicked;
    public event Action? MapReplaced;
    public event Action? UndoHistoryChanged;

    public EditorTool ActiveTool { get; set; } = EditorTool.Brush;

    private bool _panning;
    private Point _lastMouse;
    private bool _paintStroke;
    private Point? _rectPaintOrigin;
    private Point _hoverTile;
    private Point? _selectionMarqueeAnchor;
    private Rectangle? _committedSelectionTiles;

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
                if (tx >= 0 && ty >= 0 && tx < mw && ty < mh)
                {
                    var src = new Rectangle(SelectedSrc.X, SelectedSrc.Y, TileSize, TileSize);
                    var dst = new Rectangle(tx * TileSize, ty * TileSize, TileSize, TileSize);
                    using var attrs = new System.Drawing.Imaging.ImageAttributes();
                    attrs.SetColorMatrix(new System.Drawing.Imaging.ColorMatrix { Matrix33 = 0.45f }, System.Drawing.Imaging.ColorMatrixFlag.Default, System.Drawing.Imaging.ColorAdjustType.Bitmap);
                    g.DrawImage(bmpG, dst, src.X, src.Y, src.Width, src.Height, GraphicsUnit.Pixel, attrs);
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

    private void OnMouseWheelZoom(object? sender, MouseEventArgs e)
    {
        if ((ModifierKeys & Keys.Control) == 0)
        {
            return;
        }

        var before = ScreenToWorld(e.Location);
        var factor = e.Delta > 0 ? 1.1f : 1f / 1.1f;
        var newZoom = Math.Clamp(Zoom * factor, 0.25f, 4f);
        if (Math.Abs(newZoom - Zoom) < 0.0001f)
        {
            return;
        }

        Zoom = newZoom;
        var after = ScreenToWorld(e.Location);
        Pan = new PointF(Pan.X + (e.Location.X - (after.X - before.X)), Pan.Y + (e.Location.Y - (after.Y - before.Y)));
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

            if (!IsActiveLayerEditable())
            {
                return;
            }

            BeginPaintStroke();
            EraseAt(tx, ty);
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
                    EraseAt(tx, ty);
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

        if ((e.Button & MouseButtons.Right) != 0 && tx >= 0 && ty >= 0 && tx < Map.Width && ty < Map.Height && IsActiveLayerEditable())
        {
            EraseAt(tx, ty);
            Invalidate();
        }
    }

    private void OnMouseUp(object? sender, MouseEventArgs e)
    {
        if (e.Button == MouseButtons.Middle)
        {
            _panning = false;
            Cursor = Cursors.Cross;
        }

        if (e.Button == MouseButtons.Left || e.Button == MouseButtons.Right)
        {
            if (_paintStroke)
            {
                _paintStroke = false;
                Capture = false;
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

        EnsureLayerExists();
        var layer = Map.Layers[ActiveLayerIndex];
        layer.Tiles.RemoveAll(t => t.X == tx && t.Y == ty);
        layer.Tiles.Add(CreateBrushTile(tx, ty));
    }

    private Tile CreateBrushTile(int tx, int ty)
    {
        var tile = new Tile
        {
            X = tx,
            Y = ty,
            TilesetId = ActiveTilesetId,
            SrcX = SelectedSrc.X,
            SrcY = SelectedSrc.Y,
            Type = SelectedTileType
        };
        if (SelectedTileType == TileType.Warp)
        {
            tile.WarpTargetMapId = 0;
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

    private void ApplyRectangle(int x0, int y0, int x1, int y1)
    {
        if (Map is null || !IsActiveLayerEditable())
        {
            return;
        }

        EnsureLayerExists();
        var layer = Map.Layers[ActiveLayerIndex];
        var minX = Math.Min(x0, x1);
        var maxX = Math.Max(x0, x1);
        var minY = Math.Min(y0, y1);
        var maxY = Math.Max(y0, y1);
        for (var y = minY; y <= maxY; y++)
        {
            for (var x = minX; x <= maxX; x++)
            {
                layer.Tiles.RemoveAll(t => t.X == x && t.Y == y);
                layer.Tiles.Add(CreateBrushTile(x, y));
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
            layer.Tiles.Add(CreateBrushTile(x, y));
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

