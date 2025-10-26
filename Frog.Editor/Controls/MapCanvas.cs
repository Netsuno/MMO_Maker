using System;
using System.Drawing;
using System.Windows.Forms;

using Frog.Core.Models;

namespace Frog.Editor.Controls
{
    /// <summary>
    /// Canvas d’édition : affiche une grille, supporte le zoom (Ctrl+Molette) et le pan (bouton du milieu).
    /// Le rendu des tuiles sera ajouté à l’étape suivante.
    /// </summary>
    public sealed class MapCanvas : Control
    {
        public int TileSize { get; set; } = 32;
        public float Zoom { get; private set; } = 1.0f;
        public PointF Pan { get; private set; } = new PointF(0, 0);
        public Map? Map { get; set; }

        public event Action<Point>? HoveredTileChanged; // (x,y) en coordonnées tuiles
        private bool _panning;
        private Point _lastMouse;

        public MapCanvas()
        {
            SetStyle(ControlStyles.UserPaint |
                     ControlStyles.AllPaintingInWmPaint |
                     ControlStyles.OptimizedDoubleBuffer, true);
            BackColor = Color.FromArgb(34, 34, 34);
            Cursor = Cursors.Cross;
            Dock = DockStyle.Fill;

            MouseWheel += OnMouseWheelZoom;
            MouseDown += OnMouseDown;
            MouseMove += OnMouseMove;
            MouseUp += (s, e) => { if (e.Button == MouseButtons.Middle) _panning = false; };
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.Clear(BackColor);
            e.Graphics.TranslateTransform(Pan.X, Pan.Y);
            e.Graphics.ScaleTransform(Zoom, Zoom);

            // Dessine la zone de la carte si définie
            if (Map is not null && Map.Width > 0 && Map.Height > 0)
            {
                DrawGrid(e.Graphics, Map.Width, Map.Height);
            }
            else
            {
                // Grille “par défaut” 20x15 si pas de map
                DrawGrid(e.Graphics, 20, 15);
            }
        }

        private void DrawGrid(Graphics g, int widthTiles, int heightTiles)
        {
            using var penMajor = new Pen(Color.FromArgb(70, 70, 70), 1f);
            using var penMinor = new Pen(Color.FromArgb(55, 55, 55), 1f);

            // fond checker léger (optionnel)
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

            // lignes mineures
            for (int x = 0; x <= widthTiles; x++)
            {
                int px = x * TileSize;
                g.DrawLine(penMinor, px, 0, px, heightTiles * TileSize);
            }
            for (int y = 0; y <= heightTiles; y++)
            {
                int py = y * TileSize;
                g.DrawLine(penMinor, 0, py, widthTiles * TileSize, py);
            }

            // contour principal
            g.DrawRectangle(penMajor, 0, 0, widthTiles * TileSize, heightTiles * TileSize);
        }

        private void OnMouseWheelZoom(object? sender, MouseEventArgs e)
        {
            if ((ModifierKeys & Keys.Control) == 0)
                return;

            // zoom autour du pointeur
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

            // calcule la tuile sous la souris
            var world = ScreenToWorld(e.Location);
            int tx = (int)Math.Floor(world.X / TileSize);
            int ty = (int)Math.Floor(world.Y / TileSize);
            if (tx >= 0 && ty >= 0)
                HoveredTileChanged?.Invoke(new Point(tx, ty));
        }

        private PointF ScreenToWorld(Point p)
        {
            // inverse de (translate + scale)
            float x = (p.X - Pan.X) / Zoom;
            float y = (p.Y - Pan.Y) / Zoom;
            return new PointF(x, y);
        }
    }
}
