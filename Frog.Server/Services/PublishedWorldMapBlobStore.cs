using System.Security.Cryptography;
using Frog.Application.Content;
using Frog.Server.Database;

namespace Frog.Server.Services;

/// <summary><see cref="IMapBlobStore"/> backed by published PostgreSQL maps (runtime int ids).</summary>
public sealed class PublishedWorldMapBlobStore : IMapBlobStore
{
    private readonly object _gate = new();
    private Dictionary<int, (byte[] Bytes, long Revision, string ShaHex)> _byRuntime = new();

    public void ReplaceAll(IReadOnlyList<PublishedMapRuntimeEntry> maps)
    {
        ArgumentNullException.ThrowIfNull(maps);
        var next = new Dictionary<int, (byte[] Bytes, long Revision, string ShaHex)>(maps.Count);
        foreach (var m in maps)
        {
            next[m.RuntimeMapId] = (m.SerializedFmap, m.PublishedRevision, m.ContentSha256Hex);
        }

        lock (_gate)
        {
            _byRuntime = next;
        }
    }

    public bool TryGetHead(int mapId, out long revision, out string contentSha256Hex)
    {
        lock (_gate)
        {
            if (!_byRuntime.TryGetValue(mapId, out var entry))
            {
                revision = 0;
                contentSha256Hex = string.Empty;
                return false;
            }

            revision = entry.Revision;
            contentSha256Hex = entry.ShaHex;
            return true;
        }
    }

    public bool TryGet(int mapId, out byte[] fmapBytes, out long revision, out string contentSha256Hex)
    {
        lock (_gate)
        {
            if (!_byRuntime.TryGetValue(mapId, out var entry))
            {
                fmapBytes = Array.Empty<byte>();
                revision = 0;
                contentSha256Hex = string.Empty;
                return false;
            }

            fmapBytes = (byte[])entry.Bytes.Clone();
            revision = entry.Revision;
            contentSha256Hex = entry.ShaHex;
            return true;
        }
    }
}
