using Frog.Core.Constants;

namespace Frog.Core.Maps;

/// <summary>
/// Tuile unique 48×48. Les octets stockés sont le buffer normalisé (RGBA prémultiplié) dont le SHA-256 est <see cref="Id"/>.
/// </summary>
public sealed class TileAsset
{
    private TileAsset(TileAssetId id, byte[] normalizedRgba)
    {
        Id = id;
        NormalizedRgba = normalizedRgba;
    }

    public TileAssetId Id { get; }

    public int WidthPixels => TileAssetMetrics.TargetTileSizePixels;

    public int HeightPixels => TileAssetMetrics.TargetTileSizePixels;

    /// <summary>9216 octets RGBA8 row-major prémultipliés. Ne pas réinterpréter comme de l’alpha droit.</summary>
    public byte[] NormalizedRgba { get; }

    public static TileAsset FromStraightRgba(ReadOnlySpan<byte> straightRgba)
    {
        var normalized = TilePixelNormalizer.PremultiplyRgba(straightRgba);
        var id = TileAssetId.FromNormalizedRgba(normalized);
        if (id.IsNone)
        {
            throw new InvalidDataException("Hash de tuile tout à zéro réservé (TileAssetId.None).");
        }

        return new TileAsset(id, normalized);
    }

    /// <summary>Reconstruit une tuile dont les pixels sont déjà normalisés. Échoue si le hash ne colle pas à <paramref name="expectedId"/>.</summary>
    public static TileAsset FromNormalizedRgba(ReadOnlySpan<byte> normalizedRgba, TileAssetId expectedId)
    {
        var id = TileAssetId.FromNormalizedRgba(normalizedRgba);
        if (id.IsNone || expectedId.IsNone || id != expectedId)
        {
            throw new InvalidDataException("TileAssetId ne correspond pas aux pixels normalisés.");
        }

        var copy = normalizedRgba.ToArray();
        return new TileAsset(id, copy);
    }
}
