using Frog.Core.Maps;

namespace Frog.Core.Distribution;

/// <summary>
/// Résolution <see cref="TileAssetId"/> → pixels. Point d’accroche du cache client et du dépôt serveur
/// (ces phases ne sont pas implémentées ici).
/// </summary>
public interface ITileAssetLookup
{
    bool TryGet(TileAssetId id, out TileAsset? asset);
}

/// <summary>Index en mémoire, par exemple le résultat d’un <c>FrogPackReader.Read</c>.</summary>
public sealed class MemoryTileAssetLookup : ITileAssetLookup
{
    private readonly Dictionary<TileAssetId, TileAsset> _assets;

    public MemoryTileAssetLookup(IEnumerable<TileAsset> assets)
    {
        ArgumentNullException.ThrowIfNull(assets);
        _assets = new Dictionary<TileAssetId, TileAsset>();
        foreach (var asset in assets)
        {
            if (asset is null || asset.Id.IsNone)
            {
                throw new ArgumentException("Tuile absente ou sans identifiant.", nameof(assets));
            }

            _assets[asset.Id] = asset;
        }
    }

    public bool TryGet(TileAssetId id, out TileAsset? asset)
    {
        if (_assets.TryGetValue(id, out var found))
        {
            asset = found;
            return true;
        }

        asset = null;
        return false;
    }
}
