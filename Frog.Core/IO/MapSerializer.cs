#nullable enable
namespace Frog.Core.IO;

using System;
using System.IO;
using System.Text;
using Frog.Core.Constants;
using Frog.Core.Enums;
using Frog.Core.Maps;
using Frog.Core.Models;

/// <summary>
/// Format binaire « .fmap ». Magic « FMAP » (4), Version (octet unique),
/// puis Width (Int32), Height (Int32), Name UTF-8.
/// v4+ : octet d’options (<c>AllowPlayerOverlap</c> au bit 0). v3 omet cet octet.
/// v6 : Int32 <c>tileSizePixels</c> (48) puis les couches. Les cellules v6 stockent un <see cref="TileAssetId"/>
/// à la place de TilesetId/SrcX/SrcY. Les événements de carte ne sont pas dans ce blob (stockages existants inchangés).
/// pour chaque couche : LayerType (byte), Visible/Locked (byte×2), DisplayName UTF-8, TileCount, tuiles.
/// Après les couches, si une piste BGM ou SE est renseignée : magic « FMAU », octet de section 1, puis les deux pistes
/// (chemin UTF-8, volume Int32, fondu ms Int32). Absent = aucune. Les versions de fichier 5 et 6 ne bougent pas.
/// <see cref="Serialize"/> écrit v5 si <see cref="Map.GraphicIdentity"/> est <see cref="TileGraphicIdentity.SheetSource"/>,
/// et v6 si elle est <see cref="TileGraphicIdentity.TileAsset"/>. <see cref="MapFormat.Write"/> est le chemin v6 des nouvelles sauvegardes.
/// </summary>
public sealed class MapSerializer : ISerializer<Map>
{
    private const string Magic = "FMAP";

    /// <summary>Version écrite pour les cartes <see cref="TileGraphicIdentity.SheetSource"/> (éditeur et blobs actuels).</summary>
    private const byte FileVersionCurrent = 5;

    /// <summary>Version écrite pour les cartes <see cref="TileGraphicIdentity.TileAsset"/>.</summary>
    private const byte FileVersionTileAssets = 6;

    /// <summary>Compatibilité lecture seule avec les anciens blobs déjà livrés.</summary>
    private const byte FileVersionLegacy = 3;

    /// <summary>Section audio optionnelle en fin de blob. Ne change pas <see cref="MapFileFormatVersion"/>.</summary>
    private const string AudioSectionMagic = "FMAU";

    private const byte AudioSectionVersion = 1;

    /// <summary>
    /// Version écrite par <see cref="Serialize"/> pour une carte feuille (v5).
    /// Les nouvelles cartes TileAsset passent par <see cref="MapFormat.CurrentWriteVersion"/> (v6).
    /// </summary>
    public static byte MapFileFormatVersion => FileVersionCurrent;

    /// <summary>Version .fmap des cellules <see cref="TileAssetId"/>.</summary>
    public static byte TileAssetMapFileFormatVersion => FileVersionTileAssets;

    /// <inheritdoc />
    public byte[] Serialize(Map value)
    {
        if (!value.Validate(out var err))
            throw new InvalidDataException($"Map invalide: {err}");

        using var ms = new MemoryStream(capacity: 4096);
        using var bw = new BinaryWriter(ms, Encoding.UTF8, leaveOpen: true);

        var writeVersion = value.GraphicIdentity == TileGraphicIdentity.TileAsset
            ? FileVersionTileAssets
            : FileVersionCurrent;

        WriteAscii(bw, Magic);
        bw.Write(writeVersion);

        bw.Write(value.Width);
        bw.Write(value.Height);
        WriteUtf8(bw, value.Name);
        bw.Write((byte)(value.AllowPlayerOverlap ? 1 : 0));
        if (writeVersion == FileVersionTileAssets)
        {
            bw.Write(value.TileSizePixels);
        }

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
                WriteTile(bw, t, writeVersion);
            }
        }

        WriteAudioIfNeeded(bw, value);

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
        if (version is not (FileVersionLegacy or 4 or FileVersionCurrent or FileVersionTileAssets))
            throw new InvalidDataException(
                $"Version .fmap non supportée: {version}. Mettre à jour le client/serveur (versions attendues: {FileVersionLegacy}, 4, {FileVersionCurrent} ou {FileVersionTileAssets}).");

        var map = new Map
        {
            Width = br.ReadInt32(),
            Height = br.ReadInt32(),
            Name = ReadUtf8(br)
        };

        if (version is 4 or FileVersionCurrent or FileVersionTileAssets)
        {
            var flags = br.ReadByte();
            map.AllowPlayerOverlap = (flags & 1) != 0;
        }

        if (version == FileVersionTileAssets)
        {
            map.GraphicIdentity = TileGraphicIdentity.TileAsset;
            map.TileSizePixels = br.ReadInt32();
            if (map.TileSizePixels != TileAssetMetrics.TargetTileSizePixels)
            {
                throw new InvalidDataException(
                    $"tileSizePixels v6 = {map.TileSizePixels}, attendu {TileAssetMetrics.TargetTileSizePixels}. Pas de mise à l'échelle silencieuse.");
            }
        }
        else
        {
            map.GraphicIdentity = TileGraphicIdentity.SheetSource;
            map.TileSizePixels = 0;
        }

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

            ReadTilesIntoLayer(layer, br, tileCount, version);
            map.Layers.Add(layer);
        }

        ReadOptionalAudio(br, map);

        if (!map.Validate(out var err))
            throw new InvalidDataException($"Map désérialisée invalide: {err}");

        return map;
    }

    private static void WriteAudioIfNeeded(BinaryWriter bw, Map map)
    {
        if (map.Bgm is null || map.Se is null)
        {
            throw new InvalidDataException("Pistes audio absentes.");
        }

        if (!MapAudioTrack.TryCreate(map.Bgm.Asset, map.Bgm.Volume, map.Bgm.FadeMs, "Musique (BGM)", out var bgm, out var bgmError))
        {
            throw new InvalidDataException(bgmError ?? "Musique (BGM) invalide.");
        }

        if (!MapAudioTrack.TryCreate(map.Se.Asset, map.Se.Volume, map.Se.FadeMs, "Ambiance (SE)", out var se, out var seError))
        {
            throw new InvalidDataException(seError ?? "Ambiance (SE) invalide.");
        }

        if (bgm.IsNone && se.IsNone)
        {
            return;
        }

        WriteAscii(bw, AudioSectionMagic);
        bw.Write(AudioSectionVersion);
        WriteTrack(bw, bgm);
        WriteTrack(bw, se);
    }

    private static void WriteTrack(BinaryWriter bw, MapAudioTrack track)
    {
        WriteUtf8(bw, track.Asset);
        bw.Write(track.Volume);
        bw.Write(track.FadeMs);
    }

    private static void ReadOptionalAudio(BinaryReader br, Map map)
    {
        if (br.BaseStream.Position + 4 > br.BaseStream.Length)
        {
            return;
        }

        var magic = ReadAscii(br, 4);
        if (!string.Equals(magic, AudioSectionMagic, StringComparison.Ordinal))
        {
            return;
        }

        var sectionVersion = br.ReadByte();
        if (sectionVersion != AudioSectionVersion)
        {
            throw new InvalidDataException(
                $"Section audio .fmap non supportée : {sectionVersion}. Attendu {AudioSectionVersion}. Les versions de fichier v5/v6 ne changent pas.");
        }

        map.Bgm = ReadTrack(br, "Musique (BGM)");
        map.Se = ReadTrack(br, "Ambiance (SE)");
    }

    private static MapAudioTrack ReadTrack(BinaryReader br, string label)
    {
        var asset = ReadUtf8(br);
        var volume = br.ReadInt32();
        var fade = br.ReadInt32();
        if (!MapAudioTrack.TryCreate(asset, volume, fade, label, out var track, out var error))
        {
            throw new InvalidDataException(error ?? "Piste audio invalide.");
        }

        return track;
    }

    private static void WriteTile(BinaryWriter bw, Tile t, byte fileVersion)
    {
        bw.Write(t.X);
        bw.Write(t.Y);
        if (fileVersion == FileVersionTileAssets)
        {
            Span<byte> id = stackalloc byte[TileAssetId.ByteLength];
            t.AssetId.CopyTo(id);
            bw.Write(id);
        }
        else
        {
            bw.Write(t.TilesetId);
            bw.Write(t.SrcX);
            bw.Write(t.SrcY);
        }

        bw.Write((byte)t.Type);

        if (t.Type == TileType.Warp)
        {
            var bytes = t.WarpTargetMapId.ToByteArray();
            bw.Write(bytes);
            bw.Write(t.WarpTargetX);
            bw.Write(t.WarpTargetY);
        }

        if (t.Type == TileType.Script)
        {
            WriteUtf8(bw, t.ScriptId ?? string.Empty);
        }
    }

    private static void ReadTilesIntoLayer(Layer layer, BinaryReader br, int tileCount, byte fileVersion)
    {
        for (var j = 0; j < tileCount; j++)
        {
            var tile = new Tile
            {
                X = br.ReadInt32(),
                Y = br.ReadInt32()
            };

            if (fileVersion == FileVersionTileAssets)
            {
                var idBytes = br.ReadBytes(TileAssetId.ByteLength);
                if (idBytes.Length != TileAssetId.ByteLength)
                {
                    throw new EndOfStreamException("Flux terminé pendant la lecture du TileAssetId.");
                }

                tile.AssetId = TileAssetId.FromHashBytes(idBytes);
            }
            else
            {
                tile.TilesetId = br.ReadInt32();
                tile.SrcX = br.ReadInt32();
                tile.SrcY = br.ReadInt32();
            }

            tile.Type = (TileType)br.ReadByte();

            if (tile.Type == TileType.Warp)
            {
                if (fileVersion >= FileVersionCurrent)
                {
                    var guidBytes = br.ReadBytes(16);
                    if (guidBytes.Length != 16)
                    {
                        throw new EndOfStreamException("Flux terminé pendant la lecture du warp Guid.");
                    }

                    tile.WarpTargetMapId = new Guid(guidBytes);
                }
                else
                {
                    _ = br.ReadInt32();
                    tile.WarpTargetMapId = Guid.Empty;
                }

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
