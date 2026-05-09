#nullable enable
using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using Frog.Core.Models;
using Frog.Core.Protocol;
using Frog.Editor.Services;
using Frog.Editor.Ui;

namespace Frog.Editor.Controls;

/// <summary>Mini-carte : aperçu de la carte, rectangle de vue, clic pour centrer le canevas.</summary>
public sealed class MapMinimapControl : Control
{
    private MapCanvas? _canvas;

    public MapMinimapControl()
    {
        SetStyle(
            ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer | ControlStyles.UserPaint,
            true);
        Size = new Size(184, 132);
        BackColor = EditorChrome.MapCanvasBg;
        Cursor = Cursors.Hand;
        TabStop = false;
    }

    public void Attach(MapCanvas canvas)
    {
        DetachInner();
        _canvas = canvas;
        if (_canvas is not null)
        {
            _canvas.ViewTransformChanged += OnViewTransformChanged;
            _canvas.MapEventOverlayChanged += OnMapEventOverlayChanged;
            _canvas.Resize += OnCanvasResize;
        }

        Invalidate();
    }

    private void OnViewTransformChanged() => Invalidate();

    private void OnMapEventOverlayChanged() => Invalidate();

    private void OnCanvasResize(object? sender, EventArgs e) => Invalidate();

    private void DetachInner()
    {
        if (_canvas is null)
        {
            return;
        }

        _canvas.ViewTransformChanged -= OnViewTransformChanged;
        _canvas.MapEventOverlayChanged -= OnMapEventOverlayChanged;
        _canvas.Resize -= OnCanvasResize;
        _canvas = null;
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        DetachInner();
        base.OnHandleDestroyed(e);
    }

    private static void FillMinimapDiamond(Graphics g, Brush brush, RectangleF r)
    {
        var pts = new[]
        {
            new PointF(r.X + r.Width * 0.5f, r.Y),
            new PointF(r.Right, r.Y + r.Height * 0.5f),
            new PointF(r.X + r.Width * 0.5f, r.Bottom),
            new PointF(r.Left, r.Y + r.Height * 0.5f),
        };
        g.FillPolygon(brush, pts);
    }

    private static void DrawMinimapDiamond(Graphics g, Pen pen, RectangleF r)
    {
        var pts = new[]
        {
            new PointF(r.X + r.Width * 0.5f, r.Y),
            new PointF(r.Right, r.Y + r.Height * 0.5f),
            new PointF(r.X + r.Width * 0.5f, r.Bottom),
            new PointF(r.Left, r.Y + r.Height * 0.5f),
        };
        g.DrawPolygon(pen, pts);
    }

    private static bool TryComputeMinimapLayout(
        int controlWidth,
        int controlHeight,
        Map map,
        int tileSize,
        out float pad,
        out float ox,
        out float oy,
        out float scale,
        out float drawW,
        out float drawH,
        out float mapPxW,
        out float mapPxH)
    {
        pad = 3f;
        mapPxW = Math.Max(1f, map.Width * (float)tileSize);
        mapPxH = Math.Max(1f, map.Height * (float)tileSize);
        var innerW = controlWidth - 2 * pad;
        var innerH = controlHeight - 2 * pad;
        scale = Math.Min(innerW / mapPxW, innerH / mapPxH);
        if (scale <= 0 || float.IsInfinity(scale) || float.IsNaN(scale))
        {
            ox = oy = drawW = drawH = 0f;
            return false;
        }

        drawW = mapPxW * scale;
        drawH = mapPxH * scale;
        ox = pad + (innerW - drawW) * 0.5f;
        oy = pad + (innerH - drawH) * 0.5f;
        return true;
    }

    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);
        var g = e.Graphics;
        g.Clear(BackColor);
        using (var border = new Pen(Color.FromArgb(90, 95, 110), 1))
        {
            g.DrawRectangle(border, 0, 0, Width - 1, Height - 1);
        }

        if (_canvas?.Map is null)
        {
            return;
        }

        var map = _canvas.Map;
        var ts = _canvas.TileSize;
        if (!TryComputeMinimapLayout(Width, Height, map, ts, out _, out var ox, out var oy, out var scale, out var drawW, out var drawH, out var mapPxW, out var mapPxH))
        {
            return;
        }

        using (var fill = new SolidBrush(Color.FromArgb(55, 72, 86)))
        {
            g.FillRectangle(fill, ox, oy, drawW, drawH);
        }

        _canvas.GetViewportWorldBounds(out var w0, out var w1);
        var vx0 = Math.Clamp(Math.Min(w0.X, w1.X), 0f, mapPxW);
        var vy0 = Math.Clamp(Math.Min(w0.Y, w1.Y), 0f, mapPxH);
        var vx1 = Math.Clamp(Math.Max(w0.X, w1.X), 0f, mapPxW);
        var vy1 = Math.Clamp(Math.Max(w0.Y, w1.Y), 0f, mapPxH);
        var rw = Math.Max(1f, vx1 - vx0);
        var rh = Math.Max(1f, vy1 - vy0);
        var rx = ox + vx0 * scale;
        var ry = oy + vy0 * scale;
        var rwm = rw * scale;
        var rhm = rh * scale;
        using (var vp = new SolidBrush(Color.FromArgb(100, 120, 168, 255)))
        {
            g.FillRectangle(vp, rx, ry, rwm, rhm);
        }

        using (var vpPen = new Pen(EditorChrome.RibbonAccent, 1f))
        {
            g.DrawRectangle(vpPen, rx, ry, rwm, rhm);
        }

        if (_canvas.ShowMapEventMarkers && _canvas.MapEventMarkers is { Count: > 0 } markers)
        {
            var prev = g.SmoothingMode;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            try
            {
                foreach (var m in markers)
                {
                    if (m.TileX < 0 || m.TileX >= map.Width || m.TileY < 0 || m.TileY >= map.Height)
                    {
                        continue;
                    }

                    var cx = ox + (m.TileX + 0.5f) * ts * scale;
                    var cy = oy + (m.TileY + 0.5f) * ts * scale;
                    var r = Math.Max(1.25f, 2.2f * scale);
                    if (m.PlacementCount > 1)
                    {
                        r *= 1.2f;
                    }

                    var tint = MapEventMarkerColors.TintFromSlug(m.PrimarySlug);
                    var stepOn = string.Equals(m.PrimaryTriggerKind, MapEventTriggerKinds.StepOn, StringComparison.Ordinal);
                    var rect = new RectangleF(cx - r, cy - r, r * 2f, r * 2f);
                    using (var b = new SolidBrush(Color.FromArgb(228, tint)))
                    {
                        if (stepOn)
                        {
                            FillMinimapDiamond(g, b, rect);
                        }
                        else
                        {
                            g.FillEllipse(b, rect);
                        }
                    }

                    using (var edge = new Pen(Color.FromArgb(200, Color.White), 1f))
                    {
                        if (stepOn)
                        {
                            DrawMinimapDiamond(g, edge, rect);
                        }
                        else
                        {
                            g.DrawEllipse(edge, rect);
                        }
                    }
                }
            }
            finally
            {
                g.SmoothingMode = prev;
            }
        }
    }

    protected override void OnMouseClick(MouseEventArgs e)
    {
        base.OnMouseClick(e);
        if (_canvas?.Map is null || e.Button != MouseButtons.Left)
        {
            return;
        }

        var map = _canvas.Map;
        var ts = _canvas.TileSize;
        if (!TryComputeMinimapLayout(Width, Height, map, ts, out _, out var ox, out var oy, out var scale, out var drawW, out var drawH, out _, out _))
        {
            return;
        }

        var mx = e.X - ox;
        var my = e.Y - oy;
        if (mx < 0 || my < 0 || mx > drawW || my > drawH)
        {
            return;
        }

        var wx = mx / scale;
        var wy = my / scale;
        var tileX = (int)Math.Floor(wx / ts);
        var tileY = (int)Math.Floor(wy / ts);
        tileX = Math.Clamp(tileX, 0, map.Width - 1);
        tileY = Math.Clamp(tileY, 0, map.Height - 1);
        _canvas.CenterViewOnTile(tileX, tileY);
    }
}
