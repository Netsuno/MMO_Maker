#nullable enable
using System.Drawing;
using System.Windows.Forms;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Maps;
using Frog.Core.Models;

namespace Frog.Client.UI;

/// <summary>
/// Minimap HD : cache bitmap au <c>MapDataReceived</c> seulement.
/// Point joueur = invalidate locale (pas <c>MapViewRenderer.Render</c>, pas timer 16 ms).
/// </summary>
public sealed class HudMinimapModule : HudModulePanel
{
    private readonly PictureBox _view = new()
    {
        Dock = DockStyle.Fill,
        SizeMode = PictureBoxSizeMode.StretchImage,
        BackColor = Color.FromArgb(30, 40, 30),
    };
    private readonly Label _zone = new()
    {
        Dock = DockStyle.Bottom,
        Height = 18,
        TextAlign = ContentAlignment.MiddleLeft,
        ForeColor = UiTheme.TextSecondary,
        BackColor = UiTheme.BgPanelHeader,
        Text = "Zone: —",
    };

    private Bitmap? _cache;
    private int _mapW;
    private int _mapH;
    private int _playerTx = -1;
    private int _playerTy = -1;

    public HudMinimapModule()
        : base("Carte")
    {
        Size = new Size(180, 180);
        MinimumSize = new Size(140, 140);
        Controls.Add(_view);
        Controls.Add(_zone);
        _view.BringToFront();
    }

    internal string ZoneTextForTest => _zone.Text;

    internal bool HasCacheForTest => _cache is not null;

    internal (int Tx, int Ty) PlayerTileForTest => (_playerTx, _playerTy);

    public void RebuildCache(Map? map)
    {
        _cache?.Dispose();
        _cache = null;
        _mapW = 0;
        _mapH = 0;
        if (map is null || map.Width <= 0 || map.Height <= 0)
        {
            _zone.Text = "Zone: —";
            _view.Image = null;
            return;
        }

        _mapW = map.Width;
        _mapH = map.Height;
        _zone.Text = string.IsNullOrWhiteSpace(map.Name) ? $"Zone: {map.Width}×{map.Height}" : map.Name;
        var blocked = MapCollision.IndexBlockedTiles(map);
        var bmp = new Bitmap(map.Width, map.Height);
        using (var g = Graphics.FromImage(bmp))
        {
            g.Clear(Color.FromArgb(70, 100, 70));
            foreach (var layer in map.Layers)
            {
                if (layer.LayerType == LayerType.Attributes)
                {
                    continue;
                }

                foreach (var tile in layer.Tiles)
                {
                    var color = tile.Type switch
                    {
                        TileType.Block => Color.FromArgb(45, 45, 55),
                        TileType.Warp => Color.FromArgb(180, 100, 200),
                        _ => Color.FromArgb(90, 130, 80),
                    };
                    bmp.SetPixel(Math.Clamp(tile.X, 0, map.Width - 1), Math.Clamp(tile.Y, 0, map.Height - 1), color);
                }
            }

            foreach (var (x, y) in blocked)
            {
                if ((uint)x < (uint)map.Width && (uint)y < (uint)map.Height)
                {
                    bmp.SetPixel(x, y, Color.FromArgb(45, 45, 55));
                }
            }
        }

        _cache = bmp;
        PaintPlayerDot();
    }

    public void SetPlayerPixel(int pixelX, int pixelY)
    {
        if (_mapW <= 0 || _mapH <= 0)
        {
            return;
        }

        var tw = WorldMetrics.DefaultTileSizePixels;
        var tx = Math.Clamp(pixelX / tw, 0, _mapW - 1);
        var ty = Math.Clamp(pixelY / tw, 0, _mapH - 1);
        if (tx == _playerTx && ty == _playerTy)
        {
            return;
        }

        _playerTx = tx;
        _playerTy = ty;
        PaintPlayerDot();
    }

    private void PaintPlayerDot()
    {
        if (_cache is null)
        {
            _view.Image = null;
            return;
        }

        var shown = new Bitmap(_cache);
        if (_playerTx >= 0 && _playerTy >= 0 && _playerTx < shown.Width && _playerTy < shown.Height)
        {
            shown.SetPixel(_playerTx, _playerTy, UiTheme.AccentGoldHi);
        }

        var previous = _view.Image;
        _view.Image = shown;
        previous?.Dispose();
    }

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            var shown = _view.Image;
            _view.Image = null;
            shown?.Dispose();
            _cache?.Dispose();
            _cache = null;
        }

        base.Dispose(disposing);
    }
}
