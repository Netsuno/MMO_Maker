using System;
using System.IO;
using System.Linq;
using System.Text;

using Frog.Core.Animation;
using Frog.Core.Constants;
using Frog.Core.Distribution;
using Frog.Core.Enums;
using Frog.Core.IO;
using Frog.Core.Maps;
using Frog.Core.Models;

using Xunit;

namespace Frog.Tests;

public sealed class TileAssetPhase1Tests
{
    private const string OpaqueRedHash = "0118296e6c16a0113a31e71a64cac301152e44d9623ca2db92bbbfb166dd22fa";
    private const string TransparentHash = "2d07a41ae992770085117e9815300bfd0730745883e60b24aaad5e69dfc087ae";

    [Fact]
    public void Hash_IsStable_PremultiplyCollapsesTransparentRgb_AndRejectsOtherSizes()
    {
        Assert.Equal(32, WorldMetrics.DefaultTileSizePixels);
        Assert.Equal(48, TileAssetMetrics.TargetTileSizePixels);
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        Assert.Equal((byte)5, MapSerializer.MapFileFormatVersion);
        Assert.Equal((byte)6, MapFormat.CurrentWriteVersion);
        Assert.Equal(128, TilePixelNormalizer.PremultiplyChannel(255, 128));

        var red = Solid(255, 0, 0, 255);
        var first = TileAssetId.FromStraightRgba(red);
        var second = TileAssetId.FromStraightRgba(red);
        Assert.Equal(first, second);
        Assert.Equal(OpaqueRedHash, first.ToHex());
        Assert.Equal(first, TileAssetId.Parse(OpaqueRedHash.ToUpperInvariant()));
        Assert.False(first.IsNone);

        var clearBlack = Solid(0, 0, 0, 0);
        var clearWhite = Solid(255, 255, 255, 0);
        Assert.Equal(TileAssetId.FromStraightRgba(clearBlack), TileAssetId.FromStraightRgba(clearWhite));
        Assert.Equal(TransparentHash, TileAssetId.FromStraightRgba(clearWhite).ToHex());

        var mixed = Solid(0, 0, 0, 255);
        mixed[0] = 255;
        mixed[3] = 128;
        var asset = TileAsset.FromStraightRgba(mixed);
        Assert.Equal(128, asset.NormalizedRgba[0]);
        Assert.Equal(0, asset.NormalizedRgba[1]);
        Assert.Equal(0, asset.NormalizedRgba[2]);
        Assert.Equal(128, asset.NormalizedRgba[3]);
        Assert.NotEqual(first, asset.Id);

        Assert.True(TileAssetId.FromHashBytes(new byte[TileAssetId.ByteLength]).IsNone);
        Assert.Throws<ArgumentException>(() => TileAssetId.FromStraightRgba(new byte[32 * 32 * 4]));
        Assert.False(TileSizeMigrationPolicy.CanHashAsTileAsset(32, 32));
        Assert.Contains("48", TileSizeMigrationPolicy.NoSilentUpscale, StringComparison.Ordinal);
    }

    [Fact]
    public void Slice_DedupesIdenticalCells_DropsRemainder_AndRejectsNon48()
    {
        var sheet = Sheet(100, 50, (x, y) => x < 48 ? ((byte)255, (byte)0, (byte)0, (byte)255) : ((byte)255, (byte)0, (byte)0, (byte)255));
        var sliced = TileSheetSlicer.Slice(sheet, 100, 50);
        Assert.Equal(2, sliced.Columns);
        Assert.Equal(1, sliced.Rows);
        Assert.Equal(4, sliced.DiscardedRightPixels);
        Assert.Equal(2, sliced.DiscardedBottomPixels);
        var only = Assert.Single(sliced.UniqueAssets);
        Assert.Equal(only.Id, sliced.CellIds[0]);
        Assert.Equal(only.Id, sliced.CellIds[1]);
        Assert.Equal(OpaqueRedHash, only.Id.ToHex());

        var distinct = Sheet(96, 48, (x, _) => x < 48
            ? ((byte)255, (byte)0, (byte)0, (byte)255)
            : ((byte)0, (byte)0, (byte)255, (byte)255));
        var two = TileSheetSlicer.Slice(distinct, 96, 48);
        Assert.Equal(2, two.UniqueAssets.Count);
        Assert.NotEqual(two.CellIds[0], two.CellIds[1]);
        Assert.Equal(two.UniqueAssets[0].Id, two.CellIds[0]);
        Assert.Equal(two.UniqueAssets[1].Id, two.CellIds[1]);

        Assert.Throws<ArgumentOutOfRangeException>(() => TileSheetSlicer.Slice(new byte[32 * 32 * 4], 32, 32, tileWidth: 32, tileHeight: 32));
        Assert.Throws<ArgumentException>(() => TileSheetSlicer.Slice(new byte[40 * 40 * 4], 40, 40));
    }

    [Fact]
    public void FrogPack_RoundTrip_SortsAndDedupes()
    {
        var keys = FrogPackKeys.Generate();
        var red = TileAsset.FromStraightRgba(Solid(255, 0, 0, 255));
        var blue = TileAsset.FromStraightRgba(Solid(0, 0, 255, 255));
        var file = FrogPackWriter.Write(new[] { blue, red, blue }, keys.PrivateSeed);
        Assert.Equal((byte)'F', file[0]);
        Assert.Equal((byte)'P', file[1]);
        Assert.Equal((byte)'K', file[2]);
        Assert.Equal((byte)'1', file[3]);

        var loaded = FrogPackReader.Read(file, keys.PublicKey);
        Assert.Equal(2, loaded.Count);
        Assert.True(loaded[0].Id.CompareTo(loaded[1].Id) < 0);
        var lookup = new MemoryTileAssetLookup(loaded);
        Assert.True(lookup.TryGet(red.Id, out var found));
        Assert.Equal(red.NormalizedRgba, found!.NormalizedRgba);
        Assert.True(lookup.TryGet(blue.Id, out _));
        Assert.False(lookup.TryGet(TileAssetId.None, out _));

        var empty = FrogPackWriter.Write(Array.Empty<TileAsset>(), keys.PrivateSeed);
        Assert.Empty(FrogPackReader.Read(empty, keys.PublicKey));
    }

    [Fact]
    public void FrogPack_RejectsBadSignature_TamperedBytes_WrongKey_AndEncryption()
    {
        var keys = FrogPackKeys.Generate();
        var other = FrogPackKeys.Generate();
        var red = TileAsset.FromStraightRgba(Solid(200, 10, 10, 255));
        var file = FrogPackWriter.Write(new[] { red }, keys.PrivateSeed);

        var badSignature = (byte[])file.Clone();
        badSignature[^1] ^= 0x01;
        var signatureError = Assert.Throws<FrogPackRejectedException>(() => FrogPackReader.Read(badSignature, keys.PublicKey));
        Assert.Contains("Ed25519", signatureError.Message, StringComparison.Ordinal);

        var tampered = (byte[])file.Clone();
        tampered[FrogPackFormat.HeaderLength + FrogPackFormat.ManifestEntryLength] ^= 0x01;
        var shaError = Assert.Throws<FrogPackRejectedException>(() => FrogPackReader.Read(tampered, keys.PublicKey));
        Assert.Contains("SHA-256", shaError.Message, StringComparison.Ordinal);

        var resigned = FrogPackWriter.Write(new[] { red }, other.PrivateSeed);
        var keyError = Assert.Throws<FrogPackRejectedException>(() => FrogPackReader.Read(resigned, keys.PublicKey));
        Assert.Contains("Ed25519", keyError.Message, StringComparison.Ordinal);

        var encrypted = (byte[])file.Clone();
        encrypted[6] = (byte)FrogPackFormat.FlagEncrypted;
        var cryptoError = Assert.Throws<FrogPackRejectedException>(() => FrogPackReader.Read(encrypted, keys.PublicKey));
        Assert.Contains("V1", cryptoError.Message, StringComparison.Ordinal);

        var garbage = (byte[])file.Clone();
        garbage[0] = (byte)'X';
        Assert.Throws<FrogPackRejectedException>(() => FrogPackReader.Read(garbage, keys.PublicKey));
    }

    [Fact]
    public void MapV5_StillLoads_AndDefaultSaveStaysV5()
    {
        var target = Guid.Parse("aaaaaaaa-bbbb-cccc-dddd-eeeeeeeeeeee");
        var bytes = SampleV5(target);
        Assert.Equal((byte)5, bytes[4]);

        var loaded = new MapSerializer().Deserialize(bytes);
        Assert.Equal(TileGraphicIdentity.SheetSource, loaded.GraphicIdentity);
        Assert.Equal(0, loaded.TileSizePixels);
        Assert.True(loaded.AllowPlayerOverlap);
        Assert.Equal("Plage", loaded.Name);
        var tile = Assert.Single(Assert.Single(loaded.Layers).Tiles);
        Assert.Equal(7, tile.TilesetId);
        Assert.Equal(16, tile.SrcX);
        Assert.Equal(32, tile.SrcY);
        Assert.True(tile.AssetId.IsNone);
        Assert.Equal(TileType.Warp, tile.Type);
        Assert.Equal(target, tile.WarpTargetMapId);
        Assert.Equal(4, tile.WarpTargetX);
        Assert.Equal(5, tile.WarpTargetY);
        Assert.Equal(bytes, new MapSerializer().Serialize(loaded));

        var legacy = SampleV3();
        var fromV3 = new MapSerializer().Deserialize(legacy);
        Assert.Equal("V3", fromV3.Name);
        Assert.Equal(3, fromV3.Layers[0].Tiles[0].TilesetId);
        Assert.False(fromV3.AllowPlayerOverlap);
        Assert.Equal((byte)5, new MapSerializer().Serialize(fromV3)[4]);
    }

    [Fact]
    public void MapV6_RoundTrip_StoresAssetIds_NotSheetCoordinates()
    {
        var red = TileAsset.FromStraightRgba(Solid(255, 0, 0, 255));
        var blue = TileAsset.FromStraightRgba(Solid(0, 0, 255, 255));
        var target = Guid.Parse("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb");
        var map = MapFormat.CreateTileAssetMap("N", 2, 1);
        map.AllowPlayerOverlap = true;
        var ground = new Layer { LayerType = LayerType.Ground, DisplayName = "sol", Visible = false, Locked = true };
        ground.Tiles.Add(new Tile
        {
            X = 0,
            Y = 0,
            Type = TileType.Warp,
            AssetId = red.Id,
            WarpTargetMapId = target,
            WarpTargetX = 3,
            WarpTargetY = 4,
        });
        ground.Tiles.Add(new Tile
        {
            X = 1,
            Y = 0,
            Type = TileType.Script,
            AssetId = blue.Id,
            ScriptId = "porte",
        });
        map.Layers.Add(ground);
        var attributes = new Layer { LayerType = LayerType.Attributes };
        attributes.Tiles.Add(new Tile { X = 0, Y = 0, Type = TileType.Block });
        map.Layers.Add(attributes);

        var bytes = MapFormat.Write(map);
        Assert.Equal((byte)6, bytes[4]);
        Assert.Equal(48, BitConverter.ToInt32(bytes, 19));

        var loaded = MapFormat.Read(bytes);
        Assert.Equal(TileGraphicIdentity.TileAsset, loaded.GraphicIdentity);
        Assert.Equal(48, loaded.TileSizePixels);
        Assert.True(loaded.AllowPlayerOverlap);
        Assert.Equal("sol", loaded.Layers[0].DisplayName);
        Assert.False(loaded.Layers[0].Visible);
        Assert.True(loaded.Layers[0].Locked);
        var warp = loaded.Layers[0].Tiles.Single(t => t.X == 0);
        Assert.Equal(red.Id, warp.AssetId);
        Assert.Equal(0, warp.TilesetId);
        Assert.Equal(0, warp.SrcX);
        Assert.Equal(0, warp.SrcY);
        Assert.Equal(target, warp.WarpTargetMapId);
        Assert.Equal(3, warp.WarpTargetX);
        var script = loaded.Layers[0].Tiles.Single(t => t.X == 1);
        Assert.Equal(blue.Id, script.AssetId);
        Assert.Equal("porte", script.ScriptId);
        var block = Assert.Single(loaded.Layers[1].Tiles);
        Assert.Equal(TileType.Block, block.Type);
        Assert.True(block.AssetId.IsNone);
        Assert.Equal(0, block.SrcX);
        Assert.Equal(bytes, new MapSerializer().Serialize(loaded));

        bytes[19] = 32;
        var rejected = Assert.Throws<InvalidDataException>(() => MapFormat.Read(bytes));
        Assert.Contains("tileSizePixels", rejected.Message, StringComparison.Ordinal);

        map.Layers[0].Tiles[0].TilesetId = 4;
        map.Layers[0].Tiles[0].SrcX = 8;
        Assert.Throws<InvalidDataException>(() => MapFormat.Write(map));
    }

    [Fact]
    public void SheetMap_RejectsAssetId_AndV6WriterRejectsSheetIdentity()
    {
        var red = TileAsset.FromStraightRgba(Solid(1, 2, 3, 255));
        var sheet = new Map { Name = "s", Width = 1, Height = 1 };
        sheet.Layers.Add(new Layer { LayerType = LayerType.Ground });
        sheet.Layers[0].Tiles.Add(new Tile
        {
            X = 0,
            Y = 0,
            TilesetId = 1,
            Type = TileType.Ground,
            AssetId = red.Id,
        });
        Assert.Throws<InvalidDataException>(() => new MapSerializer().Serialize(sheet));

        var bare = new Map { Name = "s", Width = 1, Height = 1 };
        bare.Layers.Add(new Layer { LayerType = LayerType.Ground });
        Assert.Throws<InvalidDataException>(() => MapFormat.Write(bare));
        Assert.Equal((byte)5, new MapSerializer().Serialize(bare)[4]);
    }

    [Fact]
    public void WorkingTileset_KeepsOrder_AndTileAnimation_UsesFrameClock()
    {
        var a = TileAssetId.FromStraightRgba(Solid(1, 0, 0, 255));
        var b = TileAssetId.FromStraightRgba(Solid(2, 0, 0, 255));
        var c = TileAssetId.FromStraightRgba(Solid(3, 0, 0, 255));
        var palette = new WorkingTileset { Name = "herbe" };
        palette.Tiles.Add(b);
        palette.Tiles.Add(a);
        palette.Tiles.Add(b);
        Assert.True(palette.Validate(out _));
        Assert.Equal(new[] { b, a, b }, palette.Tiles);
        palette.Tiles.Add(TileAssetId.None);
        Assert.False(palette.Validate(out var paletteError));
        Assert.Contains("TileAssetId", paletteError, StringComparison.Ordinal);

        var animation = new TileAnimation { Name = "eau", FrameDurationMs = 200 };
        animation.Frames.Add(a);
        animation.Frames.Add(b);
        animation.Frames.Add(c);
        Assert.True(animation.Validate(out _));
        Assert.Equal(a, animation.FrameAt(0));
        Assert.Equal(b, animation.FrameAt(200));
        Assert.Equal(c, animation.FrameAt(400));
        Assert.Equal(b, animation.FrameAt(600));
    }

    private static byte[] Solid(byte r, byte g, byte b, byte a)
    {
        var buffer = new byte[TileAssetMetrics.CanonicalPixelByteCount];
        for (var i = 0; i < buffer.Length; i += 4)
        {
            buffer[i] = r;
            buffer[i + 1] = g;
            buffer[i + 2] = b;
            buffer[i + 3] = a;
        }

        return buffer;
    }

    private static byte[] Sheet(int width, int height, Func<int, int, (byte R, byte G, byte B, byte A)> pixel)
    {
        var buffer = new byte[width * height * 4];
        for (var y = 0; y < height; y++)
        {
            for (var x = 0; x < width; x++)
            {
                var (r, g, b, a) = pixel(x, y);
                var i = ((y * width) + x) * 4;
                buffer[i] = r;
                buffer[i + 1] = g;
                buffer[i + 2] = b;
                buffer[i + 3] = a;
            }
        }

        return buffer;
    }

    private static byte[] SampleV5(Guid warpTarget)
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);
        bw.Write(Encoding.ASCII.GetBytes("FMAP"));
        bw.Write((byte)5);
        bw.Write(2);
        bw.Write(1);
        WriteUtf8(bw, "Plage");
        bw.Write((byte)1);
        bw.Write(1);
        bw.Write((byte)LayerType.Ground);
        bw.Write((byte)1);
        bw.Write((byte)0);
        WriteUtf8(bw, "sol");
        bw.Write(1);
        bw.Write(1);
        bw.Write(0);
        bw.Write(7);
        bw.Write(16);
        bw.Write(32);
        bw.Write((byte)TileType.Warp);
        bw.Write(warpTarget.ToByteArray());
        bw.Write(4);
        bw.Write(5);
        bw.Flush();
        return ms.ToArray();
    }

    private static byte[] SampleV3()
    {
        using var ms = new MemoryStream();
        using var bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);
        bw.Write(Encoding.ASCII.GetBytes("FMAP"));
        bw.Write((byte)3);
        bw.Write(1);
        bw.Write(1);
        WriteUtf8(bw, "V3");
        bw.Write(1);
        bw.Write((byte)LayerType.Ground);
        bw.Write((byte)1);
        bw.Write((byte)0);
        WriteUtf8(bw, string.Empty);
        bw.Write(1);
        bw.Write(0);
        bw.Write(0);
        bw.Write(3);
        bw.Write(0);
        bw.Write(0);
        bw.Write((byte)TileType.Ground);
        bw.Flush();
        return ms.ToArray();
    }

    private static void WriteUtf8(BinaryWriter bw, string value)
    {
        var bytes = Encoding.UTF8.GetBytes(value);
        bw.Write(bytes.Length);
        bw.Write(bytes);
    }
}
