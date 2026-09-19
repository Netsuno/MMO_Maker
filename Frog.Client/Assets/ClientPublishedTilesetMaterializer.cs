#nullable enable
using System.IO;
using Frog.Application.Assets;
using Frog.Core.Models;
using Frog.Core.Protocol;

namespace Frog.Client.Assets;

/// <summary>
/// Matérialise les PNG du catalogue publié vers <c>Tilesets/{paletteId}.png</c>
/// (même layout que <see cref="ClientTilesetLoader"/>).
/// </summary>
public static class ClientPublishedTilesetMaterializer
{
    public static int Materialize(PublishedCatalogWire? catalog, string appBaseDirectory)
    {
        if (catalog is null || catalog.Tilesets.Count == 0 || string.IsNullOrWhiteSpace(appBaseDirectory))
        {
            return 0;
        }

        var written = new List<MapTilesetFile>();
        foreach (var entry in catalog.Tilesets)
        {
            if (entry.PaletteId <= 0 || string.IsNullOrWhiteSpace(entry.PngBase64))
            {
                continue;
            }

            byte[] bytes;
            try
            {
                bytes = Convert.FromBase64String(entry.PngBase64);
            }
            catch
            {
                continue;
            }

            if (bytes.Length == 0)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(entry.Sha256Hex) && entry.Sha256Hex.Length == 64)
            {
                var actual = TilesetDefinition.ComputeSha256Hex(bytes);
                if (!actual.Equals(entry.Sha256Hex, StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }
            }

            written.Add(new MapTilesetFile(entry.PaletteId, bytes));
        }

        if (written.Count == 0)
        {
            return 0;
        }

        MapTilesetPackage.WriteSidecars(appBaseDirectory, Array.Empty<string>(), written);
        return written.Count;
    }
}
