using System;
using System.Drawing;
using System.Windows.Forms;
using Frog.Core.Models;
using Frog.Core.Enums;
using Frog.Editor.Assets;

namespace Frog.Editor.Controls
{
    /// <summary>
    /// Canvas d’édition : grille, zoom/pan, rendu des tuiles, pinceau (peindre/effacer).
    /// - Clic gauche : peindre la tuile sélectionnée
    /// - Clic droit  : effacer la tuile
    /// - Molette + Ctrl : zoom
    /// - Bouton du milieu : pan
    /// </summary>
    public sealed class MapCanvas : Control
    {
        public int TileSize { get; set; } = 32;
        public float Zoom { get; private set; } = 1.0f;
        public PointF Pan { get; private set; } = new PointF(0, 0);
        public Map? Map { get; set; }

        // Pinceau courant
        public int ActiveTilesetId { get; set; } = 0;
        public Point SelectedSrc { get; set; } = new(0, 0);

        public int ActiveLayerIndex { get; set; } = 0; // 0 = Ground par défaut
        public event Action<Point>? HoveredTileChanged; // (x,y)

        private bool _panning;
        private Point _lastMouse;

        public MapCanvas()
        {
            SetStyle(ControlStyles.UserPaint | ControlStyles.AllPaintingInWmPaint | ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = Color.FromArgb(34, 34, 34);
            Cursor = Cursors.Cross;
            Dock = DockStyle.Fill;

            MouseWheel += OnMouseWheelZoom;
            MouseDown += OnMouseDown;
            MouseMove += OnMouseMove;
            MouseUp += (s, e) => { if (e.Button == MouseButtons.Middle) { _panning = false; Cursor = Cursors.Cross; } };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.Clear(BackColor);
            e.Graphics.TranslateTransform(Pan.X, Pan.Y);
            e.Graphics.ScaleTransform(Zoom, Zoom);

            int w = 20, h = 15;
            if (Map is not null) { w = Math.Max(1, Map.Width); h = Math.Max(1, Map.Height); }

            // fond checker et grille
            DrawGrid(e.Graphics, w, h);

            // rendu des tuiles par couche
            if (Map is not null)
            {
                foreach (var layer in Map.Layers)
                    DrawLayer(e.Graphics, layer);
            }

            // aperçu du pinceau
            if (Map is not null && ActiveTilesetId > 0 && TilesetCache.TryGet(ActiveTilesetId, out var bmp))
            {
                var mouse = PointToClient(Cursor.Position);
                var world = ScreenToWorld(mouse);
                int tx = (int)Math.Floor(world.X / TileSize);
                int ty = (int)Math.Floor(world.Y / TileSize);
                if (tx >= 0 && ty >= 0 && tx < Map.Width && ty < Map.Height)
                {
                    var src = new Rectangle(SelectedSrc.X, SelectedSrc.Y, TileSize, TileSize);
                    var dst = new Rectangle(tx * TileSize, ty * TileSize, TileSize, TileSize);
                    var colorMatrix = new System.Drawing.Imaging.ColorMatrix { Matrix33 = 0.5f };
                    var attrs = new System.Drawing.Imaging.ImageAttributes();
                    attrs.SetColorMatrix(colorMatrix, System.Drawing.Imaging.ColorMatrixFlag.Default, System.Drawing.Imaging.ColorAdjustType.Bitmap);
                    e.Graphics.DrawImage(bmp, dst, src.X, src.Y, src.Width, src.Height, GraphicsUnit.Pixel, attrs);
                }
            }
        }

        private void DrawLayer(Graphics g, Layer layer)
        {
            foreach (var t in layer.Tiles)
            {
                if (!TilesetCache.TryGet(t.TilesetId, out var bmp) || bmp is null) continue;
                var src = new Rectangle(t.SrcX, t.SrcY, TileSize, TileSize);
                var dst = new Rectangle(t.X * TileSize, t.Y * TileSize, TileSize, TileSize);
                if (src.Right > bmp.Width || src.Bottom > bmp.Height) continue; // sécurité
                g.DrawImage(bmp, dst, src, GraphicsUnit.Pixel);
            }
        }

        private void DrawGrid(Graphics g, int widthTiles, int heightTiles)
        {
            using var penMajor = new Pen(Color.FromArgb(70, 70, 70), 1f);
            using var penMinor = new Pen(Color.FromArgb(55, 55, 55), 1f);
            using var light = new SolidBrush(Color.FromArgb(38, 38, 38));
            using var dark = new SolidBrush(Color.FromArgb(42, 42, 42));

            for (int y = 0; y < heightTiles; y++)
            {
                for (int x = 0; x < widthTiles; x++)
                {
                    var r = new Rectangle(x * TileSize, y * TileSize, TileSize, TileSize);
                    g.FillRectangle(((x + y) % 2 == 0) ? light : dark, r);
                }
            }

            for (int x = 0; x <= widthTiles; x++)
                g.DrawLine(penMinor, x * TileSize, 0, x * TileSize, heightTiles * TileSize);
            for (int y = 0; y <= heightTiles; y++)
                g.DrawLine(penMinor, 0, y * TileSize, widthTiles * TileSize, y * TileSize);

            g.DrawRectangle(penMajor, 0, 0, widthTiles * TileSize, heightTiles * TileSize);
        }

        private void OnMouseWheelZoom(object? sender, MouseEventArgs e)
        {
            if ((ModifierKeys & Keys.Control) == 0) return;

            var before = ScreenToWorld(e.Location);
            float factor = e.Delta > 0 ? 1.1f : 1f / 1.1f;
            float newZoom = Math.Clamp(Zoom * factor, 0.25f, 4f);
            if (Math.Abs(newZoom - Zoom) < 0.0001f) return;

            Zoom = newZoom;
            var after = ScreenToWorld(e.Location);
            Pan = new PointF(Pan.X + (e.Location.X - (after.X - before.X)),
                             Pan.Y + (e.Location.Y - (after.Y - before.Y)));
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

            if (Map is null) return;

            var world = ScreenToWorld(e.Location);
            int tx = (int)Math.Floor(world.X / TileSize);
            int ty = (int)Math.Floor(world.Y / TileSize);
            if (tx < 0 || ty < 0 || tx >= Map.Width || ty >= Map.Height) return;

            if (e.Button == MouseButtons.Left)
            {
                // Peindre
                EnsureLayerExists();
                var layer = Map.Layers[ActiveLayerIndex];
                // On remplace s’il existe déjà une tuile à ces coords dans cette couche
                layer.Tiles.RemoveAll(t => t.X == tx && t.Y == ty);
                layer.Tiles.Add(new Tile
                {
                    X = tx,
                    Y = ty,
                    TilesetId = ActiveTilesetId,
                    SrcX = SelectedSrc.X,
                    SrcY = SelectedSrc.Y,
                    Type = TileType.Ground // TODO: selon la couche
                });
                Invalidate();
            }
            else if (e.Button == MouseButtons.Right)
            {
                // Effacer
                if (ActiveLayerIndex >= 0 && ActiveLayerIndex < Map.Layers.Count)
                {
                    var layer = Map.Layers[ActiveLayerIndex];
                    layer.Tiles.RemoveAll(t => t.X == tx && t.Y == ty);
                    Invalidate();
                }
            }
        }

        private void EnsureLayerExists()
        {
            if (Map is null) return;
            while (Map.Layers.Count <= ActiveLayerIndex)
            {
                Map.Layers.Add(new Layer { LayerType = LayerType.Ground });
            }
        }

        private void OnMouseMove(object? sender, MouseEventArgs e)
        {
            if (_panning)
            {
                Pan = new PointF(Pan.X + (e.Location.X - _lastMouse.X),
                                 Pan.Y + (e.Location.Y - _lastMouse.Y));
                _lastMouse = e.Location;
                Invalidate();
                return;
            }

            if (Map is null) return;
            var w = ScreenToWorld(e.Location);
            int tx = (int)Math.Floor(w.X / TileSize);
            int ty = (int)Math.Floor(w.Y / TileSize);
            if (tx >= 0 && ty >= 0 && tx < Map.Width && ty < Map.Height)
                HoveredTileChanged?.Invoke(new Point(tx, ty));
        }

        private PointF ScreenToWorld(Point p)
        {
            float x = (p.X - Pan.X) / Zoom;
            float y = (p.Y - Pan.Y) / Zoom;
            return new PointF(x, y);
        }
    }
}
