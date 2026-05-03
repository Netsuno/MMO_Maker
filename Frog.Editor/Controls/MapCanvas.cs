using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using Frog.Core.Enums;
using Frog.Core.Models;
using Frog.Editor.Assets;
using Frog.Editor.Enums;
using Frog.Editor.Services;

namespace Frog.Editor.Controls;

/// <summary>
/// Canvas d’édition : grille, zoom/pan, pinceau avec traînée, gomme, pot de peinture, rectangle, undo interne.
/// </summary>
public sealed class MapCanvas : Control
{
    public readonly MapUndoController History = new();

    public int TileSize { get; set; } = 32;
    public float Zoom { get; private set; } = 1.0f;
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
    private Point? _rectOrigin;
    private Point _hoverTile;

    public MapCanvas()
    {
        SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
        BackColor = Color.FromArgb(34, 34, 34);
        Cursor = Cursors.Cross;
        Dock = DockStyle.Fill;

        MouseWheel += OnMouseWheelZoom;
        MouseDown += OnMouseDown;
        MouseMove += OnMouseMove;
        MouseUp += OnMouseUp;
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
        e.Graphics.Clear(BackColor);
        e.Graphics.TranslateTransform(Pan.X, Pan.Y);
        e.Graphics.ScaleTransform(Zoom, Zoom);

        var w = 20;
        var h = 15;
        if (Map is not null)
        {
            w = Math.Max(1, Map.Width);
            h = Math.Max(1, Map.Height);
        }

        DrawGrid(e.Graphics, w, h);

        if (Map is not null)
        {
            foreach (var layer in Map.Layers)
            {
                DrawLayer(e.Graphics, layer);
            }
        }

        DrawTileTypeOverlay(e.Graphics);

        if (Map is not null && ActiveTool == EditorTool.Rectangle && _rectOrigin is { } o)
        {
            var x0 = Math.Min(o.X, _hoverTile.X);
            var y0 = Math.Min(o.Y, _hoverTile.Y);
            var x1 = Math.Max(o.X, _hoverTile.X);
            var y1 = Math.Max(o.Y, _hoverTile.Y);
            var pr = new Rectangle(x0 * TileSize, y0 * TileSize, (x1 - x0 + 1) * TileSize, (y1 - y0 + 1) * TileSize);
            using var b = new SolidBrush(Color.FromArgb(60, Color.Cyan));
            using var p = new Pen(Color.Cyan, 2);
            e.Graphics.FillRectangle(b, pr);
            e.Graphics.DrawRectangle(p, pr);
        }

        if (Map is not null && ActiveTilesetId > 0 && TilesetCache.TryGet(ActiveTilesetId, out var bmp) && bmp is not null)
        {
            var mouse = PointToClient(Cursor.Position);
            var world = ScreenToWorld(mouse);
            var tx = (int)Math.Floor(world.X / TileSize);
            var ty = (int)Math.Floor(world.Y / TileSize);
            if (tx >= 0 && ty >= 0 && tx < Map.Width && ty < Map.Height)
            {
                var src = new Rectangle(SelectedSrc.X, SelectedSrc.Y, TileSize, TileSize);
                var dst = new Rectangle(tx * TileSize, ty * TileSize, TileSize, TileSize);
                var colorMatrix = new System.Drawing.Imaging.ColorMatrix { Matrix33 = 0.5f };
                using var attrs = new System.Drawing.Imaging.ImageAttributes();
                attrs.SetColorMatrix(colorMatrix, System.Drawing.Imaging.ColorMatrixFlag.Default, System.Drawing.Imaging.ColorAdjustType.Bitmap);
                e.Graphics.DrawImage(bmp, dst, src.X, src.Y, src.Width, src.Height, GraphicsUnit.Pixel, attrs);
            }
        }
    }

    private void DrawLayer(Graphics g, Layer layer)
    {
        foreach (var t in layer.Tiles)
        {
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

    private void DrawGrid(Graphics g, int widthTiles, int heightTiles)
    {
        using var penMajor = new Pen(Color.FromArgb(70, 70, 70), 1f);
        using var penMinor = new Pen(Color.FromArgb(55, 55, 55), 1f);
        using var light = new SolidBrush(Color.FromArgb(38, 38, 38));
        using var dark = new SolidBrush(Color.FromArgb(42, 42, 42));

        for (var y = 0; y < heightTiles; y++)
        {
            for (var x = 0; x < widthTiles; x++)
            {
                var r = new Rectangle(x * TileSize, y * TileSize, TileSize, TileSize);
                g.FillRectangle(((x + y) % 2 == 0) ? light : dark, r);
            }
        }

        for (var x = 0; x <= widthTiles; x++)
        {
            g.DrawLine(penMinor, x * TileSize, 0, x * TileSize, heightTiles * TileSize);
        }

        for (var y = 0; y <= heightTiles; y++)
        {
            g.DrawLine(penMinor, 0, y * TileSize, widthTiles * TileSize, y * TileSize);
        }

        g.DrawRectangle(penMajor, 0, 0, widthTiles * TileSize, heightTiles * TileSize);
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

        if (e.Button == MouseButtons.Right && ActiveTool == EditorTool.Rectangle)
        {
            _rectOrigin = null;
            Invalidate();
            return;
        }

        if (e.Button == MouseButtons.Left)
        {
            switch (ActiveTool)
            {
                case EditorTool.Brush:
                    BeginPaintStroke();
                    ApplyBrush(tx, ty);
                    Capture = true;
                    Invalidate();
                    RaiseTileClicked(tx, ty);
                    break;

                case EditorTool.Eraser:
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
                    History.PushBeforeChange(Map);
                    UndoHistoryChanged?.Invoke();
                    FloodFill(tx, ty);
                    Invalidate();
                    RaiseTileClicked(tx, ty);
                    break;

                case EditorTool.Rectangle:
                    _rectOrigin = new Point(tx, ty);
                    _hoverTile = new Point(tx, ty);
                    Capture = true;
                    Invalidate();
                    break;
            }
        }
        else if (e.Button == MouseButtons.Right)
        {
            BeginPaintStroke();
            EraseAt(tx, ty);
            Capture = true;
            Invalidate();
            RaiseTileClicked(tx, ty);
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

        if ((e.Button & MouseButtons.Left) != 0)
        {
            if (ActiveTool == EditorTool.Brush && tx >= 0 && ty >= 0 && tx < Map.Width && ty < Map.Height)
            {
                ApplyBrush(tx, ty);
                Invalidate();
            }
            else if (ActiveTool == EditorTool.Rectangle && _rectOrigin is not null)
            {
                Invalidate();
            }
        }

        if ((e.Button & MouseButtons.Right) != 0 && tx >= 0 && ty >= 0 && tx < Map.Width && ty < Map.Height)
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

            if (ActiveTool == EditorTool.Rectangle && e.Button == MouseButtons.Left && _rectOrigin is { } o && Map is not null)
            {
                var world = ScreenToWorld(e.Location);
                var ex = (int)Math.Floor(world.X / TileSize);
                var ey = (int)Math.Floor(world.Y / TileSize);
                ex = Math.Clamp(ex, 0, Map.Width - 1);
                ey = Math.Clamp(ey, 0, Map.Height - 1);
                History.PushBeforeChange(Map);
                UndoHistoryChanged?.Invoke();
                ApplyRectangle(o.X, o.Y, ex, ey);
                _rectOrigin = null;
                Capture = false;
                Invalidate();
                RaiseTileClicked(ex, ey);
            }
        }
    }

    private void ApplyBrush(int tx, int ty)
    {
        if (Map is null)
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
        if (Map is null || ActiveLayerIndex < 0 || ActiveLayerIndex >= Map.Layers.Count)
        {
            return;
        }

        Map.Layers[ActiveLayerIndex].Tiles.RemoveAll(t => t.X == tx && t.Y == ty);
    }

    private void ApplyRectangle(int x0, int y0, int x1, int y1)
    {
        if (Map is null)
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
        if (Map is null)
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
            else if (!SameVisualTile(start!, here))
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

    private void DrawTileTypeOverlay(Graphics g)
    {
        if (Map is null)
        {
            return;
        }

        foreach (var layer in Map.Layers)
        {
            foreach (var t in layer.Tiles)
            {
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
