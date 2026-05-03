#nullable enable
using System.IO;
using System.Text.Json;
using Frog.Core.Models;

namespace Frog.Core.IO;

/// <summary>Sérialisation JSON du manifeste tilesets (UTF‑8).</summary>
public static class TilesetManifestJson
{
    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static byte[] Serialize(TilesetManifest manifest)
        => JsonSerializer.SerializeToUtf8Bytes(manifest, Options);

    public static TilesetManifest? TryDeserialize(ReadOnlySpan<byte> utf8)
    {
        try
        {
            return JsonSerializer.Deserialize<TilesetManifest>(utf8, Options);
        }
        catch
        {
            return null;
        }
    }

    public static TilesetManifest? TryDeserializeFromFile(string path)
    {
        if (!File.Exists(path))
        {
            return null;
        }

        try
        {
            return TryDeserialize(File.ReadAllBytes(path));
        }
        catch
        {
            return null;
        }
    }
}
