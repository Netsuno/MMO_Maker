using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Frog.Application.Maps;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.IO;
using Frog.Core.Maps;
using Frog.Core.Models;
using Xunit;

namespace Frog.Tests;

public sealed class MapAudioSelectionTests
{
    [Fact]
    public void EmptyAudio_OmitsTrailer_AndOldBytesStayByteIdentical()
    {
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Equal(48, TileAssetMetrics.TargetTileSizePixels);

        var sheet = SheetMap("Bois");
        var serializer = new MapSerializer();
        var sheetBytes = serializer.Serialize(sheet);
        Assert.Equal((byte)5, sheetBytes[4]);
        Assert.Equal(sheetBytes, serializer.Serialize(serializer.Deserialize(sheetBytes)));
        Assert.True(serializer.Deserialize(sheetBytes).Bgm.IsNone);
        Assert.True(serializer.Deserialize(sheetBytes).Se.IsNone);

        var asset = TileAssetMap("Clairière");
        var v6 = MapFormat.Write(asset);
        Assert.Equal((byte)6, v6[4]);
        Assert.Equal(48, asset.TileSizePixels);
        Assert.Equal(v6, serializer.Serialize(MapFormat.Read(v6)));

        var withTail = new byte[v6.Length + 4];
        v6.CopyTo(withTail, 0);
        Encoding.ASCII.GetBytes("XXXX").CopyTo(withTail, v6.Length);
        var ignored = MapFormat.Read(withTail);
        Assert.True(ignored.Bgm.IsNone);
        Assert.Equal(TileGraphicIdentity.TileAsset, ignored.GraphicIdentity);
        Assert.Equal(48, ignored.TileSizePixels);
    }

    [Fact]
    public void V6_RoundTripsBgmAndSe_WithoutBumpingFormat()
    {
        var map = TileAssetMap("Port");
        map.AllowPlayerOverlap = true;
        map.Bgm = new MapAudioTrack
        {
            Asset = @"Assets\Audio\music-loop.wav",
            Volume = 80,
            FadeMs = 500,
        };
        map.Se = new MapAudioTrack
        {
            Asset = "MusicLoop",
            Volume = 35,
            FadeMs = 0,
        };

        var bytes = MapFormat.Write(map);
        Assert.Equal((byte)6, bytes[4]);
        Assert.Contains("FMAU", Encoding.ASCII.GetString(bytes), StringComparison.Ordinal);

        var loaded = MapFormat.Read(bytes);
        Assert.Equal("Assets/Audio/music-loop.wav", loaded.Bgm.Asset);
        Assert.Equal(80, loaded.Bgm.Volume);
        Assert.Equal(500, loaded.Bgm.FadeMs);
        Assert.Equal("MusicLoop", loaded.Se.Asset);
        Assert.Equal(35, loaded.Se.Volume);
        Assert.Equal(0, loaded.Se.FadeMs);
        Assert.Equal(TileGraphicIdentity.TileAsset, loaded.GraphicIdentity);
        Assert.Equal(48, loaded.TileSizePixels);
        Assert.True(loaded.AllowPlayerOverlap);
        Assert.Equal(bytes, new MapSerializer().Serialize(loaded));

        loaded.Bgm = new MapAudioTrack();
        loaded.Se = new MapAudioTrack();
        var cleared = new MapSerializer().Serialize(loaded);
        Assert.Equal((byte)6, cleared[4]);
        Assert.DoesNotContain("FMAU", Encoding.ASCII.GetString(cleared), StringComparison.Ordinal);
        var again = MapFormat.Read(cleared);
        Assert.True(again.Bgm.IsNone);
        Assert.Equal(100, again.Bgm.Volume);
        Assert.Equal(0, again.Bgm.FadeMs);
    }

    [Fact]
    public void CorruptAudioSection_IsRejected()
    {
        var map = TileAssetMap("Port");
        map.Bgm = new MapAudioTrack { Asset = "cue", Volume = 50, FadeMs = 10 };
        var bytes = MapFormat.Write(map);
        var marker = "FMAU"u8;
        var index = bytes.AsSpan().IndexOf(marker);
        Assert.True(index >= 0);
        bytes[index + 4] = 2;
        var badVersion = Assert.Throws<InvalidDataException>(() => MapFormat.Read(bytes));
        Assert.Contains("Section audio", badVersion.Message, StringComparison.Ordinal);

        map.Bgm.Volume = 140;
        Assert.False(map.Validate(out var error));
        Assert.Contains("volume", error, StringComparison.OrdinalIgnoreCase);
        Assert.Throws<InvalidDataException>(() => new MapSerializer().Serialize(map));

        map.Bgm.Volume = 50;
        map.Se.Asset = "../dehors.wav";
        Assert.False(map.Validate(out error));
        Assert.Contains("traversée", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TryApplyProperties_AudioOnly_DirtiesWithoutResize()
    {
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        var map = TileAssetMap("Port");
        MapEditOperations.PaintTile(map, 0, 1, 1, new Tile { Type = TileType.Ground, AssetId = TileAssetId.FromStraightRgba(Solid(1, 2, 3, 255)) });
        var called = false;
        Assert.False(MapEditOperations.TryApplyProperties(
            map,
            new MapPropertiesEdit
            {
                Name = map.Name,
                Width = map.Width,
                Height = map.Height,
                Bgm = new MapAudioTrack(),
                Se = new MapAudioTrack(),
            },
            out var none,
            () => called = true));
        Assert.Null(none);
        Assert.False(called);

        Assert.False(MapEditOperations.TryApplyProperties(
            map,
            new MapPropertiesEdit
            {
                Name = map.Name,
                Width = map.Width,
                Height = map.Height,
                Bgm = new MapAudioTrack { Asset = "../x.wav", Volume = 10 },
                Se = new MapAudioTrack(),
            },
            out var invalid,
            () => called = true));
        Assert.Contains("traversée", invalid, StringComparison.OrdinalIgnoreCase);
        Assert.False(called);
        Assert.True(map.Bgm.IsNone);
        Assert.Equal(4, map.Width);

        Assert.True(MapEditOperations.TryApplyProperties(
            map,
            new MapPropertiesEdit
            {
                Name = map.Name,
                Width = map.Width,
                Height = map.Height,
                Bgm = new MapAudioTrack { Asset = "Assets/Audio/music-loop.wav", Volume = 90, FadeMs = 20 },
                Se = new MapAudioTrack { Asset = "vent.wav", Volume = 15 },
            },
            out var ok,
            () => called = true));
        Assert.Null(ok);
        Assert.True(called);
        Assert.Equal(4, map.Width);
        Assert.Equal(48, map.TileSizePixels);
        Assert.NotNull(map.Layers[0].Tiles.SingleOrDefault(t => t.X == 1 && t.Y == 1));
        Assert.Equal((byte)6, MapFormat.Write(map)[4]);

        var summary = MapEditOperations.FormatPropertiesSummary(map, null, null);
        Assert.Contains("Musique : music-loop.wav (90 %, fondu 20 ms)", summary, StringComparison.Ordinal);
        Assert.Contains("Ambiance : vent.wav (15 %)", summary, StringComparison.Ordinal);

        Assert.True(MapResizeShift.TryApply(
            map,
            new MapResizeShiftEdit { Width = 3, Height = 3, DeltaX = 0, DeltaY = 0 },
            entities: null,
            prefabs: null,
            events: null,
            spawn: null,
            out _,
            out var resizeError));
        Assert.Null(resizeError);
        Assert.Equal("Assets/Audio/music-loop.wav", map.Bgm.Asset);
        Assert.Equal("vent.wav", map.Se.Asset);
        Assert.Equal(48, map.TileSizePixels);

        var prior = new MapSerializer().Serialize(map);
        map.Bgm = new MapAudioTrack { Asset = "autre.wav" };
        MapResizeShift.ReplaceContents(map, new MapSerializer().Deserialize(prior));
        Assert.Equal("Assets/Audio/music-loop.wav", map.Bgm.Asset);
        Assert.Equal(90, map.Bgm.Volume);
    }

    [Fact]
    public async Task InMemorySave_KeepsAudioSelection()
    {
        var map = SheetMap("Salon");
        map.Bgm = new MapAudioTrack { Asset = "Assets/Audio/music-loop.wav", Volume = 60, FadeMs = 100 };
        map.Se = new MapAudioTrack { Asset = "pluie.wav", Volume = 20 };
        var repo = new InMemoryMapRepository(MapRepositoryCapabilities.InMemoryTest);
        var saved = await repo.SaveAsync(new SaveMapRequest { Map = map, ExpectedRevision = 0 });
        var success = Assert.IsType<SaveMapResult.Success>(saved);
        var loaded = await repo.LoadByIdAsync(success.MapId);
        Assert.NotNull(loaded);
        Assert.Equal("Assets/Audio/music-loop.wav", loaded.Map.Bgm.Asset);
        Assert.Equal(60, loaded.Map.Bgm.Volume);
        Assert.Equal(100, loaded.Map.Bgm.FadeMs);
        Assert.Equal("pluie.wav", loaded.Map.Se.Asset);
        Assert.Equal(20, loaded.Map.Se.Volume);
    }

    [Fact]
    public void FromPickedFile_PrefersProjectAudio_OtherwiseFileName()
    {
        var root = Path.Combine(Path.GetTempPath(), "frog-audio-" + Guid.NewGuid().ToString("N"));
        var audio = Path.Combine(root, "Frog.Client", "Assets", "Audio");
        Directory.CreateDirectory(audio);
        var loop = Path.Combine(audio, "music-loop.wav");
        File.WriteAllBytes(loop, [1, 2, 3]);
        Assert.True(MapAudioTrack.TryFromPickedFile(loop, root, out var stored, out var error), error);
        Assert.Equal("Assets/Audio/music-loop.wav", stored);

        var outside = Path.Combine(Path.GetTempPath(), "frog-wind-" + Guid.NewGuid().ToString("N") + ".wav");
        File.WriteAllBytes(outside, [1]);
        Assert.True(MapAudioTrack.TryFromPickedFile(outside, root, out var name, out error), error);
        Assert.Equal(Path.GetFileName(outside), name);
        Assert.False(Path.IsPathRooted(name));

        Assert.True(MapAudioTrack.TryCreate("Village theme", 100, 0, out var cue, out error), error);
        Assert.Equal("Village theme", cue.Asset);
    }

    private static Map SheetMap(string name)
    {
        var map = new Map { Name = name, Width = 4, Height = 4 };
        map.Layers.Add(new Layer { LayerType = LayerType.Ground });
        return map;
    }

    private static Map TileAssetMap(string name)
    {
        var map = MapFormat.CreateTileAssetMap(name, 4, 4);
        map.Layers.Add(new Layer { LayerType = LayerType.Ground });
        return map;
    }

    private static byte[] Solid(byte r, byte g, byte b, byte a)
    {
        var bytes = new byte[TileAssetMetrics.CanonicalPixelByteCount];
        for (var i = 0; i < bytes.Length; i += 4)
        {
            bytes[i] = r;
            bytes[i + 1] = g;
            bytes[i + 2] = b;
            bytes[i + 3] = a;
        }

        return bytes;
    }
}
