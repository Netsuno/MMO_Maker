using System;
using System.Drawing;
using System.Windows.Forms;

using Frog.Editor.Assets;

namespace Frog.Editor.Controls
{
    /// <summary>
    /// Affiche le tileset et permet de sélectionner une tuile (srcX, srcY) selon TileSize.
    /// </summary>
    public sealed class PaletteView : Control
    {
        public int TileSize { get; set; } = 32;
        public int TilesetId { get; private set; } = 0;

        public event Action<Point>? SelectedTileChanged; // (srcX, srcY) en pixels

        private Point _selected = new(0, 0);
        private VScrollBar _scroll = new();

        public PaletteView()
        {
            DoubleBuffered = true;
            BackColor = Color.FromArgb(45, 45, 45);
            Dock = DockStyle.Fill;

            _scroll.Dock = DockStyle.Right;
            _scroll.Width = 16;
            _scroll.ValueChanged += (s, e) => Invalidate();
            Controls.Add(_scroll);

            MouseDown += OnMouseDownSelect;
            Resize += (s, e) => UpdateScroll();
        }

        public void SetTileset(int tilesetId)
        {
            TilesetId = tilesetId;
            _selected = new Point(0, 0);
            UpdateScroll();
            Invalidate();
            SelectedTileChanged?.Invoke(_selected);
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

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.Clear(BackColor);

            if (!TilesetCache.TryGet(TilesetId, out var bmp) || bmp is null) return;

            // décalage vertical (scroll)
            e.Graphics.TranslateTransform(0, -_scroll.Value);

            // dessine l’image
            e.Graphics.DrawImageUnscaled(bmp, 0, 0);

            // grille
            using var pen = new Pen(Color.FromArgb(60, 60, 60));
            for (int x = 0; x <= bmp.Width; x += TileSize)
                e.Graphics.DrawLine(pen, x, 0, x, bmp.Height);
            for (int y = 0; y <= bmp.Height; y += TileSize)
                e.Graphics.DrawLine(pen, 0, y, bmp.Width, y);

            // sélection
            using var penSel = new Pen(Color.Orange, 2);
            var r = new Rectangle(_selected.X, _selected.Y, TileSize, TileSize);
            e.Graphics.DrawRectangle(penSel, r);
        }

        private void OnMouseDownSelect(object? sender, MouseEventArgs e)
        {
            if (!TilesetCache.TryGet(TilesetId, out var bmp) || bmp is null) return;

            int y = e.Y + _scroll.Value;
            int sx = (e.X / TileSize) * TileSize;
            int sy = (y / TileSize) * TileSize;
            if (sx < 0 || sy < 0 || sx >= bmp.Width || sy >= bmp.Height) return;

            _selected = new Point(sx, sy);
            Invalidate();
            SelectedTileChanged?.Invoke(_selected);
        }
    }
}
