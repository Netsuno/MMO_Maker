using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;

namespace Frog.Editor.Assets;

/// <summary>Cache Bitmap + métadonnées pour plusieurs tilesets (style RPG Maker : liste A/B/C…).</summary>
internal static class TilesetCache
{
    private static readonly Dictionary<int, Bitmap> _byId = new();
    private static readonly Dictionary<int, string> _labelById = new();
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

        if (_byId.TryGetValue(id, out var old))
        {
            old.Dispose();
        }

        using var tmp = new Bitmap(path);
        _byId[id] = new Bitmap(tmp);
        _labelById[id] = Path.GetFileName(path);
        _nextId = Math.Max(_nextId, id + 1);
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
        _nextId = 1;
    }
}
