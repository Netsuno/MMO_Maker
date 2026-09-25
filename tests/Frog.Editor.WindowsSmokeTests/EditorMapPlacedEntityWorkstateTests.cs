using System.IO;
using Frog.Application.Maps;
using Frog.Editor.Config;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

[Collection(UiSmokeCollectionDefinition.Name)]
public sealed class EditorMapPlacedEntityWorkstateTests : IDisposable
{
    private readonly string _tempFile;

    public EditorMapPlacedEntityWorkstateTests()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), $"frog-placed-entities-{Guid.NewGuid():N}.json");
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
    public void WriteThenRead_RestoresEntities_AndDropsInvalidTiles()
    {
        var map = DemoMapFactory.CreateStarter();
        var id = Guid.Parse("11111111-2222-3333-4444-555555555555");
        var entities = new List<MapPlacedEntity>();
        Assert.True(MapPlacedEntityEdit.TryPlace(entities, map, MapPlacedKind.Npc, 2, 3, out var npc, out _));
        Assert.True(MapPlacedEntityEdit.TryPlace(entities, map, MapPlacedKind.Object, 4, 1, out _, out _));
        npc.Notes = "mémo locale";

        EditorMapPlacedEntityWorkstate.Write(id, map, entities);
        Assert.True(EditorMapPlacedEntityWorkstate.TryRead(id, map, out var restored));
        Assert.Equal(2, restored.Count);
        Assert.Contains(restored, entity => entity.Id == npc.Id && entity.Notes == "mémo locale");
        Assert.False(EditorMapPlacedEntityWorkstate.TryRead(Guid.NewGuid(), map, out _));

        var outside = MapPlacedEntityEdit.Clone(entities);
        outside[0].TileX = map.Width + 4;
        EditorMapPlacedEntityWorkstate.Write(null, map, outside);
        Assert.True(EditorMapPlacedEntityWorkstate.TryRead(null, map, out var local));
        Assert.Single(local);
        Assert.DoesNotContain(local, entity => entity.TileX >= map.Width);
    }
}
