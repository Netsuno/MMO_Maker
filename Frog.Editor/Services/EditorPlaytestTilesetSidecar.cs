using System.IO;
using System.Linq;
using Frog.Application.Assets;
using Frog.Application.Playtest;
using Frog.Application.Prefabs;
using Frog.Core.Models;
using Frog.Editor.Assets;

namespace Frog.Editor.Services;

/// <summary>Écrit tilesets + prefabs au layout client avant le lancement playtest.</summary>
internal sealed class EditorPlaytestTilesetSidecar : IPlaytestAssetSidecar
{
    private readonly Func<IReadOnlyList<PrefabPlacement>>? _prefabPlacements;

    public EditorPlaytestTilesetSidecar(Func<IReadOnlyList<PrefabPlacement>>? prefabPlacements = null)
    {
        _prefabPlacements = prefabPlacements;
    }

    public void Write(PlaytestLaunchPlan plan, string? clientExecutablePath)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var names = plan.Maps.Select(m => m.Name).ToArray();
        var files = TilesetCache.SnapshotPngFiles();
        if (files.Count > 0)
        {
            MapTilesetPackage.WriteSidecars(plan.WorkDirectory, names, files);
        }

        var catalog = PrefabSpriteCache.LoadCatalog();
        var placements = _prefabPlacements?.Invoke() ?? Array.Empty<PrefabPlacement>();
        var sprites = PrefabSpriteCache.SnapshotPngFiles(catalog);
        MapPrefabPackage.WriteSidecars(plan.WorkDirectory, names, catalog, placements, sprites);

        if (string.IsNullOrWhiteSpace(clientExecutablePath))
        {
            return;
        }

        var clientDir = Path.GetDirectoryName(clientExecutablePath);
        if (string.IsNullOrWhiteSpace(clientDir))
        {
            return;
        }

        if (files.Count > 0)
        {
            MapTilesetPackage.WriteSidecars(clientDir, names, files);
        }

        MapPrefabPackage.WriteSidecars(clientDir, names, catalog, placements, sprites);
    }
}
