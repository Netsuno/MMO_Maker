using System.Security.Cryptography;
using Frog.Application.Playtest;
using Frog.Server.Database;

namespace Frog.Server.Playtest;

/// <summary>
/// Store de blobs pour playtest : cartes publiées préchargées depuis le manifeste (pas PostgreSQL).
/// </summary>
public sealed class PlaytestMapBlobStore : IMapBlobStore
{
    private sealed record Entry(byte[] Bytes, long Revision, string Sha256HexLower);

    private readonly Dictionary<int, Entry> _entries;

    public PlaytestMapBlobStore(IReadOnlyDictionary<int, (byte[] Bytes, long Revision, string Name)> blobs)
    {
        ArgumentNullException.ThrowIfNull(blobs);
        _entries = new Dictionary<int, Entry>(blobs.Count);
        foreach (var (mapId, value) in blobs)
        {
            var sha = SHA256.HashData(value.Bytes);
            _entries[mapId] = new Entry(
                (byte[])value.Bytes.Clone(),
                value.Revision,
                Convert.ToHexString(sha).ToLowerInvariant());
        }
    }

    public static PlaytestMapBlobStore FromManifest(string manifestPath)
    {
        var doc = PlaytestManifestWriter.Read(manifestPath);
        var dir = Path.GetDirectoryName(Path.GetFullPath(manifestPath))
                  ?? throw new InvalidOperationException("Répertoire manifeste playtest introuvable.");
        var blobs = PlaytestManifestWriter.LoadBlobs(doc, dir);
        return new PlaytestMapBlobStore(blobs);
    }

    public static PlaytestMapBlobStore FromLaunchPlan(PlaytestLaunchPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        var dict = plan.Maps.ToDictionary(
            m => m.RuntimeMapId,
            m => (m.SerializedFmap, m.PublishedRevision, m.Name));
        return new PlaytestMapBlobStore(dict);
    }

    public bool TryGetHead(int mapId, out long revision, out string contentSha256Hex)
    {
        if (!_entries.TryGetValue(mapId, out var entry))
        {
            revision = 0;
            contentSha256Hex = string.Empty;
            return false;
        }

        revision = entry.Revision;
        contentSha256Hex = entry.Sha256HexLower;
        return true;
    }

    public bool TryGet(int mapId, out byte[] fmapBytes, out long revision, out string contentSha256Hex)
    {
        if (!_entries.TryGetValue(mapId, out var entry))
        {
            fmapBytes = Array.Empty<byte>();
            revision = 0;
            contentSha256Hex = string.Empty;
            return false;
        }

        fmapBytes = (byte[])entry.Bytes.Clone();
        revision = entry.Revision;
        contentSha256Hex = entry.Sha256HexLower;
        return true;
    }
}
