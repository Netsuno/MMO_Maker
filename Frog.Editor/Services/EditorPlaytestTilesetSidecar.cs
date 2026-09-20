using System.IO;
using System.Linq;
using Frog.Application.Assets;
using Frog.Application.Playtest;
using Frog.Editor.Assets;

namespace Frog.Editor.Services;

/// <summary>Écrit les PNG du <see cref="TilesetCache"/> au layout client avant le lancement playtest.</summary>
internal sealed class EditorPlaytestTilesetSidecar : IPlaytestAssetSidecar
{
    public void Write(PlaytestLaunchPlan plan, string? clientExecutablePath)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var files = TilesetCache.SnapshotPngFiles();
        if (files.Count == 0)
        {
            return;
        }

        var names = plan.Maps.Select(m => m.Name).ToArray();
        MapTilesetPackage.WriteSidecars(plan.WorkDirectory, names, files);

        if (string.IsNullOrWhiteSpace(clientExecutablePath))
        {
            return;
        }

        var clientDir = Path.GetDirectoryName(clientExecutablePath);
        if (string.IsNullOrWhiteSpace(clientDir))
        {
            return;
        }

        MapTilesetPackage.WriteSidecars(clientDir, names, files);
    }
}
