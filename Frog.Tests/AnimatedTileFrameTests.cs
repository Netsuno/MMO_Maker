using System;
using System.IO;
using Frog.Core.Animation;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.IO;
using Frog.Core.Models;
using Xunit;

namespace Frog.Tests;

public sealed class AnimatedTileFrameTests
{
    [Theory]
    [InlineData(0, 0)]
    [InlineData(199, 0)]
    [InlineData(200, 1)]
    [InlineData(399, 1)]
    [InlineData(400, 2)]
    [InlineData(599, 2)]
    [InlineData(600, 1)]
    [InlineData(799, 1)]
    [InlineData(800, 0)]
    public void ThreeFrames_PingPongLikeRpgMakerWater(long elapsedMs, int expected)
    {
        Assert.Equal(expected, AnimatedTileFrames.FrameIndex(3, elapsedMs, 200));
    }

    [Theory]
    [InlineData(0, 0)]
    [InlineData(100, 1)]
    [InlineData(300, 3)]
    [InlineData(400, 0)]
    [InlineData(-20, 0)]
    public void OtherFrameCounts_Loop(long elapsedMs, int expected)
    {
        Assert.Equal(expected, AnimatedTileFrames.FrameIndex(4, elapsedMs, 100));
    }

    [Fact]
    public void FrameIndex_StaticOrEmpty_StaysOnFirstFrame()
    {
        Assert.Equal(0, AnimatedTileFrames.FrameIndex(1, 500, 200));
        Assert.Equal(0, AnimatedTileFrames.FrameIndex(0, 500, 200));
        Assert.Equal(0, AnimatedTileFrames.FrameIndex(3, 0, 0));
    }

    [Fact]
    public void SourcePixel_HorizontalAndVertical()
    {
        var horizontal = new AnimatedTileStrip
        {
            OriginX = 32,
            OriginY = 64,
            FrameCount = 3,
            Layout = AnimatedTileFrames.LayoutHorizontal,
        };
        AnimatedTileFrames.SourcePixel(horizontal, 2, 32, out var hx, out var hy);
        Assert.Equal(96, hx);
        Assert.Equal(64, hy);

        var vertical = new AnimatedTileStrip
        {
            OriginX = 0,
            OriginY = 0,
            FrameCount = 3,
            Layout = "VERTICAL",
        };
        AnimatedTileFrames.SourcePixel(vertical, 2, 32, out var vx, out var vy);
        Assert.Equal(0, vx);
        Assert.Equal(64, vy);
    }

    [Fact]
    public void TryDrawSource_OnlyOriginAnimates_AndRejectsOutOfSheet()
    {
        var set = new TilesetAnimationSet
        {
            TilesetId = 2,
            FrameDurationMs = 200,
            Strips =
            {
                new AnimatedTileStrip { OriginX = 0, OriginY = 0, FrameCount = 3, Layout = "horizontal" },
            },
        };

        Assert.True(AnimatedTileFrames.TryDrawSource(set, 0, 0, 32, 0, 96, 32, out var x0, out var y0));
        Assert.Equal(0, x0);
        Assert.Equal(0, y0);

        Assert.True(AnimatedTileFrames.TryDrawSource(set, 0, 0, 32, 400, 96, 32, out var x2, out var y2));
        Assert.Equal(64, x2);
        Assert.Equal(0, y2);

        Assert.False(AnimatedTileFrames.TryDrawSource(set, 32, 0, 32, 400, 96, 32, out _, out _));
        Assert.False(AnimatedTileFrames.TryDrawSource(set, 0, 0, 32, 400, 64, 32, out var fallbackX, out _));
        Assert.Equal(0, fallbackX);
    }

    [Fact]
    public void CanonicalizePaintSource_CollapsesStripOntoOrigin()
    {
        var set = new TilesetAnimationSet
        {
            Strips =
            {
                new AnimatedTileStrip { OriginX = 0, OriginY = 0, FrameCount = 3 },
            },
        };

        var sx = 64;
        var sy = 0;
        AnimatedTileFrames.CanonicalizePaintSource(set, 0, 0, 3, 1, 32, ref sx, ref sy);
        Assert.Equal(0, sx);
        Assert.Equal(0, sy);

        sx = 64;
        sy = 0;
        AnimatedTileFrames.CanonicalizePaintSource(set, 64, 0, 1, 1, 32, ref sx, ref sy);
        Assert.Equal(64, sx);
        Assert.Equal(0, sy);
    }

    [Fact]
    public void StripsFromHorizontalStamp_OneRowAndTwoRows()
    {
        var one = AnimatedTileFrames.StripsFromHorizontalStamp(0, 0, 96, 32, 32, 96, 64);
        var strip = Assert.Single(one);
        Assert.Equal(3, strip.FrameCount);
        Assert.Equal(AnimatedTileFrames.LayoutHorizontal, strip.Layout);

        var two = AnimatedTileFrames.StripsFromHorizontalStamp(0, 0, 64, 64, 32, 128, 64);
        Assert.Equal(2, two.Count);
        Assert.Equal(0, two[1].OriginX);
        Assert.Equal(32, two[1].OriginY);
        Assert.Equal(2, two[1].FrameCount);

        Assert.Empty(AnimatedTileFrames.StripsFromHorizontalStamp(0, 0, 32, 32, 32, 64, 32));
        Assert.Empty(AnimatedTileFrames.StripsFromHorizontalStamp(1, 0, 64, 32, 32, 128, 32));
    }

    [Fact]
    public void Session_MarkResolveClear_AndPreviewToggle()
    {
        var session = new TilesetAnimSession();
        var missing = session.MarkHorizontalSelection(0, 0, 0, 64, 32, 32, 128, 32);
        Assert.False(missing.Ok);
        Assert.Equal(TilesetAnimSession.NeedTilesetMessage, missing.Message);

        var narrow = session.MarkHorizontalSelection(1, 0, 0, 32, 32, 32, 128, 32);
        Assert.False(narrow.Ok);
        Assert.Equal(TilesetAnimSession.NeedHorizontalStripMessage, narrow.Message);

        var marked = session.MarkHorizontalSelection(1, 0, 0, 96, 32, 32, 128, 64);
        Assert.True(marked.Ok);
        Assert.Equal(3, marked.FrameCount);
        Assert.Equal(1, marked.RowCount);
        Assert.Contains("1re case", marked.Message, StringComparison.Ordinal);

        Assert.True(session.TryResolveDrawSource(1, 0, 0, 32, 0, 128, 64, out var x0, out var y0));
        Assert.Equal((0, 0), (x0, y0));
        Assert.True(session.TryResolveDrawSource(1, 0, 0, 32, 200, 128, 64, out var x1, out _));
        Assert.Equal(32, x1);
        Assert.True(session.TryResolveDrawSource(1, 0, 0, 32, 600, 128, 64, out var xBack, out _));
        Assert.Equal(32, xBack);
        Assert.NotEqual(session.PreviewSignature(0), session.PreviewSignature(200));

        var sx = 64;
        var sy = 0;
        session.CanonicalizePaintSource(1, 0, 0, 3, 1, 32, ref sx, ref sy);
        Assert.Equal(0, sx);

        session.PreviewEnabled = false;
        Assert.False(session.TryResolveDrawSource(1, 0, 0, 32, 200, 128, 64, out _, out _));
        Assert.Equal(0, session.PreviewSignature(200));
        session.PreviewEnabled = true;

        var cleared = session.ClearSelection(1, 32, 0, 32, 32, 32);
        Assert.True(cleared.Ok);
        Assert.Equal(TilesetAnimSession.ClearedMessage, cleared.Message);
        Assert.False(session.HasAnyStrip);
        Assert.Equal(TilesetAnimSession.NothingToClearMessage, session.ClearSelection(1, 0, 0, 32, 32, 32).Message);
    }

    [Fact]
    public void Json_Roundtrip_ClampsAndRejectsUnknownVersion()
    {
        var doc = new TilesetAnimationDocument
        {
            Tilesets =
            {
                new TilesetAnimationSet
                {
                    TilesetId = 4,
                    FrameDurationMs = 0,
                    Strips =
                    {
                        new AnimatedTileStrip { OriginX = 0, OriginY = 16, FrameCount = 12, Layout = "vertical" },
                        new AnimatedTileStrip { OriginX = 0, OriginY = 16, FrameCount = 3, Layout = "horizontal" },
                        new AnimatedTileStrip { OriginX = -1, OriginY = 0, FrameCount = 2 },
                    },
                },
            },
        };

        var back = TilesetAnimationJson.TryDeserializeDocument(TilesetAnimationJson.SerializeDocument(doc));
        Assert.NotNull(back);
        var set = Assert.Single(back!.Tilesets);
        Assert.Equal(4, set.TilesetId);
        Assert.Equal(AnimatedTileFrames.DefaultFrameDurationMs, set.FrameDurationMs);
        var strip = Assert.Single(set.Strips);
        Assert.Equal(AnimatedTileFrames.MaxFrameCount, strip.FrameCount);
        Assert.Equal(AnimatedTileFrames.LayoutVertical, strip.Layout);
        Assert.Equal(16, strip.OriginY);

        var future = """{"documentVersion":9,"tilesets":[]}"""u8.ToArray();
        Assert.Null(TilesetAnimationJson.TryDeserializeDocument(future));

        var image = TilesetAnimationJson.TryDeserializeSet(TilesetAnimationJson.SerializeSet(set));
        Assert.NotNull(image);
        Assert.Equal(4, image!.TilesetId);
    }

    [Fact]
    public void SidecarPaths_DoNotTouchFmapVersion()
    {
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Equal((byte)5, MapSerializer.MapFileFormatVersion);

        var mapPath = Path.Combine("maps", "plage.fmap");
        Assert.Equal(Path.Combine("maps", "plage.anims.json"), TilesetAnimationJson.MapSidecarPath(mapPath));
        Assert.Equal(Path.Combine("tiles", "eau.anim.json"), TilesetAnimationJson.ImageSidecarPath(Path.Combine("tiles", "eau.png")));

        var map = new Map { Name = "Plage", Width = 2, Height = 2 };
        var layer = new Layer { LayerType = LayerType.Ground, Visible = true };
        layer.Tiles.Add(new Tile { X = 0, Y = 0, TilesetId = 1, SrcX = 0, SrcY = 0, Type = TileType.Ground });
        map.Layers.Add(layer);
        var bytes = new MapSerializer().Serialize(map);
        Assert.Equal((byte)5, bytes[4]);
        var loaded = new MapSerializer().Deserialize(bytes);
        Assert.Equal(0, loaded.Layers[0].Tiles[0].SrcX);
        Assert.Equal(map.Layers[0].Tiles[0].TilesetId, loaded.Layers[0].Tiles[0].TilesetId);
    }

    [Fact]
    public void SyncImageSidecars_WritesAnimatedAndDeletesStale()
    {
        var dir = Path.Combine(Path.GetTempPath(), "frog-anim-sync-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(dir);
        try
        {
            var stale = Path.Combine(dir, "2.anim.json");
            File.WriteAllText(stale, "{}");
            var doc = new TilesetAnimationDocument
            {
                Tilesets =
                {
                    new TilesetAnimationSet
                    {
                        TilesetId = 1,
                        FrameDurationMs = 200,
                        Strips =
                        {
                            new AnimatedTileStrip { OriginX = 0, OriginY = 0, FrameCount = 2 },
                        },
                    },
                },
            };

            TilesetAnimationJson.SyncImageSidecars(dir, doc, new[] { 1, 2 });
            Assert.True(File.Exists(Path.Combine(dir, "1.anim.json")));
            Assert.False(File.Exists(stale));

            TilesetAnimationJson.SyncImageSidecars(dir, new TilesetAnimationDocument(), new[] { 1, 2 });
            Assert.False(File.Exists(Path.Combine(dir, "1.anim.json")));
        }
        finally
        {
            Directory.Delete(dir, recursive: true);
        }
    }

    [Fact]
    public void EditorSurfaces_UseFrenchAnimCopy()
    {
        var root = RepoRoot();
        var window = File.ReadAllText(Path.Combine(root, "Frog.Editor", "MainWindow.xaml"));
        var picker = File.ReadAllText(Path.Combine(root, "Frog.Editor", "Panels", "TilesetPickerPanelWpf.xaml"));
        Assert.Contains("Animer la sélection de tuiles", window, StringComparison.Ordinal);
        Assert.Contains("Retirer l’animation de la sélection", window, StringComparison.Ordinal);
        Assert.Contains("Aperçu des tuiles animées", window, StringComparison.Ordinal);
        Assert.Contains("Animer la sélection", picker, StringComparison.Ordinal);
        Assert.Contains("Retirer l’animation", picker, StringComparison.Ordinal);
        Assert.Contains("style RPG Maker", picker, StringComparison.Ordinal);

        var status = File.ReadAllText(Path.Combine(root, "docs", "progress", "editor-tile-anim", "STATUS.md"));
        Assert.Contains("MapFileFormatVersion", status, StringComparison.Ordinal);
        Assert.Contains("reste 5", status, StringComparison.Ordinal);
    }

    private static string RepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null)
        {
            if (File.Exists(Path.Combine(dir.FullName, "Frog.Creator.sln")))
            {
                return dir.FullName;
            }

            dir = dir.Parent;
        }

        throw new InvalidOperationException("Frog.Creator.sln not found from " + AppContext.BaseDirectory);
    }
}
