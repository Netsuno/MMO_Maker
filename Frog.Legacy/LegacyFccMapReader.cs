#nullable enable
namespace Frog.Legacy;

using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using Frog.Core.Enums;
using Frog.Core.Models;

/// <summary>
/// Lecteur des cartes VB6 <c>mapN.fcc</c> (Put/Get de <c>MapRec</c>).
/// Les 13 couches Long + Type/Data1-3 sont lus avec confiance ; le packing exact
/// des String/*Set dans les 88 octets restants reste signalé en avertissement.
/// </summary>
public sealed class LegacyFccMapReader
{
    public const int ObservedTileRecordSize = 88;
    public const int ObservedHeaderTilesOffset = 92;

    public LegacyFccMapReadResult Read(string path)
    {
        var bytes = File.ReadAllBytes(path);
        return Read(bytes, path);
    }

    public LegacyFccMapReadResult Read(ReadOnlySpan<byte> data, string sourcePath)
    {
        var sha = Convert.ToHexString(SHA256.HashData(data)).ToLowerInvariant();
        var report = new LegacyImportReport
        {
            SourcePath = sourcePath,
            Sha256Hex = sha,
            Success = false,
        };

        if (data.Length < ObservedHeaderTilesOffset + ObservedTileRecordSize)
        {
            report.Errors.Add($"Fichier trop court ({data.Length} octets).");
            return new LegacyFccMapReadResult { Report = report };
        }

        var name = Encoding.Latin1.GetString(data[..40]).TrimEnd();
        var revision = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(40, 4));
        var moral = data[44];
        var up = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(45, 4));
        var down = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(49, 4));
        var left = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(53, 4));
        var right = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(57, 4));

        var musicLen = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(61, 4));
        if (musicLen < 0 || 65 + musicLen > data.Length)
        {
            report.Errors.Add($"Longueur Music invalide: {musicLen}.");
            return new LegacyFccMapReadResult { Report = report };
        }

        var o = 65 + musicLen;
        if (o + 11 > data.Length)
        {
            report.Errors.Add("En-tête tronqué après Music.");
            return new LegacyFccMapReadResult { Report = report };
        }

        var bootMap = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(o, 4));
        o += 4;
        var bootX = data[o++];
        var bootY = data[o++];
        var shop = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(o, 4));
        o += 4;
        var indoors = data[o++];

        if (o + 16 > data.Length)
        {
            report.Errors.Add("Descripteur de tableau tuile manquant.");
            return new LegacyFccMapReadResult { Report = report };
        }

        var width = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(o, 4));
        var xBound = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(o + 4, 4));
        var height = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(o + 8, 4));
        var yBound = BinaryPrimitives.ReadInt32LittleEndian(data.Slice(o + 12, 4));
        o += 16;

        if (xBound != 0 || yBound != 0 || width <= 0 || height <= 0 || width > 512 || height > 512)
        {
            report.Errors.Add($"Bornes tableau invalides: {width}x{height} lbound=({xBound},{yBound}).");
            return new LegacyFccMapReadResult { Report = report };
        }

        var cellCount = checked(width * height);
        var tilesBytes = checked(cellCount * ObservedTileRecordSize);
        if (o + tilesBytes > data.Length)
        {
            report.Errors.Add($"Tuiles tronquées: besoin {tilesBytes} octets à partir de {o}, fichier {data.Length}.");
            return new LegacyFccMapReadResult { Report = report };
        }

        report.Warnings.Add(
            "Records tuile 88 octets: String1-3 et *Set non entièrement prouvés; indices graphiques legacy stockés dans SrcX.");
        if (shop != 0)
        {
            report.Warnings.Add($"Shop brut={shop} (valeur non interprétée au-delà du champ Long).");
        }

        var ground = new Layer { LayerType = LayerType.Ground, DisplayName = "Ground" };
        var mask = new Layer { LayerType = LayerType.Mask, DisplayName = "Mask" };
        var mask2 = new Layer { LayerType = LayerType.Mask2, DisplayName = "Mask2" };
        var fringe = new Layer { LayerType = LayerType.Fringe, DisplayName = "Fringe" };
        var fringe2 = new Layer { LayerType = LayerType.Fringe2, DisplayName = "Fringe2" };
        var attributes = new Layer { LayerType = LayerType.Attributes, DisplayName = "Attributes" };

        var unsupportedAttrCounts = new Dictionary<byte, int>();

        for (var i = 0; i < cellCount; i++)
        {
            var tileOffset = o + i * ObservedTileRecordSize;
            var rec = data.Slice(tileOffset, ObservedTileRecordSize);
            var x = i % width;
            var y = i / width;

            var groundId = BinaryPrimitives.ReadInt32LittleEndian(rec.Slice(0, 4));
            var maskId = BinaryPrimitives.ReadInt32LittleEndian(rec.Slice(4, 4));
            var animId = BinaryPrimitives.ReadInt32LittleEndian(rec.Slice(8, 4));
            var mask2Id = BinaryPrimitives.ReadInt32LittleEndian(rec.Slice(12, 4));
            var m2AnimId = BinaryPrimitives.ReadInt32LittleEndian(rec.Slice(16, 4));
            var mask3Id = BinaryPrimitives.ReadInt32LittleEndian(rec.Slice(20, 4));
            var m3AnimId = BinaryPrimitives.ReadInt32LittleEndian(rec.Slice(24, 4));
            var fringeId = BinaryPrimitives.ReadInt32LittleEndian(rec.Slice(28, 4));
            var fAnimId = BinaryPrimitives.ReadInt32LittleEndian(rec.Slice(32, 4));
            var fringe2Id = BinaryPrimitives.ReadInt32LittleEndian(rec.Slice(36, 4));
            var f2AnimId = BinaryPrimitives.ReadInt32LittleEndian(rec.Slice(40, 4));
            var fringe3Id = BinaryPrimitives.ReadInt32LittleEndian(rec.Slice(44, 4));
            var f3AnimId = BinaryPrimitives.ReadInt32LittleEndian(rec.Slice(48, 4));

            var attrType = rec[52];
            var data1 = BinaryPrimitives.ReadInt32LittleEndian(rec.Slice(53, 4));
            var data2 = BinaryPrimitives.ReadInt32LittleEndian(rec.Slice(57, 4));
            var data3 = BinaryPrimitives.ReadInt32LittleEndian(rec.Slice(61, 4));

            if (mask3Id != 0 || m3AnimId != 0 || fringe3Id != 0 || f3AnimId != 0 || animId != 0 || m2AnimId != 0 || fAnimId != 0 || f2AnimId != 0)
            {
                // Une seule fois par type de signal pour limiter le bruit serait idéal ; on compte via unsupported.
                unsupportedAttrCounts.TryGetValue(255, out var c);
                unsupportedAttrCounts[255] = c + 1;
            }

            AddGraphic(ground, x, y, groundId);
            AddGraphic(mask, x, y, maskId);
            AddGraphic(mask2, x, y, mask2Id);
            AddGraphic(fringe, x, y, fringeId);
            AddGraphic(fringe2, x, y, fringe2Id);

            switch ((LegacyTileAttributeType)attrType)
            {
                case LegacyTileAttributeType.Walkable:
                    break;
                case LegacyTileAttributeType.Blocked:
                    attributes.Tiles.Add(new Tile
                    {
                        X = x,
                        Y = y,
                        Type = TileType.Block,
                        Attributes = { new BlockAttribute() },
                    });
                    break;
                case LegacyTileAttributeType.Warp:
                    attributes.Tiles.Add(new Tile
                    {
                        X = x,
                        Y = y,
                        Type = TileType.Warp,
                        WarpTargetMapId = data1,
                        WarpTargetX = data2,
                        WarpTargetY = data3,
                        Attributes =
                        {
                            new WarpAttribute
                            {
                                TargetMapId = data1,
                                TargetX = data2,
                                TargetY = data3,
                            },
                        },
                    });
                    break;
                default:
                    unsupportedAttrCounts.TryGetValue(attrType, out var n);
                    unsupportedAttrCounts[attrType] = n + 1;
                    break;
            }
        }

        foreach (var (code, count) in unsupportedAttrCounts.OrderBy(kv => kv.Key))
        {
            if (code == 255)
            {
                report.Warnings.Add(
                    $"{count} cellule(s) avec couches Anim/Mask3/Fringe3 non projetées vers LayerType moderne.");
                continue;
            }

            report.Unsupported.Add(
                $"TILE_TYPE {code} ({(LegacyTileAttributeType)code}) sur {count} cellule(s) — non mappé au modèle moderne.");
        }

        var trailer = data.Length - (o + tilesBytes);
        if (trailer < 0)
        {
            report.Errors.Add("Trailer négatif (bug interne).");
            return new LegacyFccMapReadResult { Report = report };
        }

        if (trailer > 0)
        {
            report.Warnings.Add(
                $"Trailer post-tuiles: {trailer} octets (Npc/Npcs/Pano/Fog…) non décodés dans cette version.");
        }

        _ = bootMap;
        _ = bootX;
        _ = bootY;
        _ = indoors;

        var map = new Map
        {
            Width = width,
            Height = height,
            Name = name,
        };
        map.Layers.Add(ground);
        map.Layers.Add(mask);
        map.Layers.Add(mask2);
        map.Layers.Add(fringe);
        map.Layers.Add(fringe2);
        map.Layers.Add(attributes);

        if (!map.Validate(out var err))
        {
            report.Errors.Add($"Map domaine invalide: {err}");
            return new LegacyFccMapReadResult { Report = report, Revision = revision, Moral = moral, Up = up, Down = down, Left = left, Right = right };
        }

        report.Success = report.Errors.Count == 0;
        return new LegacyFccMapReadResult
        {
            Report = report,
            Map = map,
            Revision = revision,
            Moral = moral,
            Up = up,
            Down = down,
            Left = left,
            Right = right,
        };
    }

    private static void AddGraphic(Layer layer, int x, int y, int legacyTileIndex)
    {
        if (legacyTileIndex == 0)
        {
            return;
        }

        layer.Tiles.Add(new Tile
        {
            X = x,
            Y = y,
            Type = TileType.Ground,
            TilesetId = 0,
            // Provisional: index tuile legacy jusqu’à mapping sheet tileset.
            SrcX = legacyTileIndex,
            SrcY = 0,
        });
    }
}
