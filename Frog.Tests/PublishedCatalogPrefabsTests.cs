using System;
using System.IO;
using System.Text.Json;
using System.Threading.Tasks;
using Frog.Application.Prefabs;
using Frog.Core.Models;
using Frog.Core.Protocol;
using Xunit;

namespace Frog.Tests;

public sealed class PublishedCatalogPrefabsTests
{
    [Fact]
    public void PublishedCatalogWire_PrefabsAdditive_OldPayloadStillDeserializes()
    {
        const string withoutPrefabs =
            "{\"classes\":[],\"items\":[],\"spells\":[],\"shops\":[],\"npcs\":[],\"recipes\":[],\"tilesets\":[]}";
        var old = JsonSerializer.Deserialize<PublishedCatalogWire>(withoutPrefabs);
        Assert.NotNull(old);
        Assert.Empty(old!.Prefabs);
        Assert.Empty(old.PrefabMaps);
        Assert.Empty(old.Tilesets);

        var withPrefabs =
            "{\"classes\":[],\"items\":[],\"spells\":[],\"shops\":[],\"npcs\":[],\"recipes\":[],\"prefabs\":[{\"id\":\"sofa\",\"displayName\":\"Canapé\",\"variants\":[{\"facing\":\"south\",\"spriteFileName\":\"sofa-south.png\"}]}],\"prefabMaps\":[{\"mapName\":\"Maison\",\"placements\":[{\"prefabId\":\"sofa\",\"facing\":\"south\",\"tileX\":1,\"tileY\":2}]}]}";
        var parsed = JsonSerializer.Deserialize<PublishedCatalogWire>(withPrefabs);
        Assert.NotNull(parsed);
        var prefab = Assert.Single(parsed!.Prefabs);
        Assert.Equal("sofa", prefab.Id);
        var map = Assert.Single(parsed.PrefabMaps);
        Assert.Equal("Maison", map.MapName);
        var placement = Assert.Single(map.Placements);
        Assert.Equal("sofa", placement.PrefabId);
        Assert.Equal(1, placement.TileX);
    }

    [Fact]
    public async Task PublishedCatalogService_IncludesPrefabPng_WithoutFilesystem()
    {
        var maps = new Frog.Application.Maps.InMemoryMapRepository();
        var catalog = BuiltInPrefabCatalog.Create();
        var document = MapPrefabPersistDocument.Create(
            catalog,
            [new PrefabPlacement { PrefabId = BuiltInPrefabCatalog.SofaId, Facing = PrefabFacing.South, TileX = 1, TileY = 1 }],
            [new PrefabSpriteFile("sofa-south.png", SamplePng.Solid32Coral)]);
        var map = new Map { Name = "Salon", Width = 8, Height = 8 };
        map.Layers.Add(new Layer { LayerType = Frog.Core.Enums.LayerType.Ground, Visible = true });
        var saved = await maps.SaveAsync(new Frog.Application.Maps.SaveMapRequest
        {
            Map = map,
            ExpectedRevision = 0,
            Intent = Frog.Application.Maps.SaveMapIntent.Publish,
            Prefabs = document,
        });
        var success = Assert.IsType<Frog.Application.Maps.SaveMapResult.Success>(saved);

        var sidecarDir = Path.Combine(Path.GetTempPath(), $"frog-prefab-svc-{Guid.NewGuid():N}");
        Directory.CreateDirectory(sidecarDir);
        try
        {
            var sidecarPath = Path.Combine(sidecarDir, SidecarPublishedPrefabCatalog.FileName);
            SidecarPublishedPrefabCatalog.WriteFromDocument(sidecarPath, success.MapId, map.Name, document);
            var prefabCatalog = new SidecarPublishedPrefabCatalog(sidecarPath);
            var phase7 = new Frog.Server.Gameplay.Phase7PublishedContent();
            var phase8 = new Frog.Server.Gameplay.Phase8InMemoryPublishedContent();
            var service = new Frog.Server.Gameplay.PublishedCatalogService(
                phase7,
                phase7,
                phase7,
                phase7,
                phase7,
                phase8,
                prefabs: prefabCatalog);

            var wire = await service.BuildAsync();
            var entry = Assert.Single(wire.Prefabs);
            Assert.Equal(BuiltInPrefabCatalog.SofaId, entry.Id);
            var variant = Assert.Single(entry.Variants, v => v.Facing == "south");
            Assert.False(string.IsNullOrWhiteSpace(variant.PngBase64));
            Assert.Equal(SamplePng.Solid32Coral, Convert.FromBase64String(variant.PngBase64!));
            var mapEntry = Assert.Single(wire.PrefabMaps);
            Assert.Equal("Salon", mapEntry.MapName);
            Assert.Empty(PublishedPrefabClientCoverage.Missing(map, wire));
        }
        finally
        {
            Directory.Delete(sidecarDir, recursive: true);
        }
    }
}
