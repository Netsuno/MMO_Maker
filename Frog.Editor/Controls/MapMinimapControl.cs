#nullable enable
using System;
using System.Drawing;
using System.Windows.Forms;
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
            _canvas.Resize += OnCanvasResize;
        }

        Invalidate();
    }

    private void OnViewTransformChanged() => Invalidate();

    private void OnCanvasResize(object? sender, EventArgs e) => Invalidate();

    private void DetachInner()
    {
        if (_canvas is null)
        {
            return;
        }

        _canvas.ViewTransformChanged -= OnViewTransformChanged;
        _canvas.Resize -= OnCanvasResize;
        _canvas = null;
    }

    protected override void OnHandleDestroyed(EventArgs e)
    {
        DetachInner();
        base.OnHandleDestroyed(e);
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
        var mapPxW = Math.Max(1f, map.Width * (float)ts);
        var mapPxH = Math.Max(1f, map.Height * (float)ts);
        const float pad = 3f;
        var innerW = Width - 2 * pad;
        var innerH = Height - 2 * pad;
        var scale = Math.Min(innerW / mapPxW, innerH / mapPxH);
        if (scale <= 0 || float.IsInfinity(scale) || float.IsNaN(scale))
        {
            return;
        }

        var drawW = mapPxW * scale;
        var drawH = mapPxH * scale;
        var ox = pad + (innerW - drawW) * 0.5f;
        var oy = pad + (innerH - drawH) * 0.5f;

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
        var mapPxW = Math.Max(1f, map.Width * (float)ts);
        var mapPxH = Math.Max(1f, map.Height * (float)ts);
        const float pad = 3f;
        var innerW = Width - 2 * pad;
        var innerH = Height - 2 * pad;
        var scale = Math.Min(innerW / mapPxW, innerH / mapPxH);
        if (scale <= 0 || float.IsInfinity(scale) || float.IsNaN(scale))
        {
            return;
        }

        var drawW = mapPxW * scale;
        var drawH = mapPxH * scale;
        var ox = pad + (innerW - drawW) * 0.5f;
        var oy = pad + (innerH - drawH) * 0.5f;
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
