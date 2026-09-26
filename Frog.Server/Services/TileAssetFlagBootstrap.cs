using Frog.Core.Maps;
using Frog.Server.Config;

namespace Frog.Server.Services;

/// <summary>
/// Charge <c>tile-flags.json</c> posé à côté du manifeste playtest.
/// Table vide si le fichier n’est pas là : les cartes feuille gardent la collision <c>TileType.Block</c>.
/// </summary>
public static class TileAssetFlagBootstrap
{
    public static TileAssetFlagTable Load(PlaytestRuntimeOptions playtest)
    {
        ArgumentNullException.ThrowIfNull(playtest);
        if (playtest.Enabled && !string.IsNullOrWhiteSpace(playtest.ManifestPath))
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(playtest.ManifestPath));
            var table = TileAssetFlagTable.TryLoad(dir);
            if (table is not null)
            {
                return table;
            }
        }

        return new TileAssetFlagTable();
    }
}
