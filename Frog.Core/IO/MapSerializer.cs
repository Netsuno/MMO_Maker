#nullable enable
namespace Frog.Core.IO;

using System;
using System.IO;
using System.Text;
using Frog.Core.Models;
using Frog.Core.Enums;

/// <summary>
/// Sérialise/Désérialise une <see cref="Map"/> dans le nouveau format binaire .fmap (versionné).
/// Format v1:
///   Magic "FMAP" (4 octets), Version (1 octet)
///   Width (Int32), Height (Int32), Name (string UTF-8), LayerCount (Int32)
///   Pour chaque layer:
///     LayerType (Byte), TileCount (Int32)
///     Pour chaque tile:
///       X (Int32), Y (Int32), TilesetId (Int32), SrcX (Int32), SrcY (Int32), TileType (Byte)
/// </summary>
public sealed class MapSerializer : ISerializer<Map>
{
    private const string Magic = "FMAP";
    private const byte Version = 1;

    /// <summary>
    /// Sérialise une carte <see cref="Map"/> en tableau d'octets (.fmap).
    /// </summary>
    public byte[] Serialize(Map value)
    {
        if (!value.Validate(out var err))
            throw new InvalidDataException($"Map invalide: {err}");

        using var ms = new MemoryStream(capacity: 4096);
        using var bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);

        // En-tête
        WriteAscii(bw, Magic);        // 4 octets
        bw.Write(Version);            // 1 octet

        // Bloc Map
        bw.Write(value.Width);
        bw.Write(value.Height);
        WriteUtf8(bw, value.Name);

        // Couches
        var layers = value.Layers ?? throw new InvalidDataException("Layers null.");
        bw.Write(layers.Count);

        foreach (var layer in layers)
        {
            bw.Write((byte)layer.LayerType);

            var tiles = layer.Tiles ?? throw new InvalidDataException("Tiles null.");
            bw.Write(tiles.Count);

            foreach (var t in tiles)
            {
                bw.Write(t.X);
                bw.Write(t.Y);
                bw.Write(t.TilesetId);
                bw.Write(t.SrcX);
                bw.Write(t.SrcY);
                bw.Write((byte)t.Type);
            }
        }

        bw.Flush();
        return ms.ToArray();
    }

    /// <summary>
    /// Désérialise des octets .fmap en <see cref="Map"/>.
    /// </summary>
    public Map Deserialize(ReadOnlySpan<byte> data)
    {
        using var ms = new MemoryStream(data.ToArray(), writable: false);
        using var br = new BinaryReader(ms, Encoding.UTF8, leaveOpen: true);

        // En-tête
        var magic = ReadAscii(br, 4);
        if (!string.Equals(magic, Magic, StringComparison.Ordinal))
            throw new InvalidDataException($"Magic invalide: '{magic}' (attendu '{Magic}').");

        var version = br.ReadByte();
        if (version != Version)
            throw new InvalidDataException($"Version non supportée: {version} (attendu {Version}).");

        // Bloc Map
        var map = new Map
        {
            Width = br.ReadInt32(),
            Height = br.ReadInt32(),
            Name = ReadUtf8(br)
        };

        // Couches
        var layerCount = br.ReadInt32();
        if (layerCount < 0 || layerCount > 1024)
            throw new InvalidDataException($"LayerCount anormal: {layerCount}");

        for (int i = 0; i < layerCount; i++)
        {
            var lt = (LayerType)br.ReadByte();
            var tileCount = br.ReadInt32();
            if (tileCount < 0 || tileCount > 1_000_000)
                throw new InvalidDataException($"TileCount anormal (layer {i}): {tileCount}");

            var layer = new Layer
            {
                LayerType = lt
            };

            for (int j = 0; j < tileCount; j++)
            {
                var tile = new Tile
                {
                    X = br.ReadInt32(),
                    Y = br.ReadInt32(),
                    TilesetId = br.ReadInt32(),
                    SrcX = br.ReadInt32(),
                    SrcY = br.ReadInt32(),
                    Type = (TileType)br.ReadByte()
                };
                layer.Tiles.Add(tile);
            }

            map.Layers.Add(layer);
        }

        if (!map.Validate(out var err))
            throw new InvalidDataException($"Map désérialisée invalide: {err}");

        return map;
    }

    // --- Helpers ---

    /// <summary>Écrit une chaîne ASCII fixe (utilisée pour le Magic).</summary>
    private static void WriteAscii(BinaryWriter bw, string ascii)
    {
        // #NOTE: supposé ASCII 7-bit; si besoin, on peut valider chaque char < 128.
        var bytes = Encoding.ASCII.GetBytes(ascii);
        bw.Write(bytes);
    }

    /// <summary>Lit une chaîne ASCII de longueur fixe.</summary>
    private static string ReadAscii(BinaryReader br, int len)
    {
        var bytes = br.ReadBytes(len);
        if (bytes.Length != len)
            throw new EndOfStreamException("Flux terminé pendant la lecture ASCII.");
        return Encoding.ASCII.GetString(bytes);
    }

    /// <summary>Écrit une chaîne UTF-8 préfixée par sa longueur (Int32 octets).</summary>
    private static void WriteUtf8(BinaryWriter bw, string value)
    {
        value ??= string.Empty;
        var bytes = Encoding.UTF8.GetBytes(value);
        bw.Write(bytes.Length);
        bw.Write(bytes);
    }

    /// <summary>Lit une chaîne UTF-8 préfixée par sa longueur (Int32 octets).</summary>
    private static string ReadUtf8(BinaryReader br)
    {
        var len = br.ReadInt32();
        if (len < 0 || len > 10_000_000)
            throw new InvalidDataException($"Longueur de chaîne invalide: {len}");
        var bytes = br.ReadBytes(len);
        if (bytes.Length != len)
            throw new EndOfStreamException("Flux terminé pendant la lecture UTF-8.");
        return Encoding.UTF8.GetString(bytes);
    }
}
