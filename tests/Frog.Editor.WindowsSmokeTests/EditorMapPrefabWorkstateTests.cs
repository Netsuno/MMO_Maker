using System;
using System.IO;
using Frog.Application.Maps;
using Frog.Application.Prefabs;
using Frog.Core.Models;
using Frog.Editor.Config;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

[Collection(UiSmokeCollectionDefinition.Name)]
public sealed class EditorMapPrefabWorkstateTests : IDisposable
{
    private readonly string _tempFile;

    public EditorMapPrefabWorkstateTests()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), $"frog-prefab-workstate-{Guid.NewGuid():N}.json");
        EditorLocalWorkstate.OverrideFilePathForTest = _tempFile;
    }

    public void Dispose()
    {
        EditorLocalWorkstate.OverrideFilePathForTest = null;
        try
        {
            if (File.Exists(_tempFile))
            {
                File.Delete(_tempFile);
            }
        }
        catch
        {
            // best-effort
        }
    }

    [Fact]
    public void WriteThenRead_RestoresPlacementsForMapId()
    {
        var map = DemoMapFactory.CreateStarter();
        var id = Guid.Parse("11111111-2222-3333-4444-555555555555");
        EditorMapPrefabWorkstate.Write(
            id,
            map,
            [new PrefabPlacement { PrefabId = BuiltInPrefabCatalog.SofaId, Facing = PrefabFacing.North, TileX = 2, TileY = 3 }]);

        Assert.True(EditorMapPrefabWorkstate.TryRead(id, map, out var placements));
        var item = Assert.Single(placements);
        Assert.Equal(BuiltInPrefabCatalog.SofaId, item.PrefabId);
        Assert.Equal(PrefabFacing.North, item.Facing);
        Assert.Equal(2, item.TileX);
        Assert.Equal(3, item.TileY);
    }

    [Fact]
    public void LastSelection_Roundtrips()
    {
        EditorLocalWorkstate.WriteLastPrefabSelection(BuiltInPrefabCatalog.FenceRailId, PrefabFacing.East);
        EditorLocalWorkstate.TryReadLastPrefabSelection(out var id, out var facing);
        Assert.Equal(BuiltInPrefabCatalog.FenceRailId, id);
        Assert.Equal(PrefabFacing.East, facing);
    }
}
