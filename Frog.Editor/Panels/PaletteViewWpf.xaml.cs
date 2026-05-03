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
    private System.Windows.Point _dragAnchorDip;
    private System.Windows.Point _dragCurrentDip;

    public PaletteViewWpf()
    {
        InitializeComponent();
        Loaded += (_, _) =>
        {
            if (TilesetId != 0 && TilesetCache.TryGet(TilesetId, out var b) && b is not null)
            {
                SetTileset(TilesetId);
            }
        };
    }

    private double PixelsPerDip => VisualTreeHelper.GetDpi(this).PixelsPerDip;

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

        var ppd = PixelsPerDip;
        var wDip = bmp.Width / ppd;
        var hDip = bmp.Height / ppd;

        TilesetImg.Source = BitmapToWpf.ToFrozenPng(bmp);
        TilesetImg.Width = wDip;
        TilesetImg.Height = hDip;
        Canvas.SetLeft(TilesetImg, 0);
        Canvas.SetTop(TilesetImg, 0);
        Sheet.Width = wDip;
        Sheet.Height = hDip;
        BuildGridLines(bmp.Width, bmp.Height, ts, ppd);
        PositionOverlayRects(ppd);
        SelectionRect.Visibility = Visibility.Visible;
        RaiseStampChanged();
    }

    protected override void OnDpiChanged(DpiScale oldDpi, DpiScale newDpi)
    {
        base.OnDpiChanged(oldDpi, newDpi);
        if (TilesetId != 0 && TilesetCache.TryGet(TilesetId, out var b) && b is not null)
        {
            SetTileset(TilesetId);
        }
    }

    private void BuildGridLines(int bmpW, int bmpH, int tilePx, double pxPerDip)
    {
        var pen = new SolidColorBrush(MediaColor.FromRgb(76, 80, 90));
        pen.Freeze();
        var hDip = bmpH / pxPerDip;
        var wDip = bmpW / pxPerDip;
        for (var xPix = 0; xPix <= bmpW; xPix += tilePx)
        {
            var xd = xPix / pxPerDip;
            GridOverlay.Children.Add(new Line
            {
                X1 = xd,
                Y1 = 0,
                X2 = xd,
                Y2 = hDip,
                Stroke = pen,
                StrokeThickness = 1,
            });
        }

        for (var yPix = 0; yPix <= bmpH; yPix += tilePx)
        {
            var yd = yPix / pxPerDip;
            GridOverlay.Children.Add(new Line
            {
                X1 = 0,
                Y1 = yd,
                X2 = wDip,
                Y2 = yd,
                Stroke = pen,
                StrokeThickness = 1,
            });
        }
    }

    private static System.Windows.Point SnapSheetDipToTileGridDip(System.Windows.Point sheetDip, int tilePx, double pxPerDip)
    {
        var cellDip = tilePx / pxPerDip;
        var sx = Math.Max(0, Math.Floor(sheetDip.X / cellDip) * cellDip);
        var sy = Math.Max(0, Math.Floor(sheetDip.Y / cellDip) * cellDip);
        return new System.Windows.Point(sx, sy);
    }

    private static int DipToPixelFloor(double dip, double pxPerDip) =>
        (int)Math.Floor(dip * pxPerDip);

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

    private static void ApplyPixelRectToElement(System.Drawing.Rectangle rect, FrameworkElement el, double pxPerDip)
    {
        Canvas.SetLeft(el, rect.X / pxPerDip);
        Canvas.SetTop(el, rect.Y / pxPerDip);
        el.Width = rect.Width / pxPerDip;
        el.Height = rect.Height / pxPerDip;
    }

    private void Sheet_OnMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (!TilesetCache.TryGet(TilesetId, out var bmp) || bmp is null)
        {
            return;
        }

        var ppd = PixelsPerDip;
        var sheetPt = e.GetPosition(Sheet);
        _dragSelect = true;
        _dragAnchorDip = SnapSheetDipToTileGridDip(sheetPt, Math.Max(1, TileSize), ppd);
        _dragCurrentDip = _dragAnchorDip;
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
            var ppd = PixelsPerDip;
            var ts = Math.Max(1, TileSize);
            _dragCurrentDip = SnapSheetDipToTileGridDip(e.GetPosition(Sheet), ts, ppd);
            var aPix = new System.Drawing.Point(DipToPixelFloor(_dragAnchorDip.X, ppd), DipToPixelFloor(_dragAnchorDip.Y, ppd));
            var bPix = new System.Drawing.Point(DipToPixelFloor(_dragCurrentDip.X, ppd), DipToPixelFloor(_dragCurrentDip.Y, ppd));
            var dragRect = NormalizeStampRect(aPix, bPix, ts, bmp.Width, bmp.Height);
            ApplyPixelRectToElement(dragRect, DragPreviewRect, ppd);
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
        var ppd = PixelsPerDip;
        var ts = Math.Max(1, TileSize);
        _dragCurrentDip = SnapSheetDipToTileGridDip(e.GetPosition(Sheet), ts, ppd);
        var aPix = new System.Drawing.Point(DipToPixelFloor(_dragAnchorDip.X, ppd), DipToPixelFloor(_dragAnchorDip.Y, ppd));
        var bPix = new System.Drawing.Point(DipToPixelFloor(_dragCurrentDip.X, ppd), DipToPixelFloor(_dragCurrentDip.Y, ppd));
        var rect = NormalizeStampRect(aPix, bPix, ts, bmp.Width, bmp.Height);
        _stampOrigin = rect.Location;
        _stampSizePixels = rect.Size;
        PositionOverlayRects(ppd);
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

    private void PositionOverlayRects(double pxPerDip)
    {
        Canvas.SetLeft(GridOverlay, 0);
        Canvas.SetTop(GridOverlay, 0);
        GridOverlay.Width = Sheet.Width;
        GridOverlay.Height = Sheet.Height;
        var sel = new System.Drawing.Rectangle(_stampOrigin, _stampSizePixels);
        ApplyPixelRectToElement(sel, SelectionRect, pxPerDip);
    }

    private void RaiseStampChanged()
    {
        var r = new System.Drawing.Rectangle(_stampOrigin, _stampSizePixels);
        StampSelectionChanged?.Invoke(r);
    }
}
