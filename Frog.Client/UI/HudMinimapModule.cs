#nullable enable
using System.Drawing;
using System.Windows.Forms;
using Frog.Core.Maps;
using Frog.Core.Models;

namespace Frog.Client.UI;

/// <summary>
/// Minicarte coin d'écran : tuiles autour du joueur, cache au <c>MapDataReceived</c> seulement.
/// Le point joueur est local (pas de passe tuiles 60 Hz, pas le timer 16 ms, pas d'opcode).
/// </summary>
public sealed class HudMinimapModule : HudModulePanel
{
    private readonly PictureBox _view = new()
    {
        Dock = DockStyle.Fill,
        SizeMode = PictureBoxSizeMode.CenterImage,
        BackColor = Color.FromArgb(12, 16, 22),
        AccessibleName = "Tuiles autour du joueur",
    };

    private readonly Label _zone = new()
    {
        Dock = DockStyle.Bottom,
        Height = 18,
        AutoEllipsis = true,
        TextAlign = ContentAlignment.MiddleLeft,
        ForeColor = UiTheme.TextSecondary,
        BackColor = UiTheme.BgPanelHeader,
        Text = "Zone: —",
    };

    private MinimapWindow.Index _index = MinimapWindow.Index.Empty;
    private string _zoneName = "Zone: —";
    private int _playerTx = -1;
    private int _playerTy = -1;
    private bool _painting;

    public HudMinimapModule()
        : base("Carte")
    {
        AccessibleName = "Minicarte";
        Size = new Size(180, 180);
        MinimumSize = new Size(140, 140);
        Controls.Add(_view);
        Controls.Add(_zone);
        _view.BringToFront();
        _view.Resize += (_, _) => PaintNearby();
    }

    internal string ZoneTextForTest => _zone.Text;

    internal bool HasCacheForTest => !_index.IsEmpty;

    internal (int Tx, int Ty) PlayerTileForTest => (_playerTx, _playerTy);

    internal int NearbyRadiusForTest => MinimapWindow.NearbyRadiusTiles;

    internal int WindowSpanForTest => MinimapWindow.WindowSpanTiles;

    public void RebuildCache(Map? map)
    {
        _index = MinimapWindow.Build(map);
        _playerTx = -1;
        _playerTy = -1;
        if (map is null || map.Width <= 0 || map.Height <= 0)
        {
            _zoneName = "Zone: —";
        }
        else
        {
            _zoneName = string.IsNullOrWhiteSpace(map.Name) ? $"Zone: {map.Width}×{map.Height}" : map.Name;
        }

        RefreshZoneLabel();
        PaintNearby();
    }

    public void SetPlayerPixel(int pixelX, int pixelY)
    {
        if (_index.IsEmpty)
        {
            return;
        }

        var (tx, ty) = MinimapWindow.TileFromPixels(pixelX, pixelY, _index.Width, _index.Height);
        if (tx == _playerTx && ty == _playerTy)
        {
            return;
        }

        _playerTx = tx;
        _playerTy = ty;
        RefreshZoneLabel();
        PaintNearby();
    }

    private void RefreshZoneLabel()
    {
        _zone.Text = _playerTx < 0
            ? _zoneName
            : $"{_zoneName} · tuile {_playerTx},{_playerTy}";
    }

    private void PaintNearby()
    {
        if (_painting || IsDisposed || _view.IsDisposed)
        {
            return;
        }

        _painting = true;
        try
        {
            if (_index.IsEmpty)
            {
                ReplaceImage(null);
                return;
            }

            var span = MinimapWindow.WindowSpanTiles;
            var availW = _view.ClientSize.Width;
            var availH = _view.ClientSize.Height;
            if (availW < 16 || availH < 16)
            {
                availW = 104;
                availH = 104;
            }

            var cell = Math.Max(2, Math.Min(availW, availH) / span);
            var edge = span * cell;
            var bmp = new Bitmap(edge, edge);
            var window = MinimapWindow.Sample(_index, _playerTx, _playerTy);
            var (originX, originY) = MinimapWindow.WindowOrigin(_playerTx, _playerTy);
            using (var g = Graphics.FromImage(bmp))
            using (var outside = new SolidBrush(Color.FromArgb(12, 16, 22)))
            using (var open = new SolidBrush(Color.FromArgb(70, 100, 70)))
            using (var ground = new SolidBrush(Color.FromArgb(90, 130, 80)))
            using (var block = new SolidBrush(Color.FromArgb(45, 45, 55)))
            using (var warp = new SolidBrush(Color.FromArgb(180, 100, 200)))
            using (var player = new SolidBrush(UiTheme.TextPrimary))
            {
                g.Clear(Color.FromArgb(12, 16, 22));
                for (var row = 0; row < span; row++)
                {
                    for (var col = 0; col < span; col++)
                    {
                        var kind = window[(row * span) + col];
                        var brush = kind switch
                        {
                            MinimapWindow.CellKind.Ground => ground,
                            MinimapWindow.CellKind.Block => block,
                            MinimapWindow.CellKind.Warp => warp,
                            MinimapWindow.CellKind.Open => open,
                            _ => outside,
                        };
                        var rect = new Rectangle(col * cell, row * cell, cell, cell);
                        g.FillRectangle(brush, rect);
                        if (_playerTx >= 0 && originX + col == _playerTx && originY + row == _playerTy)
                        {
                            var mark = Math.Max(2, cell / 2);
                            var mx = rect.X + ((cell - mark) / 2);
                            var my = rect.Y + ((cell - mark) / 2);
                            g.FillRectangle(player, mx, my, mark, mark);
                        }
                    }
                }
            }

            ReplaceImage(bmp);
        }
        finally
        {
            _painting = false;
        }
    }

    private void ReplaceImage(Image? next)
    {
        var previous = _view.Image;
        _view.Image = next;
        previous?.Dispose();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            ReplaceImage(null);
            _index = MinimapWindow.Index.Empty;
        }

        base.Dispose(disposing);
    }
}
