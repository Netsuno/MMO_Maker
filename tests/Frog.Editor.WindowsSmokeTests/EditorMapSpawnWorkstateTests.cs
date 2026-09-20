using System;
using System.IO;
using Frog.Application.Maps;
using Frog.Editor.Config;
using Xunit;

namespace Frog.Editor.WindowsSmokeTests;

[Collection(UiSmokeCollectionDefinition.Name)]
public sealed class EditorMapSpawnWorkstateTests : IDisposable
{
    private readonly string _tempFile;

    public EditorMapSpawnWorkstateTests()
    {
        _tempFile = Path.Combine(Path.GetTempPath(), $"frog-spawn-workstate-{Guid.NewGuid():N}.json");
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
    public void WriteThenRead_RestoresSpawnForMapId()
    {
        var map = DemoMapFactory.CreateStarter();
        var id = Guid.Parse("11111111-2222-3333-4444-555555555555");
        EditorMapSpawnWorkstate.Write(id, map, 7, 4);
        Assert.True(EditorMapSpawnWorkstate.TryRead(id, map, out var x, out var y));
        Assert.Equal(7, x);
        Assert.Equal(4, y);
    }

    [Fact]
    public void Write_ClampsOutOfBounds()
    {
        var map = DemoMapFactory.CreateStarter();
        EditorMapSpawnWorkstate.Write(null, map, 99, -3);
        Assert.True(EditorMapSpawnWorkstate.TryRead(null, map, out var x, out var y));
        Assert.Equal(map.Width - 1, x);
        Assert.Equal(0, y);
    }

    [Fact]
    public void MissingKey_ReturnsFalse()
    {
        var map = DemoMapFactory.CreateStarter();
        Assert.False(EditorMapSpawnWorkstate.TryRead(Guid.NewGuid(), map, out _, out _));
    }

    [Fact]
    public void LocalDraftKey_IsIndependentFromCatalogId()
    {
        var map = DemoMapFactory.CreateStarter();
        EditorMapSpawnWorkstate.Write(null, map, 2, 3);
        Assert.True(EditorMapSpawnWorkstate.TryRead(null, map, out var x, out var y));
        Assert.Equal((2, 3), (x, y));
        Assert.False(EditorMapSpawnWorkstate.TryRead(Guid.NewGuid(), map, out _, out _));
    }
}
