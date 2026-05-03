#nullable enable
namespace Frog.Core.IO;

using System;
using System.IO;
using System.Text;
using Frog.Core.Models;
using Frog.Core.Enums;

/// <summary>
/// Format binaire « .fmap » courant uniquement ; le client/serveur sont mis à jour avec le projet.
/// Magic « FMAP » (4), Version (octet unique),
/// puis Width (Int32), Height (Int32), Name UTF-8, LayerCount,
/// pour chaque couche : LayerType (byte), Visible/Locked (byte×2), DisplayName UTF-8, TileCount, tuiles.
/// </summary>
public sealed class MapSerializer : ISerializer<Map>
{
    private const string Magic = "FMAP";

    /// <summary>Incrementer ce numéro des que le bloc binaire change (plus de compat. arrière).</summary>
    private const byte FileVersion = 3;

    /// <inheritdoc />
    public byte[] Serialize(Map value)
    {
        if (!value.Validate(out var err))
            throw new InvalidDataException($"Map invalide: {err}");

        using var ms = new MemoryStream(capacity: 4096);
        using var bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);

        WriteAscii(bw, Magic);
        bw.Write(FileVersion);

        bw.Write(value.Width);
        bw.Write(value.Height);
        WriteUtf8(bw, value.Name);

        var layers = value.Layers ?? throw new InvalidDataException("Layers null.");
        bw.Write(layers.Count);

        foreach (var layer in layers)
        {
            bw.Write((byte)layer.LayerType);
            bw.Write((byte)(layer.Visible ? 1 : 0));
            bw.Write((byte)(layer.Locked ? 1 : 0));
            WriteUtf8(bw, layer.DisplayName ?? string.Empty);

            var tiles = layer.Tiles ?? throw new InvalidDataException("Tiles null.");
            bw.Write(tiles.Count);

            foreach (var t in tiles)
            {
                WriteTile(bw, t);
            }
        }

        bw.Flush();
        return ms.ToArray();
    }

    /// <inheritdoc />
    public Map Deserialize(ReadOnlySpan<byte> data)
    {
        using var ms = new MemoryStream(data.ToArray(), writable: false);
        using var br = new BinaryReader(ms, Encoding.UTF8, leaveOpen: true);

        var magic = ReadAscii(br, 4);
        if (!string.Equals(magic, Magic, StringComparison.Ordinal))
            throw new InvalidDataException($"Magic invalide: '{magic}' (attendu '{Magic}').");

        var version = br.ReadByte();
        if (version != FileVersion)
            throw new InvalidDataException($"Version .fmap non supportée: {version}. Mettre à jour le client/serveur (version attendue: {FileVersion}).");

        var map = new Map
        {
            Width = br.ReadInt32(),
            Height = br.ReadInt32(),
            Name = ReadUtf8(br)
        };

        var layerCount = br.ReadInt32();
        if (layerCount < 0 || layerCount > 1024)
            throw new InvalidDataException($"LayerCount anormal: {layerCount}");

        for (var i = 0; i < layerCount; i++)
        {
            var lt = (LayerType)br.ReadByte();
            var visible = br.ReadByte() != 0;
            var locked = br.ReadByte() != 0;
            var displayName = ReadUtf8(br);
            var tileCount = br.ReadInt32();

            if (tileCount < 0 || tileCount > 1_000_000)
                throw new InvalidDataException($"TileCount anormal (layer {i}): {tileCount}");

            var layer = new Layer
            {
                LayerType = lt,
                Visible = visible,
                Locked = locked,
                DisplayName = displayName ?? string.Empty
            };

            ReadTilesIntoLayer(layer, br, tileCount);
            map.Layers.Add(layer);
        }

        if (!map.Validate(out var err))
            throw new InvalidDataException($"Map désérialisée invalide: {err}");

        return map;
    }

    private static void WriteTile(BinaryWriter bw, Tile t)
    {
        bw.Write(t.X);
        bw.Write(t.Y);
        bw.Write(t.TilesetId);
        bw.Write(t.SrcX);
        bw.Write(t.SrcY);
        bw.Write((byte)t.Type);

        if (t.Type == TileType.Warp)
        {
            bw.Write(t.WarpTargetMapId);
            bw.Write(t.WarpTargetX);
            bw.Write(t.WarpTargetY);
        }

        if (t.Type == TileType.Script)
        {
            WriteUtf8(bw, t.ScriptId ?? string.Empty);
        }
    }

    private static void ReadTilesIntoLayer(Layer layer, BinaryReader br, int tileCount)
    {
        for (var j = 0; j < tileCount; j++)
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

            if (tile.Type == TileType.Warp)
            {
                tile.WarpTargetMapId = br.ReadInt32();
                tile.WarpTargetX = br.ReadInt32();
                tile.WarpTargetY = br.ReadInt32();
            }

            if (tile.Type == TileType.Script)
            {
                tile.ScriptId = ReadUtf8(br);
            }

            layer.Tiles.Add(tile);
        }
    }

    private static void WriteAscii(BinaryWriter bw, string ascii)
    {
        var bytes = Encoding.ASCII.GetBytes(ascii);
        bw.Write(bytes);
    }

    private static string ReadAscii(BinaryReader br, int len)
    {
        var bytes = br.ReadBytes(len);
        if (bytes.Length != len)
            throw new EndOfStreamException("Flux terminé pendant la lecture ASCII.");
        return Encoding.ASCII.GetString(bytes);
    }

    private static void WriteUtf8(BinaryWriter bw, string value)
    {
        value ??= string.Empty;
        var bytes = Encoding.UTF8.GetBytes(value);
        bw.Write(bytes.Length);
        bw.Write(bytes);
    }

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
