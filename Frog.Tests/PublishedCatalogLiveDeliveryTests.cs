using System;
using System.Buffers.Binary;
using System.IO;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Xunit;
using Frog.Application.Assets;
using Frog.Application.Content;
using Frog.Application.Prefabs;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Models;
using Frog.Core.Protocol;
using Frog.Server.Gameplay;

namespace Frog.Tests;

/// <summary>
/// Chemin live : catalogue PostgreSQL (PNG embarqué + placements) → frame(s) → fichiers client.
/// </summary>
public sealed class PublishedCatalogLiveDeliveryTests
{
    [Fact]
    public void SmallCatalog_KeepsHistoricUInt16Length()
    {
        Assert.Equal((ushort)11, FrogWireProtocol.Version);
        const string json = "{\"classes\":[],\"tilesets\":[]}";
        var frames = PublishedCatalogPacket.EncodeFrames(json);
        var frame = Assert.Single(frames);
        Assert.Equal((byte)PacketId.PublishedCatalogResult, frame[0]);
        var len = BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(1));
        Assert.NotEqual(ushort.MaxValue, len);
        Assert.Equal(frame.Length, 1 + sizeof(ushort) + len);
        Assert.Equal(json, RoundTrip(json));
    }

    [Fact]
    public void OversizedCatalog_RoundTripsAcrossFramesWithinOneMegabyte()
    {
        var json = new string('p', PublishedCatalogPacket.MaxFramePayloadBytes);
        var frames = PublishedCatalogPacket.EncodeFrames(json);
        Assert.True(frames.Length >= 2);
        foreach (var frame in frames)
        {
            Assert.InRange(frame.Length, 1, PublishedCatalogPacket.MaxFramePayloadBytes);
            Assert.Equal(ushort.MaxValue, BinaryPrimitives.ReadUInt16LittleEndian(frame.AsSpan(1)));
        }

        Assert.Equal(json, RoundTrip(json));
    }

    [Fact]
    public async Task LiveCatalog_WithTilesetPngAndPrefabPlacements_MaterializesClientFiles()
    {
        var png = new byte[60_000];
        png[0] = 0x89;
        png[1] = 0x50;
        for (var i = 2; i < png.Length; i++)
        {
            png[i] = (byte)(i % 251);
        }

        var sha = TilesetDefinition.ComputeSha256Hex(png);
        var tilesets = new InMemoryTilesetRepository();
        await tilesets.SaveAsync(new SaveTilesetRequest
        {
            Definition = new TilesetDefinition
            {
                Id = Guid.NewGuid(),
                Name = "Herbe",
                LogicalPath = "tiles/herbe.png",
                TileSizePixels = 32,
                WidthPixels = 32,
                HeightPixels = 32,
                Sha256Hex = sha,
                EditorPaletteId = 1,
                PngBytes = png,
            },
            ExpectedRevision = 0,
            Intent = SaveContentIntent.Publish,
        });

        var mapId = Guid.Parse("aaaaaaaa-bbbb-4ccc-8ddd-eeeeeeeeeeee");
        const string mapName = "Carte démo";
        var spriteSha = TilesetDefinition.ComputeSha256Hex(SamplePng.Solid32Coral);
        var prefabs = new FixedPrefabCatalog(new PublishedPrefabCatalogBundle
        {
            Prefabs = PublishedPrefabClientCoverage.ToWirePrefabs(
                new PrefabCatalog
                {
                    Prefabs =
                    {
                        new PrefabDefinition
                        {
                            Id = "sofa",
                            DisplayName = "Canapé",
                            FootprintWidthTiles = 2,
                            FootprintHeightTiles = 1,
                            Variants =
                            {
                                new PrefabFacingVariant
                                {
                                    Facing = PrefabFacing.South,
                                    SpriteFileName = "sofa-south.png",
                                },
                            },
                        },
                    },
                },
                [new PrefabSpriteFile("sofa-south.png", SamplePng.Solid32Coral)]),
            PrefabMaps =
            [
                PublishedPrefabClientCoverage.ToWireMap(
                    mapId,
                    mapName,
                    [new PrefabPlacement { PrefabId = "sofa", Facing = PrefabFacing.South, TileX = 4, TileY = 5 }],
                    runtimeMapId: 1),
            ],
        });

        var phase7 = new Phase7PublishedContent();
        var phase8 = new Phase8InMemoryPublishedContent();
        var service = new PublishedCatalogService(
            phase7,
            phase7,
            phase7,
            phase7,
            phase7,
            phase8,
            tilesets,
            new CompositePublishedTilesetImageSource(EmbeddedPublishedTilesetImageSource.Instance),
            prefabs,
            NullPublishedWorldCatalog.Instance);

        var json = await service.BuildJsonAsync();
        Assert.True(Encoding.UTF8.GetByteCount(json) > ushort.MaxValue);

        var roundTripped = RoundTrip(json);
        var wire = JsonSerializer.Deserialize<PublishedCatalogWire>(roundTripped);
        Assert.NotNull(wire);
        var tileset = Assert.Single(wire!.Tilesets);
        Assert.Equal(1, tileset.PaletteId);
        Assert.False(string.IsNullOrWhiteSpace(tileset.PngBase64));
        Assert.Equal(png, Convert.FromBase64String(tileset.PngBase64!));

        var mapEntry = Assert.Single(wire.PrefabMaps);
        Assert.Equal(mapName, mapEntry.MapName);
        Assert.Equal(1, mapEntry.RuntimeMapId);
        Assert.True(PublishedPrefabClientCoverage.TryMatchPrefabMap(wire, mapName, out var byName, runtimeMapId: 1));
        Assert.Equal(4, Assert.Single(byName.Placements).TileX);
        Assert.True(PublishedPrefabClientCoverage.TryMatchPrefabMap(wire, "autre nom", out var byRuntime, runtimeMapId: 1));
        Assert.Equal(mapId.ToString("D"), byRuntime.MapId);

        var root = Path.Combine(Path.GetTempPath(), $"frog-live-cat-{Guid.NewGuid():N}");
        Directory.CreateDirectory(root);
        try
        {
            Assert.Equal(1, PublishedTilesetCatalogMaterializer.Materialize(wire, [root], [mapName]));
            Assert.True(PublishedPrefabCatalogMaterializer.Materialize(wire, root) > 0);

            Assert.Equal(png, File.ReadAllBytes(Path.Combine(root, "Tilesets", "1.png")));
            Assert.True(File.Exists(Path.Combine(root, "Tilesets", "manifest.json")));
            Assert.True(File.Exists(Path.Combine(root, "Maps", mapName + ".tilesets.json")));
            Assert.Equal(SamplePng.Solid32Coral, File.ReadAllBytes(Path.Combine(root, "Prefabs", "sofa-south.png")));
            Assert.True(File.Exists(Path.Combine(root, "Prefabs", "catalog.json")));
            Assert.True(File.Exists(Path.Combine(root, "Maps", mapName + ".prefabs.json")));
            Assert.True(File.Exists(Path.Combine(
                root,
                "Maps",
                MapPrefabPackage.PlacementSidecarFileName(mapName, mapId))));

            var map = new Map { Name = mapName, Width = 8, Height = 8 };
            var layer = new Layer { LayerType = LayerType.Ground, Visible = true };
            layer.Tiles.Add(new Tile { X = 0, Y = 0, TilesetId = 1, Type = TileType.Ground });
            map.Layers.Add(layer);
            Assert.Empty(PublishedTilesetClientCoverage.MissingTilesetIds(map, wire, root));
            Assert.Empty(PublishedPrefabClientCoverage.Missing(map, wire, root, mapId, runtimeMapId: 1));
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }

    private static string RoundTrip(string json)
    {
        var frames = PublishedCatalogPacket.EncodeFrames(json);
        var assembler = new PublishedCatalogPacket.Assembler();
        string? done = null;
        foreach (var frame in frames)
        {
            Assert.True(assembler.TryAccept(frame.AsSpan(1), out var part));
            if (part is null)
            {
                continue;
            }

            Assert.Null(done);
            done = part;
        }

        Assert.NotNull(done);
        return done!;
    }

    private sealed class FixedPrefabCatalog(PublishedPrefabCatalogBundle bundle) : IPublishedPrefabCatalog
    {
        public Task<PublishedPrefabCatalogBundle> LoadPublishedAsync(CancellationToken cancellationToken = default)
            => Task.FromResult(bundle);
    }
}
