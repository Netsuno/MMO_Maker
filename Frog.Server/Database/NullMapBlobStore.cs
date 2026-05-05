namespace Frog.Server.Database;

public sealed class NullMapBlobStore : IMapBlobStore
{
    public static readonly NullMapBlobStore Instance = new();

    private NullMapBlobStore()
    {
    }

    public bool TryGetHead(int mapId, out long revision, out string contentSha256Hex)
    {
        _ = mapId;
        revision = 0;
        contentSha256Hex = string.Empty;
        return false;
    }

    public bool TryGet(int mapId, out byte[] fmapBytes, out long revision, out string contentSha256Hex)
    {
        _ = mapId;
        fmapBytes = Array.Empty<byte>();
        revision = 0;
        contentSha256Hex = string.Empty;
        return false;
    }
}
