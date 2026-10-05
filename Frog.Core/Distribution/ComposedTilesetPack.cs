using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;

using Frog.Core.Constants;
using Frog.Core.Maps;
using Frog.Core.Models;

namespace Frog.Core.Distribution;

/// <summary>
/// Charge protégée des tilesets composés. Même confiance que <c>.frogpack</c> :
/// signature Ed25519 sur le SHA-256 du corps, clé publique épinglée.
/// Le bit de chiffrement reste refusé (pas un second format chiffré maison).
/// Hello TCP : <see cref="FrogWireProtocol.Version"/>. Tuiles : <see cref="TileAssetMetrics.TargetTileSizePixels"/>.
/// </summary>
public static class ComposedTilesetPackFormat
{
    public const string Magic = "CTS1";

    /// <summary>GET HTTP du canal contenu. Pas un opcode du Hello.</summary>
    public const string HttpPath = "/content/composed-tilesets";
    public const ushort FormatVersion = 1;
    public const ushort FlagsNone = 0;

    /// <summary>Même bit que <see cref="FrogPackFormat.FlagEncrypted"/>. Refusé : ce document est signé, pas chiffré.</summary>
    public const ushort FlagEncrypted = FrogPackFormat.FlagEncrypted;

    public const int HeaderLength = 20;
    public const int MaxTilesets = 1024;
    public const int MaxNameUtf8Bytes = 480;

    public static bool IsUnprotectedCatalog(ReadOnlySpan<byte> payload)
    {
        var i = 0;
        if (payload.Length >= 3 && payload[0] == 0xEF && payload[1] == 0xBB && payload[2] == 0xBF)
        {
            i = 3;
        }

        while (i < payload.Length && payload[i] is (byte)' ' or (byte)'\t' or (byte)'\r' or (byte)'\n')
        {
            i++;
        }

        return i < payload.Length && payload[i] is (byte)'{' or (byte)'[';
    }
}

public static class ComposedTilesetPackWriter
{
    public static byte[] Write(IReadOnlyList<ComposedTilesetDefinition> tilesets, ReadOnlySpan<byte> privateSeed)
    {
        ArgumentNullException.ThrowIfNull(tilesets);
        var publicKey = FrogPackKeys.PublicKeyFromSeed(privateSeed);
        var orderedSets = OrderTilesets(tilesets);
        var unique = CollectUniqueTiles(orderedSets);
        var body = BuildBody(orderedSets, unique);
        var contentHash = SHA256.HashData(body);
        var signature = FrogPackKeys.Sign(privateSeed, contentHash);
        if (signature.Length != FrogPackFormat.SignatureLength)
        {
            throw new InvalidDataException("Signature Ed25519 inattendue.");
        }

        var file = new byte[body.Length + FrogPackFormat.TrailerLength];
        body.CopyTo(file, 0);
        contentHash.CopyTo(file.AsSpan(body.Length));
        publicKey.CopyTo(file.AsSpan(body.Length + FrogPackFormat.ContentHashLength));
        signature.CopyTo(file.AsSpan(body.Length + FrogPackFormat.ContentHashLength + FrogPackFormat.PublicKeyLength));
        return file;
    }

    private static byte[] BuildBody(
        IReadOnlyList<ComposedTilesetDefinition> tilesets,
        IReadOnlyList<(TileAssetId Id, byte[] Rgba)> unique)
    {
        var size = ComposedTilesetPackFormat.HeaderLength;
        size += unique.Count * (TileAssetId.ByteLength + TileAssetMetrics.CanonicalPixelByteCount);
        foreach (var set in tilesets)
        {
            var nameBytes = Encoding.UTF8.GetBytes(set.Name.Trim());
            size += 16 + 2 + nameBytes.Length + 4;
            size += set.Tiles.Count * (TileAssetId.ByteLength + 2);
            foreach (var tile in set.Tiles)
            {
                var display = tile.DisplayName ?? string.Empty;
                size += Encoding.UTF8.GetByteCount(display);
            }
        }

        var body = new byte[size];
        body[0] = (byte)'C';
        body[1] = (byte)'T';
        body[2] = (byte)'S';
        body[3] = (byte)'1';
        BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(4), ComposedTilesetPackFormat.FormatVersion);
        BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(6), ComposedTilesetPackFormat.FlagsNone);
        BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(8), FrogWireProtocol.Version);
        BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(10), (ushort)TileAssetMetrics.TargetTileSizePixels);
        BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(12), (uint)tilesets.Count);
        BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(16), (uint)unique.Count);

        var cursor = ComposedTilesetPackFormat.HeaderLength;
        foreach (var tile in unique)
        {
            tile.Id.CopyTo(body.AsSpan(cursor, TileAssetId.ByteLength));
            cursor += TileAssetId.ByteLength;
            tile.Rgba.CopyTo(body.AsSpan(cursor, TileAssetMetrics.CanonicalPixelByteCount));
            cursor += TileAssetMetrics.CanonicalPixelByteCount;
        }

        foreach (var set in tilesets)
        {
            set.Id.ToByteArray().CopyTo(body.AsSpan(cursor, 16));
            cursor += 16;
            var nameBytes = Encoding.UTF8.GetBytes(set.Name.Trim());
            BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(cursor), (ushort)nameBytes.Length);
            cursor += 2;
            nameBytes.CopyTo(body.AsSpan(cursor));
            cursor += nameBytes.Length;
            BinaryPrimitives.WriteUInt32LittleEndian(body.AsSpan(cursor), (uint)set.Tiles.Count);
            cursor += 4;
            foreach (var tile in set.Tiles)
            {
                TileAssetId.Parse(tile.TileAssetId).CopyTo(body.AsSpan(cursor, TileAssetId.ByteLength));
                cursor += TileAssetId.ByteLength;
                var display = Encoding.UTF8.GetBytes(tile.DisplayName ?? string.Empty);
                BinaryPrimitives.WriteUInt16LittleEndian(body.AsSpan(cursor), (ushort)display.Length);
                cursor += 2;
                display.CopyTo(body.AsSpan(cursor));
                cursor += display.Length;
            }
        }

        if (cursor != body.Length)
        {
            throw new InvalidDataException("Corps de tileset composé de taille inattendue.");
        }

        return body;
    }

    private static List<ComposedTilesetDefinition> OrderTilesets(IReadOnlyList<ComposedTilesetDefinition> tilesets)
    {
        if (tilesets.Count > ComposedTilesetPackFormat.MaxTilesets)
        {
            throw new ArgumentException($"Au plus {ComposedTilesetPackFormat.MaxTilesets} tilesets.", nameof(tilesets));
        }

        var copy = new List<ComposedTilesetDefinition>(tilesets.Count);
        var seen = new HashSet<Guid>();
        foreach (var set in tilesets)
        {
            if (set is null)
            {
                throw new ArgumentException("Tileset composé invalide.", nameof(tilesets));
            }

            if (!set.Validate(out var error))
            {
                throw new ArgumentException(error ?? "Tileset composé invalide.", nameof(tilesets));
            }

            if (!seen.Add(set.Id))
            {
                throw new ArgumentException("Identifiant de tileset en double.", nameof(tilesets));
            }

            var nameBytes = Encoding.UTF8.GetByteCount(set.Name.Trim());
            if (nameBytes is < 1 or > ComposedTilesetPackFormat.MaxNameUtf8Bytes)
            {
                throw new ArgumentException("Nom de tileset hors limite UTF-8.", nameof(tilesets));
            }

            copy.Add(set);
        }

        copy.Sort(static (a, b) => CompareGuid(a.Id, b.Id));
        return copy;
    }

    private static List<(TileAssetId Id, byte[] Rgba)> CollectUniqueTiles(IReadOnlyList<ComposedTilesetDefinition> tilesets)
    {
        var map = new Dictionary<TileAssetId, byte[]>();
        foreach (var set in tilesets)
        {
            foreach (var tile in set.Tiles)
            {
                var id = TileAssetId.Parse(tile.TileAssetId);
                if (map.TryGetValue(id, out var existing))
                {
                    if (!existing.AsSpan().SequenceEqual(tile.NormalizedRgba))
                    {
                        throw new InvalidDataException("Pixels divergents pour " + id.ToHex() + ".");
                    }

                    continue;
                }

                map.Add(id, tile.NormalizedRgba);
            }
        }

        var list = map.Select(pair => (pair.Key, pair.Value)).ToList();
        list.Sort(static (a, b) => a.Key.CompareTo(b.Key));
        if (list.Count > FrogPackFormat.MaxTileCount)
        {
            throw new ArgumentException("Trop de tuiles distinctes.");
        }

        return list;
    }

    internal static int CompareGuid(Guid left, Guid right)
    {
        var a = left.ToByteArray();
        var b = right.ToByteArray();
        for (var i = 0; i < 16; i++)
        {
            var cmp = a[i].CompareTo(b[i]);
            if (cmp != 0)
            {
                return cmp;
            }
        }

        return 0;
    }
}

public static class ComposedTilesetPackReader
{
    public static IReadOnlyList<ComposedTilesetDefinition> Read(ReadOnlySpan<byte> data, ReadOnlySpan<byte> trustedPublicKey)
    {
        if (ComposedTilesetPackFormat.IsUnprotectedCatalog(data))
        {
            throw new FrogPackRejectedException("Catalogue brut non protégé refusé.");
        }

        if (trustedPublicKey.Length != FrogPackFormat.PublicKeyLength)
        {
            throw new ArgumentException("Clé publique Ed25519 : 32 octets.", nameof(trustedPublicKey));
        }

        if (data.Length < ComposedTilesetPackFormat.HeaderLength + FrogPackFormat.TrailerLength)
        {
            throw new FrogPackRejectedException("Charge tileset trop courte.");
        }

        if (data[0] != (byte)'C' || data[1] != (byte)'T' || data[2] != (byte)'S' || data[3] != (byte)'1')
        {
            throw new FrogPackRejectedException("Magic CTS1 attendu.");
        }

        var version = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(4, 2));
        if (version != ComposedTilesetPackFormat.FormatVersion)
        {
            throw new FrogPackRejectedException($"Version de charge tileset non supportée : {version}.");
        }

        var flags = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(6, 2));
        if ((flags & ComposedTilesetPackFormat.FlagEncrypted) != 0)
        {
            throw new FrogPackRejectedException("Chiffrement de la charge tileset refusé (signature Ed25519, pas un chiffre maison).");
        }

        if (flags != ComposedTilesetPackFormat.FlagsNone)
        {
            throw new FrogPackRejectedException("Flags de charge tileset inconnus.");
        }

        var protocol = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(8, 2));
        if (protocol != FrogWireProtocol.Version)
        {
            throw new FrogPackRejectedException(
                $"protocolVersion = {protocol}, Hello client = {FrogWireProtocol.Version}.");
        }

        var tileSize = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(10, 2));
        if (tileSize != TileAssetMetrics.TargetTileSizePixels)
        {
            throw new FrogPackRejectedException(
                $"tileSizePixels = {tileSize}, attendu {TileAssetMetrics.TargetTileSizePixels}.");
        }

        var tilesetCount = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(12, 4));
        var uniqueCount = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(16, 4));
        if (tilesetCount > ComposedTilesetPackFormat.MaxTilesets || uniqueCount > FrogPackFormat.MaxTileCount)
        {
            throw new FrogPackRejectedException("Nombre de tilesets ou de tuiles hors limite.");
        }

        var bodyLength = data.Length - FrogPackFormat.TrailerLength;
        var body = data[..bodyLength];
        var contentHash = data.Slice(bodyLength, FrogPackFormat.ContentHashLength);
        var embeddedKey = data.Slice(bodyLength + FrogPackFormat.ContentHashLength, FrogPackFormat.PublicKeyLength);
        var signature = data.Slice(bodyLength + FrogPackFormat.ContentHashLength + FrogPackFormat.PublicKeyLength, FrogPackFormat.SignatureLength);
        var actualHash = SHA256.HashData(body);
        if (!CryptographicOperations.FixedTimeEquals(actualHash, contentHash))
        {
            throw new FrogPackRejectedException("SHA-256 du contenu tileset invalide.");
        }

        if (!CryptographicOperations.FixedTimeEquals(embeddedKey, trustedPublicKey))
        {
            throw new FrogPackRejectedException("Clé publique Ed25519 non fiable.");
        }

        if (!FrogPackKeys.Verify(trustedPublicKey, actualHash, signature))
        {
            throw new FrogPackRejectedException("Signature Ed25519 refusée.");
        }

        var cursor = ComposedTilesetPackFormat.HeaderLength;
        var pixels = new Dictionary<TileAssetId, byte[]>();
        TileAssetId previousId = default;
        var havePreviousId = false;
        for (var i = 0; i < uniqueCount; i++)
        {
            if (!TryTake(body, ref cursor, TileAssetId.ByteLength, out var idBytes))
            {
                throw new FrogPackRejectedException("Tuile tronquée.");
            }

            if (!TryTake(body, ref cursor, TileAssetMetrics.CanonicalPixelByteCount, out var rgba))
            {
                throw new FrogPackRejectedException("Pixels de tuile tronqués.");
            }

            var id = TileAssetId.FromHashBytes(idBytes);
            if (id.IsNone)
            {
                throw new FrogPackRejectedException("TileAssetId réservé.");
            }

            if (havePreviousId && previousId.CompareTo(id) >= 0)
            {
                throw new FrogPackRejectedException("Tuiles non triées par TileAssetId.");
            }

            havePreviousId = true;
            previousId = id;
            var hashed = TileAssetId.FromNormalizedRgba(rgba);
            if (hashed != id)
            {
                throw new FrogPackRejectedException("TileAssetId incohérent avec les pixels.");
            }

            pixels.Add(id, rgba.ToArray());
        }

        var sets = new List<ComposedTilesetDefinition>((int)tilesetCount);
        Guid previousGuid = default;
        var havePreviousGuid = false;
        for (var s = 0; s < tilesetCount; s++)
        {
            if (!TryTake(body, ref cursor, 16, out var guidBytes))
            {
                throw new FrogPackRejectedException("Tileset tronqué.");
            }

            var id = new Guid(guidBytes);
            if (id == Guid.Empty)
            {
                throw new FrogPackRejectedException("Identifiant de tileset vide.");
            }

            if (havePreviousGuid && ComposedTilesetPackWriter.CompareGuid(previousGuid, id) >= 0)
            {
                throw new FrogPackRejectedException("Tilesets non triés.");
            }

            havePreviousGuid = true;
            previousGuid = id;
            if (!TryReadUInt16(body, ref cursor, out var nameLen)
                || nameLen is < 1 or > ComposedTilesetPackFormat.MaxNameUtf8Bytes
                || !TryTake(body, ref cursor, nameLen, out var nameBytes))
            {
                throw new FrogPackRejectedException("Nom de tileset illisible.");
            }

            var name = Encoding.UTF8.GetString(nameBytes);
            if (string.IsNullOrWhiteSpace(name) || name.Length > ComposedTilesetDefinition.MaxNameLength)
            {
                throw new FrogPackRejectedException("Nom de tileset invalide.");
            }

            if (!TryReadUInt32(body, ref cursor, out var memberCount) || memberCount is < 1 or > WorkingTileset.MaxTileCount)
            {
                throw new FrogPackRejectedException("Nombre de tuiles du tileset hors limite.");
            }

            var set = new ComposedTilesetDefinition
            {
                Id = id,
                Name = name,
                LogicalPath = "tiles/composed/" + id.ToString("N") + ".tileset",
            };
            for (var m = 0; m < memberCount; m++)
            {
                if (!TryTake(body, ref cursor, TileAssetId.ByteLength, out var memberIdBytes))
                {
                    throw new FrogPackRejectedException("Membre de tileset tronqué.");
                }

                var memberId = TileAssetId.FromHashBytes(memberIdBytes);
                if (!pixels.TryGetValue(memberId, out var rgba))
                {
                    throw new FrogPackRejectedException("Tuile référencée absente du corps.");
                }

                if (!TryReadUInt16(body, ref cursor, out var displayLen)
                    || displayLen > ComposedTilesetPackFormat.MaxNameUtf8Bytes
                    || !TryTake(body, ref cursor, displayLen, out var displayBytes))
                {
                    throw new FrogPackRejectedException("Nom de tuile illisible.");
                }

                var display = Encoding.UTF8.GetString(displayBytes);
                if (display.Length > ComposedTilesetDefinition.MaxDisplayNameLength)
                {
                    throw new FrogPackRejectedException("Nom de tuile trop long.");
                }

                set.Tiles.Add(new ComposedTileRef
                {
                    TileAssetId = memberId.ToHex(),
                    DisplayName = string.IsNullOrEmpty(display) ? null : display,
                    NormalizedRgba = rgba.ToArray(),
                });
            }

            if (!set.Validate(out var error))
            {
                throw new FrogPackRejectedException(error ?? "Tileset composé invalide.");
            }

            sets.Add(set);
        }

        if (cursor != bodyLength)
        {
            throw new FrogPackRejectedException("Longueur de charge tileset incohérente.");
        }

        return sets;
    }

    private static bool TryTake(ReadOnlySpan<byte> body, ref int cursor, int length, out byte[] bytes)
    {
        bytes = Array.Empty<byte>();
        if (length < 0 || cursor < 0 || body.Length - cursor < length)
        {
            return false;
        }

        bytes = body.Slice(cursor, length).ToArray();
        cursor += length;
        return true;
    }

    private static bool TryReadUInt16(ReadOnlySpan<byte> body, ref int cursor, out ushort value)
    {
        value = 0;
        if (body.Length - cursor < 2)
        {
            return false;
        }

        value = BinaryPrimitives.ReadUInt16LittleEndian(body.Slice(cursor, 2));
        cursor += 2;
        return true;
    }

    private static bool TryReadUInt32(ReadOnlySpan<byte> body, ref int cursor, out uint value)
    {
        value = 0;
        if (body.Length - cursor < 4)
        {
            return false;
        }

        value = BinaryPrimitives.ReadUInt32LittleEndian(body.Slice(cursor, 4));
        cursor += 4;
        return true;
    }
}
