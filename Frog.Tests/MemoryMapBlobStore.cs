using System;
using System.Collections.Generic;
using System.Security.Cryptography;
using Frog.Server.Database;

namespace Frog.Tests;

/// <summary>Faux <see cref="IMapBlobStore"/> pour tests multi-cartes (blobs pré-seed).</summary>
public sealed class MemoryMapBlobStore : IMapBlobStore
{
    private sealed record Entry(byte[] Bytes, long Revision, string Sha256HexLower);

    private readonly Dictionary<int, Entry> _entries = new();

    /// <summary>Enregistre un blob carte ; revision + SHA suivent strictement cet octet.</summary>
    public void Seed(int mapId, byte[] fmapBytes, long revision)
    {
        ArgumentNullException.ThrowIfNull(fmapBytes);
        var sha = SHA256.HashData(fmapBytes);
        var hex = Convert.ToHexString(sha).ToLowerInvariant();
        _entries[mapId] = new Entry((byte[])fmapBytes.Clone(), revision, hex);
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
