using System.Buffers.Binary;
using System.Security.Cryptography;

using Frog.Core.Constants;
using Frog.Core.Maps;

namespace Frog.Core.Distribution;

/// <summary>
/// Lit un <c>.frogpack</c> V1. Échoue fermé : signature Ed25519 (clé de confiance fournie par l’appelant)
/// puis SHA-256 du corps et de chaque blob. Le chiffrement V1 est refusé. Pas de delta.
/// </summary>
public static class FrogPackReader
{
    public static IReadOnlyList<TileAsset> Read(ReadOnlySpan<byte> data, ReadOnlySpan<byte> trustedPublicKey)
    {
        if (trustedPublicKey.Length != FrogPackFormat.PublicKeyLength)
        {
            throw new ArgumentException("Clé publique Ed25519 : 32 octets.", nameof(trustedPublicKey));
        }

        if (data.Length < FrogPackFormat.HeaderLength + FrogPackFormat.TrailerLength)
        {
            throw new FrogPackRejectedException("frogpack trop court.");
        }

        if (data[0] != (byte)'F' || data[1] != (byte)'P' || data[2] != (byte)'K' || data[3] != (byte)'1')
        {
            throw new FrogPackRejectedException("Magic FPK1 attendu.");
        }

        var version = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(4, 2));
        if (version != FrogPackFormat.FormatVersion)
        {
            throw new FrogPackRejectedException($"Version frogpack non supportée : {version}.");
        }

        var flags = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(6, 2));
        if ((flags & FrogPackFormat.FlagEncrypted) != 0)
        {
            throw new FrogPackRejectedException("Chiffrement .frogpack désactivé en V1 (flags).");
        }

        if (flags != FrogPackFormat.FlagsNone)
        {
            throw new FrogPackRejectedException("Flags .frogpack V1 inconnus.");
        }

        var tileSize = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(8, 2));
        if (tileSize != TileAssetMetrics.TargetTileSizePixels)
        {
            throw new FrogPackRejectedException(
                $"tileSizePixels frogpack = {tileSize}, attendu {TileAssetMetrics.TargetTileSizePixels}. {TileSizeMigrationPolicy.NoSilentUpscale}");
        }

        var reserved = BinaryPrimitives.ReadUInt16LittleEndian(data.Slice(10, 2));
        if (reserved != 0)
        {
            throw new FrogPackRejectedException("Champ réservé frogpack non nul.");
        }

        var tileCount = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(12, 4));
        if (tileCount > FrogPackFormat.MaxTileCount)
        {
            throw new FrogPackRejectedException("Nombre de tuiles frogpack hors limite.");
        }

        var manifestBytes = (long)tileCount * FrogPackFormat.ManifestEntryLength;
        var blobBytes = (long)tileCount * TileAssetMetrics.CanonicalPixelByteCount;
        var bodyLength = FrogPackFormat.HeaderLength + manifestBytes + blobBytes;
        if (bodyLength > int.MaxValue - FrogPackFormat.TrailerLength)
        {
            throw new FrogPackRejectedException("frogpack trop grand.");
        }

        var expectedLength = (int)bodyLength + FrogPackFormat.TrailerLength;
        if (data.Length != expectedLength)
        {
            throw new FrogPackRejectedException("Longueur frogpack incohérente avec le manifeste.");
        }

        var body = data[..(int)bodyLength];
        var contentHash = data.Slice((int)bodyLength, FrogPackFormat.ContentHashLength);
        var embeddedKey = data.Slice((int)bodyLength + FrogPackFormat.ContentHashLength, FrogPackFormat.PublicKeyLength);
        var signature = data.Slice((int)bodyLength + FrogPackFormat.ContentHashLength + FrogPackFormat.PublicKeyLength, FrogPackFormat.SignatureLength);

        var actualHash = SHA256.HashData(body);
        if (!CryptographicOperations.FixedTimeEquals(actualHash, contentHash))
        {
            throw new FrogPackRejectedException("SHA-256 du contenu .frogpack invalide.");
        }

        if (!CryptographicOperations.FixedTimeEquals(embeddedKey, trustedPublicKey))
        {
            throw new FrogPackRejectedException("Clé publique Ed25519 non fiable.");
        }

        if (!FrogPackKeys.Verify(trustedPublicKey, actualHash, signature))
        {
            throw new FrogPackRejectedException("Signature Ed25519 refusée.");
        }

        var assets = new List<TileAsset>((int)tileCount);
        TileAssetId previous = default;
        var havePrevious = false;
        var blobStart = FrogPackFormat.HeaderLength + (int)manifestBytes;
        for (var i = 0; i < tileCount; i++)
        {
            var entry = FrogPackFormat.HeaderLength + (i * FrogPackFormat.ManifestEntryLength);
            var id = TileAssetId.FromHashBytes(data.Slice(entry, TileAssetId.ByteLength));
            if (id.IsNone)
            {
                throw new FrogPackRejectedException("TileAssetId.None dans le manifeste.");
            }

            if (havePrevious && previous.CompareTo(id) >= 0)
            {
                throw new FrogPackRejectedException("Manifeste frogpack non trié ou doublon.");
            }

            var offset = BinaryPrimitives.ReadUInt64LittleEndian(data.Slice(entry + FrogPackFormat.BlobOffsetField, 8));
            var size = BinaryPrimitives.ReadUInt32LittleEndian(data.Slice(entry + FrogPackFormat.BlobSizeField, 4));
            var declaredHash = data.Slice(entry + FrogPackFormat.BlobHashField, FrogPackFormat.ContentHashLength);
            var expectedOffset = (ulong)i * (ulong)TileAssetMetrics.CanonicalPixelByteCount;
            if (offset != expectedOffset || size != TileAssetMetrics.CanonicalPixelByteCount)
            {
                throw new FrogPackRejectedException("Offset ou taille de blob invalide.");
            }

            var blob = data.Slice(blobStart + (int)offset, TileAssetMetrics.CanonicalPixelByteCount);
            var blobHash = SHA256.HashData(blob);
            if (!CryptographicOperations.FixedTimeEquals(blobHash, declaredHash)
                || !id.Equals(TileAssetId.FromHashBytes(blobHash)))
            {
                throw new FrogPackRejectedException("SHA-256 de tuile invalide.");
            }

            TileAsset asset;
            try
            {
                asset = TileAsset.FromNormalizedRgba(blob, id);
            }
            catch (InvalidDataException ex)
            {
                throw new FrogPackRejectedException(ex.Message);
            }

            assets.Add(asset);
            previous = id;
            havePrevious = true;
        }

        return assets;
    }
}
