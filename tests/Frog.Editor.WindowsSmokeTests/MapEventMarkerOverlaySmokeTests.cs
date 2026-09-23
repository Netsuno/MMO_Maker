using System.Drawing;
using System.Windows.Forms;
using Frog.Application.Maps;
using Frog.Editor.Controls;
using Frog.Editor.Services;
using Frog.Editor.Ui;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

[Collection(UiSmokeCollectionDefinition.Name)]
public sealed class MapEventMarkerOverlaySmokeTests
{
    [Fact]
    public void FormatLabel_UsesDisplayNameSlugFallbackAndTruncates()
    {
        Assert.Equal("Marchand", MapEventMarkerLayout.FormatLabel("Marchand", "pnj_marchand", 1));
        Assert.Equal("pnj_garde", MapEventMarkerLayout.FormatLabel("  ", "pnj_garde", 1));
        Assert.Equal("Événement", MapEventMarkerLayout.FormatLabel(null, " ", 1));
        Assert.Equal("Marchand · 3", MapEventMarkerLayout.FormatLabel("Marchand", "slug", 3));
        Assert.Equal("Marchand · 9+", MapEventMarkerLayout.FormatLabel("Marchand", "slug", 12));

        var truncated = MapEventMarkerLayout.FormatLabel(new string('A', 40), "slug", 1);
        Assert.Equal(MapEventMarkerLayout.MaxNameChars, truncated.Length);
        Assert.EndsWith("…", truncated);
    }

    [Fact]
    public void ShouldDrawName_RespectsToggleZoomHoverAndSelection()
    {
        Assert.True(MapEventMarkerLayout.ShouldDrawName(true, 1f, 32, hovered: false, selected: false));
        Assert.False(MapEventMarkerLayout.ShouldDrawName(true, 0.5f, 32, hovered: false, selected: false));
        Assert.True(MapEventMarkerLayout.ShouldDrawName(true, 0.5f, 32, hovered: true, selected: false));
        Assert.True(MapEventMarkerLayout.ShouldDrawName(true, 0.5f, 32, hovered: false, selected: true));
        Assert.False(MapEventMarkerLayout.ShouldDrawName(false, 2f, 32, hovered: true, selected: true));
    }

    [Fact]
    public void HitTest_DiamondAndNameLabel()
    {
        const int tile = 32;
        var diamond = MapEventMarkerLayout.DiamondBounds(2, 3, tile);
        var cx = diamond.X + diamond.Width / 2f;
        var cy = diamond.Y + diamond.Height / 2f;
        var label = MapEventMarkerLayout.FormatLabel("Gardien", "pnj", 1);
        var name = MapEventMarkerLayout.NameLabelBounds(2, 3, tile, label);

        Assert.True(MapEventMarkerLayout.HitTest(2, 3, tile, cx, cy, includeNameLabel: false, label));
        Assert.False(MapEventMarkerLayout.HitTest(2, 3, tile, 64f, 96f, includeNameLabel: true, label));
        Assert.True(MapEventMarkerLayout.HitTest(2, 3, tile, name.X + name.Width / 2f, name.Y + name.Height / 2f, includeNameLabel: true, label));
        Assert.False(MapEventMarkerLayout.HitTest(2, 3, tile, name.X + name.Width / 2f, name.Y + name.Height / 2f, includeNameLabel: false, label));
    }

    [Fact]
    public void FindPlacementIndex_PrefersKeyThenTile()
    {
        var rows = new[]
        {
            new MapEventMarkerLayout.MapEventPlacementListKey("aaa", 1, 1),
            new MapEventMarkerLayout.MapEventPlacementListKey("BBB", 4, 2),
            new MapEventMarkerLayout.MapEventPlacementListKey("ccc", 4, 2),
        };

        Assert.Equal(1, MapEventMarkerLayout.FindPlacementIndex(rows, "bbb", 1, 1));
        Assert.Equal(1, MapEventMarkerLayout.FindPlacementIndex(rows, null, 4, 2));
        Assert.Equal(-1, MapEventMarkerLayout.FindPlacementIndex(rows, "missing", 9, 9));
    }

    [Fact]
    public void ToMarkerViews_KeepsPrimaryNameAndPlacementKey()
    {
        var mapId = Guid.NewGuid();
        var eventId = Guid.NewGuid();
        var first = Guid.Parse("00000000-0000-0000-0000-000000000001");
        var second = Guid.Parse("00000000-0000-0000-0000-000000000002");
        var rows = new[]
        {
            new PgMapEventPlacementRow(second, mapId, eventId, 4, 5, "slug_b", "Beta", "action"),
            new PgMapEventPlacementRow(first, mapId, eventId, 4, 5, "slug_a", "Alpha", "player_contact"),
        };

        var view = Assert.Single(MapEventsPostgreSqlService.ToMarkerViews(rows));
        Assert.Equal(4, view.TileX);
        Assert.Equal(5, view.TileY);
        Assert.Equal(2, view.PlacementCount);
        Assert.Equal("slug_a", view.PrimarySlug);
        Assert.Equal("Alpha", view.PrimaryDisplayName);
        Assert.Equal(first.ToString("D"), view.PrimaryPlacementKey);

        var legacy = new[]
        {
            new MapEventPlacementRow(9, 1, 3, 2, 2, "coffre", "Coffre", "interact"),
            new MapEventPlacementRow(2, 1, 3, 2, 2, "porte", "Porte", "step_on"),
        };
        var legacyView = Assert.Single(MapEventsMariaDbReader.ToMarkerViews(legacy));
        Assert.Equal("porte", legacyView.PrimarySlug);
        Assert.Equal("Porte", legacyView.PrimaryDisplayName);
        Assert.Equal("2", legacyView.PrimaryPlacementKey);
        Assert.Equal(2, legacyView.PlacementCount);
    }

    [Fact]
    public void Canvas_ClickOnDiamondSelectsWithoutPainting_AndNameFollowsZoom()
    {
        StaTestRunner.Run(() =>
        {
            EditorSmokeTestAccess.ResetHooks();
            var canvas = new MapCanvas { TileSize = 32 };
            canvas.Map = DemoMapFactory.CreateStarter();
            var key = Guid.NewGuid().ToString("D");
            var marker = new MapEventMarkerView(2, 3, 1, "pnj_garde", "action", "Gardien", key);
            canvas.MapEventMarkers = new[] { marker };
            Assert.True(canvas.ShowMapEventNames);

            var diamond = MapEventMarkerLayout.DiamondBounds(2, 3, 32);
            var cx = diamond.X + diamond.Width / 2;
            var cy = diamond.Y + diamond.Height / 2;
            MapEventMarkerView? picked = null;
            canvas.MapEventMarkerPicked += view => picked = view;

            canvas.RaiseMouseDownForTest(MouseButtons.Left, cx, cy);
            Assert.Equal(key, canvas.SelectedMapEventPlacementKeyForTest);
            Assert.Equal(key, picked?.PrimaryPlacementKey);
            Assert.Equal("Gardien", canvas.ActiveMapEventCaption);
            Assert.False(canvas.History.CanUndo);
            canvas.RaiseMouseUpForTest(MouseButtons.Left, cx, cy);

            canvas.RaiseMouseDownForTest(MouseButtons.Left, 65, 97);
            Assert.True(canvas.History.CanUndo);
            canvas.RaiseMouseUpForTest(MouseButtons.Left, 65, 97);

            Assert.True(canvas.ShouldDrawMapEventNameForTest(marker));
            canvas.SetZoomForTest(0.5f);
            canvas.ShowMapEventNames = true;
            Assert.True(canvas.ShouldDrawMapEventNameForTest(marker));
            canvas.HighlightMapEventMarker(-1, -1, "");
            Assert.False(canvas.ShouldDrawMapEventNameForTest(marker));
            canvas.SetHoveredMapEventAtWorldForTest(cx, cy);
            Assert.True(canvas.ShouldDrawMapEventNameForTest(marker));

            canvas.ShowMapEventNames = false;
            Assert.False(canvas.ShouldDrawMapEventNameForTest(marker));
        });
    }

    [Fact]
    public void Canvas_PaintsColoredDiamondDistinctFromEmptyTile()
    {
        StaTestRunner.Run(() =>
        {
            EditorSmokeTestAccess.ResetHooks();
            using var host = new Form
            {
                ShowInTaskbar = false,
                FormBorderStyle = FormBorderStyle.None,
                StartPosition = FormStartPosition.Manual,
                ClientSize = new Size(220, 180),
            };
            var canvas = new MapCanvas { TileSize = 32, Dock = DockStyle.Fill };
            host.Controls.Add(canvas);
            host.Show();
            try
            {
                canvas.Map = DemoMapFactory.CreateStarter();
                canvas.MapEventMarkers = new[]
                {
                    new MapEventMarkerView(1, 1, 1, "pnj_garde", "action", "Gardien", "placement-1"),
                };
                canvas.Refresh();
                System.Windows.Forms.Application.DoEvents();

                using var bmp = new Bitmap(canvas.Width, canvas.Height);
                canvas.DrawToBitmap(bmp, new Rectangle(0, 0, bmp.Width, bmp.Height));
                var diamond = MapEventMarkerLayout.DiamondBounds(1, 1, 32);
                var markerPixel = bmp.GetPixel(diamond.X + diamond.Width / 2, diamond.Y + diamond.Height / 2);
                var emptyPixel = bmp.GetPixel(8, 8);
                var distance =
                    Math.Abs(markerPixel.R - emptyPixel.R)
                    + Math.Abs(markerPixel.G - emptyPixel.G)
                    + Math.Abs(markerPixel.B - emptyPixel.B);
                Assert.True(distance > 40, $"diamond={markerPixel} empty={emptyPixel} distance={distance}");

                var label = MapEventMarkerLayout.FormatLabel("Gardien", "pnj_garde", 1);
                var name = MapEventMarkerLayout.NameLabelBounds(1, 1, 32, label);
                var nameX = (int)(name.X + Math.Max(4f, 32f / 10f));
                var nameY = (int)(name.Y + 1f);
                Assert.InRange(nameX, 0, bmp.Width - 1);
                Assert.InRange(nameY, 0, bmp.Height - 1);
                var namePixel = bmp.GetPixel(nameX, nameY);
                Assert.True(namePixel.R + namePixel.G + namePixel.B < 180, $"nameplate={namePixel}");
            }
            finally
            {
                host.Close();
            }
        });
    }
}
