namespace Frog.Server.Database;

/// <summary>
/// Stockage des cartes sérialisées (.fmap) côté PostgreSQL, avec révision et empreinte pour la synchro client.
/// </summary>
public interface IMapBlobStore
{
    /// <summary>Métadonnées légères pour comparer avec le cache local (éviter de retélécharger le blob).</summary>
    bool TryGetHead(int mapId, out long revision, out string contentSha256Hex);

    /// <summary>Blob complet + métadonnées.</summary>
    bool TryGet(int mapId, out byte[] fmapBytes, out long revision, out string contentSha256Hex);
}
