using System.Drawing;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Frog.Editor.Assets;
using Frog.Editor.Utils;
using DSize = System.Drawing.Size;
using MediaColor = System.Windows.Media.Color;

namespace Frog.Editor.Panels;

public partial class PaletteViewWpf : System.Windows.Controls.UserControl
{
    public int TileSize { get; set; } = 32;
    public int TilesetId { get; private set; }

    public event Action<System.Drawing.Rectangle>? StampSelectionChanged;

    private System.Drawing.Point _stampOrigin;
    private DSize _stampSizePixels = new(32, 32);
    private bool _dragSelect;
    private System.Windows.Point _dragAnchorPixels;
    private System.Windows.Point _dragCurrentPixels;

    public PaletteViewWpf()
    {
        InitializeComponent();
    }

    public void SetTileset(int tilesetId)
    {
        TilesetId = tilesetId;
        _stampOrigin = new System.Drawing.Point(0, 0);
        var ts = Math.Max(1, TileSize);
        _stampSizePixels = new DSize(ts, ts);
        _dragSelect = false;

        GridOverlay.Children.Clear();
        if (!TilesetCache.TryGet(tilesetId, out var bmp) || bmp is null)
        {
            TilesetImg.Source = null;
            Sheet.Width = 0;
            Sheet.Height = 0;
            SelectionRect.Visibility = Visibility.Collapsed;
            RaiseStampChanged();
            return;
        }

        TilesetImg.Source = BitmapToWpf.ToFrozenPng(bmp);
        TilesetImg.Width = bmp.Width;
        TilesetImg.Height = bmp.Height;
        Canvas.SetLeft(TilesetImg, 0);
        Canvas.SetTop(TilesetImg, 0);
        Sheet.Width = bmp.Width;
        Sheet.Height = bmp.Height;
        BuildGridLines(bmp.Width, bmp.Height, ts);
        PositionOverlayRects();
        SelectionRect.Visibility = Visibility.Visible;
        RaiseStampChanged();
    }

    private void BuildGridLines(int bmpW, int bmpH, int ts)
    {
        var pen = new SolidColorBrush(MediaColor.FromRgb(76, 80, 90));
        pen.Freeze();
        for (var x = 0; x <= bmpW; x += ts)
        {
            GridOverlay.Children.Add(new Line
            {
                X1 = x,
                Y1 = 0,
                X2 = x,
                Y2 = bmpH,
                Stroke = pen,
                StrokeThickness = 1,
            });
        }

        for (var y = 0; y <= bmpH; y += ts)
        {
            GridOverlay.Children.Add(new Line
            {
                X1 = 0,
                Y1 = y,
                X2 = bmpW,
                Y2 = y,
                Stroke = pen,
                StrokeThickness = 1,
            });
        }
    }

    private System.Windows.Point SnapToTileGrid(System.Windows.Point sheetPoint)
    {
        var ts = Math.Max(1, TileSize);
        var sx = Math.Max(0, (int)(sheetPoint.X / ts) * ts);
        var sy = Math.Max(0, (int)(sheetPoint.Y / ts) * ts);
        return new System.Windows.Point(sx, sy);
    }

    private static System.Drawing.Rectangle NormalizeStampRect(System.Drawing.Point a, System.Drawing.Point b, int tileSize, int bmpW, int bmpH)
    {
        var x0 = Math.Min(a.X, b.X);
        var y0 = Math.Min(a.Y, b.Y);
        var x1 = Math.Max(a.X, b.X) + tileSize;
        var y1 = Math.Max(a.Y, b.Y) + tileSize;
        x0 = Math.Clamp(x0, 0, Math.Max(0, bmpW - tileSize));
        y0 = Math.Clamp(y0, 0, Math.Max(0, bmpH - tileSize));
        x1 = Math.Clamp(x1, x0 + tileSize, bmpW);
        y1 = Math.Clamp(y1, y0 + tileSize, bmpH);
        return new System.Drawing.Rectangle(x0, y0, x1 - x0, y1 - y0);
    }

    private void Sheet_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!TilesetCache.TryGet(TilesetId, out var bmp) || bmp is null)
        {
            return;
        }

        var sheetPt = e.GetPosition(Sheet);
        _dragSelect = true;
        _dragAnchorPixels = SnapToTileGrid(sheetPt);
        _dragCurrentPixels = _dragAnchorPixels;
        Sheet.CaptureMouse();
        DragPreviewRect.Visibility = Visibility.Visible;
        e.Handled = true;
    }

    private void Sheet_OnMouseMove(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (!_dragSelect || !TilesetCache.TryGet(TilesetId, out var bmp) || bmp is null)
        {
            return;
        }

        if (e.LeftButton == MouseButtonState.Pressed)
        {
            _dragCurrentPixels = SnapToTileGrid(e.GetPosition(Sheet));
            var ts = Math.Max(1, TileSize);
            var dragRect = NormalizeStampRect(
                new System.Drawing.Point((int)_dragAnchorPixels.X, (int)_dragAnchorPixels.Y),
                new System.Drawing.Point((int)_dragCurrentPixels.X, (int)_dragCurrentPixels.Y),
                ts,
                bmp.Width,
                bmp.Height);
            Canvas.SetLeft(DragPreviewRect, dragRect.X);
            Canvas.SetTop(DragPreviewRect, dragRect.Y);
            DragPreviewRect.Width = dragRect.Width;
            DragPreviewRect.Height = dragRect.Height;
        }
    }

    private void Sheet_OnMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (!_dragSelect || !TilesetCache.TryGet(TilesetId, out var bmp) || bmp is null)
        {
            return;
        }

        _dragSelect = false;
        Sheet.ReleaseMouseCapture();
        _dragCurrentPixels = SnapToTileGrid(e.GetPosition(Sheet));
        var ts = Math.Max(1, TileSize);
        var rect = NormalizeStampRect(
            new System.Drawing.Point((int)_dragAnchorPixels.X, (int)_dragAnchorPixels.Y),
            new System.Drawing.Point((int)_dragCurrentPixels.X, (int)_dragCurrentPixels.Y),
            ts,
            bmp.Width,
            bmp.Height);
        _stampOrigin = rect.Location;
        _stampSizePixels = rect.Size;
        PositionOverlayRects();
        DragPreviewRect.Visibility = Visibility.Collapsed;
        RaiseStampChanged();
        e.Handled = true;
    }

    private void Sheet_OnMouseLeave(object sender, System.Windows.Input.MouseEventArgs e)
    {
        if (_dragSelect && e.LeftButton != MouseButtonState.Pressed)
        {
            _dragSelect = false;
            Sheet.ReleaseMouseCapture();
            DragPreviewRect.Visibility = Visibility.Collapsed;
        }
    }

    private void PositionOverlayRects()
    {
        Canvas.SetLeft(GridOverlay, 0);
        Canvas.SetTop(GridOverlay, 0);
        GridOverlay.Width = Sheet.Width;
        GridOverlay.Height = Sheet.Height;
        Canvas.SetLeft(SelectionRect, _stampOrigin.X);
        Canvas.SetTop(SelectionRect, _stampOrigin.Y);
        SelectionRect.Width = _stampSizePixels.Width;
        SelectionRect.Height = _stampSizePixels.Height;
    }

    private void RaiseStampChanged()
    {
        var r = new System.Drawing.Rectangle(_stampOrigin, _stampSizePixels);
        StampSelectionChanged?.Invoke(r);
    }
}
