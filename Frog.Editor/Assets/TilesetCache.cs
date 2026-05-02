using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;

namespace Frog.Editor.Assets
{
    /// <summary>
    /// Cache très simple des tilesets (Bitmap). Identifiants entiers auto-incrémentés.
    /// </summary>
    internal static class TilesetCache
    {
        private static readonly Dictionary<int, Bitmap> _byId = new();
        private static int _nextId = 1;

        /// <summary>Charge un PNG/JPG en mémoire et retourne son identifiant.</summary>
        public static int LoadFromFile(string path)
        {
            if (!File.Exists(path)) throw new FileNotFoundException(path);
            using var tmp = new Bitmap(path);
            // On fait une copie pour libérer le handle de fichier.
            var bmp = new Bitmap(tmp);
            var id = _nextId++;
            _byId[id] = bmp;
            return id;
        }

        public static bool TryGet(int tilesetId, out Bitmap? bmp)
        {
            var ok = _byId.TryGetValue(tilesetId, out var b);
            bmp = b;
            return ok;
        }

        /// <summary>Libère tous les bitmaps (appelé à la fermeture de l’éditeur).</summary>
        public static void Clear()
        {
            foreach (var b in _byId.Values) b.Dispose();
            _byId.Clear();
            _nextId = 1;
        }
    }
}
