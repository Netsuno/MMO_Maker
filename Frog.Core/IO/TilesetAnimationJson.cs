#nullable enable
using System.Text.Json;
using Frog.Core.Animation;
using Frog.Core.Models;

namespace Frog.Core.IO;

/// <summary>JSON UTF-8 des bandes animées (sidecar, pas le blob <c>.fmap</c>).</summary>
public static class TilesetAnimationJson
{
    public const string MapSidecarSuffix = ".anims.json";
    public const string ImageSidecarSuffix = ".anim.json";

    private static readonly JsonSerializerOptions Options = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = true,
    };

    public static byte[] SerializeDocument(TilesetAnimationDocument document)
        => JsonSerializer.SerializeToUtf8Bytes(document, Options);

    public static byte[] SerializeSet(TilesetAnimationSet set)
        => JsonSerializer.SerializeToUtf8Bytes(set, Options);

    public static TilesetAnimationDocument? TryDeserializeDocument(ReadOnlySpan<byte> utf8)
    {
        try
        {
            var doc = JsonSerializer.Deserialize<TilesetAnimationDocument>(utf8, Options);
            if (doc is null)
            {
                return null;
            }

            if (doc.DocumentVersion is 0)
            {
                doc.DocumentVersion = TilesetAnimationDocument.CurrentVersion;
            }

            if (doc.DocumentVersion != TilesetAnimationDocument.CurrentVersion)
            {
                return null;
            }

            doc.Tilesets ??= new List<TilesetAnimationSet>();
            var normalized = new List<TilesetAnimationSet>();
            foreach (var set in doc.Tilesets)
            {
                var copy = NormalizePublic(set);
                if (copy.Strips.Count > 0)
                {
                    normalized.Add(copy);
                }
            }

            doc.Tilesets = normalized;
            return doc;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static TilesetAnimationSet? TryDeserializeSet(ReadOnlySpan<byte> utf8)
    {
        try
        {
            var set = JsonSerializer.Deserialize<TilesetAnimationSet>(utf8, Options);
            if (set is null)
            {
                return null;
            }

            var copy = NormalizePublic(set);
            return copy.Strips.Count > 0 ? copy : null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static TilesetAnimationDocument? TryReadDocument(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        try
        {
            return TryDeserializeDocument(File.ReadAllBytes(path));
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Lit une bande (<c>.anim.json</c>) ou, à défaut, la première entrée d’un document.</summary>
    public static TilesetAnimationSet? TryReadSet(string path)
    {
        if (string.IsNullOrWhiteSpace(path) || !File.Exists(path))
        {
            return null;
        }

        byte[] bytes;
        try
        {
            bytes = File.ReadAllBytes(path);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }

        var set = TryDeserializeSet(bytes);
        if (set is not null)
        {
            return set;
        }

        var doc = TryDeserializeDocument(bytes);
        return doc?.Tilesets.FirstOrDefault(entry => entry.Strips.Count > 0);
    }

    public static string MapSidecarPath(string mapFilePath)
    {
        var dir = Path.GetDirectoryName(mapFilePath);
        var stem = Path.GetFileNameWithoutExtension(mapFilePath);
        if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(stem))
        {
            return string.Empty;
        }

        return Path.Combine(dir, stem + MapSidecarSuffix);
    }

    /// <summary>
    /// Écrit <c>{id}.anim.json</c> pour les tilesets animés et retire le sidecar des autres ids exportés.
    /// </summary>
    public static void SyncImageSidecars(string directory, TilesetAnimationDocument document, IEnumerable<int> tilesetIds)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(tilesetIds);

        var animated = new Dictionary<int, TilesetAnimationSet>();
        foreach (var set in document.Tilesets ?? new List<TilesetAnimationSet>())
        {
            if (set.TilesetId > 0 && set.Strips is { Count: > 0 })
            {
                animated[set.TilesetId] = set;
            }
        }

        foreach (var id in tilesetIds.Where(id => id > 0).Distinct())
        {
            var sidecar = ImageSidecarPath(Path.Combine(directory, id + ".png"));
            if (string.IsNullOrEmpty(sidecar))
            {
                continue;
            }

            if (animated.TryGetValue(id, out var set))
            {
                File.WriteAllBytes(sidecar, SerializeSet(set));
            }
            else if (File.Exists(sidecar))
            {
                File.Delete(sidecar);
            }
        }
    }

    public static string ImageSidecarPath(string imagePath)
    {
        var dir = Path.GetDirectoryName(imagePath);
        var stem = Path.GetFileNameWithoutExtension(imagePath);
        if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(stem))
        {
            return string.Empty;
        }

        return Path.Combine(dir, stem + ImageSidecarSuffix);
    }

    private static TilesetAnimationSet NormalizePublic(TilesetAnimationSet set)
    {
        set.Strips ??= new List<AnimatedTileStrip>();
        return AnimatedTileFramesNormalize.Set(set);
    }
}
