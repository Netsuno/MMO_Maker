using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using Frog.Application.Assets;

namespace Frog.Editor.Assets;

/// <summary>Cache Bitmap + métadonnées pour plusieurs tilesets (style RPG Maker : liste A/B/C…).</summary>
internal static class TilesetCache
{
    private static readonly Dictionary<int, Bitmap> _byId = new();
    private static readonly Dictionary<int, string> _labelById = new();
    private static readonly Dictionary<int, string> _sourcePathById = new();
    private static int _nextId = 1;

    public static int LoadFromFile(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException(path);
        }

        using var tmp = new Bitmap(path);
        var bmp = new Bitmap(tmp);
        var id = _nextId++;
        _byId[id] = bmp;
        _labelById[id] = Path.GetFileName(path);
        _sourcePathById[id] = Path.GetFullPath(path);
        return id;
    }

    /// <summary>
    /// Charge une image sous un <paramref name="id"/> fixe (réouverture carte + manifeste).
    /// Remplace une entrée existante du même id.
    /// </summary>
    public static void LoadFromFileAtId(string path, int id)
    {
        if (id < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(id), id, "TilesetId doit être >= 1.");
        }

        if (!File.Exists(path))
        {
            throw new FileNotFoundException(path);
        }

        using var tmp = new Bitmap(path);
        ReplaceAtId(id, new Bitmap(tmp), Path.GetFileName(path), Path.GetFullPath(path));
    }

    public static void LoadFromPngBytesAtId(byte[] pngBytes, int id, string? label = null)
    {
        if (id < 1)
        {
            throw new ArgumentOutOfRangeException(nameof(id), id, "TilesetId doit être >= 1.");
        }

        ArgumentNullException.ThrowIfNull(pngBytes);
        using var ms = new MemoryStream(pngBytes, writable: false);
        using var tmp = new Bitmap(ms);
        ReplaceAtId(id, new Bitmap(tmp), label ?? $"{id}.png", sourcePath: null);
    }

    private static void ReplaceAtId(int id, Bitmap bmp, string label, string? sourcePath)
    {
        if (_byId.TryGetValue(id, out var old))
        {
            old.Dispose();
        }

        _byId[id] = bmp;
        _labelById[id] = label;
        if (!string.IsNullOrWhiteSpace(sourcePath))
        {
            _sourcePathById[id] = sourcePath;
        }
        else
        {
            _sourcePathById.Remove(id);
        }

        _nextId = Math.Max(_nextId, id + 1);
    }

    public static string? TryGetSourcePath(int id)
        => _sourcePathById.TryGetValue(id, out var path) ? path : null;

    public static IReadOnlyList<MapTilesetFile> SnapshotPngFiles()
    {
        var list = new List<MapTilesetFile>();
        foreach (var id in _byId.Keys.OrderBy(k => k))
        {
            using var ms = new MemoryStream();
            _byId[id].Save(ms, ImageFormat.Png);
            list.Add(new MapTilesetFile(id, ms.ToArray()));
        }

        return list;
    }

    public static bool TryGet(int tilesetId, out Bitmap? bmp)
    {
        var ok = _byId.TryGetValue(tilesetId, out var b);
        bmp = b;
        return ok;
    }

    public static IReadOnlyList<(int Id, string Label)> ListRegistered()
        => _byId.Keys.OrderBy(k => k).Select(k => (k, _labelById.GetValueOrDefault(k, $"#{k}"))).ToArray();

    public static string GetLabel(int id) => _labelById.GetValueOrDefault(id, $"#{id}");

    public static void Clear()
    {
        foreach (var b in _byId.Values)
        {
            b.Dispose();
        }

        _byId.Clear();
        _labelById.Clear();
        _sourcePathById.Clear();
        _nextId = 1;
    }
}
