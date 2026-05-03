using System;
using System.Drawing;
using System.Windows.Forms;

using Frog.Editor.Assets;
using Frog.Editor.Ui;

namespace Frog.Editor.Controls
{
    /// <summary>
    /// Affiche le tileset : clic = 1 tuile ; glisser = rectangle de tuiles (tampon multi-tuiles pour le pinceau).
    /// </summary>
    public sealed class PaletteView : Control
    {
        public int TileSize { get; set; } = 32;
        public int TilesetId { get; private set; } = 0;

        /// <summary>Rectangle source en pixels (coin haut-gauche + taille), largeur/hauteur multiples de <see cref="TileSize"/>.</summary>
        public event Action<Rectangle>? StampSelectionChanged;

        private Point _stampOrigin;
        private Size _stampSizePixels = new(32, 32);
        private VScrollBar _scroll = new();
        private bool _dragSelect;
        private Point _dragAnchorPixels;
        private Point _dragCurrentPixels;

        public PaletteView()
        {
            DoubleBuffered = true;
            BackColor = EditorChrome.PaletteStripBg;
            Dock = DockStyle.Fill;

            _scroll.Dock = DockStyle.Right;
            _scroll.Width = 16;
            _scroll.ValueChanged += (_, _) => Invalidate();
            Controls.Add(_scroll);

            MouseDown += OnMouseDown;
            MouseMove += OnMouseMove;
            MouseUp += OnMouseUp;
            Resize += (_, _) => UpdateScroll();
        }

        public void SetTileset(int tilesetId)
        {
            TilesetId = tilesetId;
            _stampOrigin = new Point(0, 0);
            var ts = Math.Max(1, TileSize);
            _stampSizePixels = new Size(ts, ts);
            _dragSelect = false;
            UpdateScroll();
            Invalidate();
            RaiseStampChanged();
        }

        private void RaiseStampChanged()
        {
            var r = new Rectangle(_stampOrigin, _stampSizePixels);
            StampSelectionChanged?.Invoke(r);
        }

        private void UpdateScroll()
        {
            if (!TilesetCache.TryGet(TilesetId, out var bmp) || bmp is null)
            {
                _scroll.Enabled = false;
                _scroll.Maximum = 0;
                _scroll.Value = 0;
                return;
            }

            int visible = Math.Max(1, Height);
            int content = bmp.Height;
            _scroll.Enabled = content > visible;
            _scroll.Maximum = Math.Max(0, content - 1);
            _scroll.LargeChange = Math.Max(1, visible);
            _scroll.SmallChange = TileSize;
        }

        private Point ClientToTilesetTopLeft(int clientX, int clientY)
        {
            int yWorld = clientY + _scroll.Value;
            int sx = Math.Max(0, (clientX / Math.Max(1, TileSize)) * TileSize);
            int sy = Math.Max(0, (yWorld / Math.Max(1, TileSize)) * TileSize);
            return new Point(sx, sy);
        }

        private static Rectangle NormalizeStampRect(Point a, Point b, int tileSize, int bmpW, int bmpH)
        {
            int x0 = Math.Min(a.X, b.X);
            int y0 = Math.Min(a.Y, b.Y);
            int x1 = Math.Max(a.X, b.X) + tileSize;
            int y1 = Math.Max(a.Y, b.Y) + tileSize;
            x0 = Math.Clamp(x0, 0, Math.Max(0, bmpW - tileSize));
            y0 = Math.Clamp(y0, 0, Math.Max(0, bmpH - tileSize));
            x1 = Math.Clamp(x1, x0 + tileSize, bmpW);
            y1 = Math.Clamp(y1, y0 + tileSize, bmpH);
            return new Rectangle(x0, y0, x1 - x0, y1 - y0);
        }

        private void OnMouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || !TilesetCache.TryGet(TilesetId, out var bmp) || bmp is null)
            {
                return;
            }

            _dragSelect = true;
            _dragAnchorPixels = ClientToTilesetTopLeft(e.X, e.Y);
            _dragCurrentPixels = _dragAnchorPixels;
            Capture = true;
            Invalidate();
        }

        private void OnMouseMove(object? sender, MouseEventArgs e)
        {
            if (!_dragSelect || !TilesetCache.TryGet(TilesetId, out var bmp) || bmp is null)
            {
                return;
            }

            if ((e.Button & MouseButtons.Left) != 0)
            {
                _dragCurrentPixels = ClientToTilesetTopLeft(e.X, e.Y);
                Invalidate();
            }
        }

        private void OnMouseUp(object? sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || !_dragSelect || !TilesetCache.TryGet(TilesetId, out var bmp) || bmp is null)
            {
                return;
            }

            _dragSelect = false;
            Capture = false;
            _dragCurrentPixels = ClientToTilesetTopLeft(e.X, e.Y);
            var rect = NormalizeStampRect(_dragAnchorPixels, _dragCurrentPixels, TileSize, bmp.Width, bmp.Height);
            _stampOrigin = rect.Location;
            _stampSizePixels = rect.Size;
            Invalidate();
            RaiseStampChanged();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.Clear(BackColor);

            if (!TilesetCache.TryGet(TilesetId, out var bmp) || bmp is null)
            {
                return;
            }

            e.Graphics.TranslateTransform(0, -_scroll.Value);
            e.Graphics.DrawImageUnscaled(bmp, 0, 0);

            using var pen = new Pen(Color.FromArgb(76, 80, 90));
            for (int x = 0; x <= bmp.Width; x += TileSize)
            {
                e.Graphics.DrawLine(pen, x, 0, x, bmp.Height);
            }

            for (int y = 0; y <= bmp.Height; y += TileSize)
            {
                e.Graphics.DrawLine(pen, 0, y, bmp.Width, y);
            }

            using var penSel = new Pen(Color.FromArgb(255, 182, 72), 2);
            e.Graphics.DrawRectangle(penSel, new Rectangle(_stampOrigin, _stampSizePixels));

            if (_dragSelect)
            {
                var dragRect = NormalizeStampRect(_dragAnchorPixels, _dragCurrentPixels, TileSize, bmp.Width, bmp.Height);
                using var penDrag = new Pen(Color.FromArgb(220, 120, 255), 2) { DashStyle = System.Drawing.Drawing2D.DashStyle.Dash };
                e.Graphics.DrawRectangle(penDrag, dragRect);
            }
        }
    }
}
