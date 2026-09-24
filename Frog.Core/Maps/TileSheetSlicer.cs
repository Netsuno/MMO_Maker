using Frog.Core.Constants;

namespace Frog.Core.Maps;

/// <summary>
/// Découpe une feuille RGBA droite en grille <c>floor(W / tuile) × floor(H / tuile)</c>.
/// V1 n’émet des <see cref="TileAsset"/> que pour des cellules 48×48 (défaut). Le reliquat de pixels
/// (bord droit / bas) est ignoré, pas étiré. Les doublons partagent un seul <see cref="TileAssetId"/>.
/// </summary>
public static class TileSheetSlicer
{
    public static TileSheetSlice Slice(
        ReadOnlySpan<byte> straightRgba,
        int imageWidth,
        int imageHeight,
        int tileWidth = TileAssetMetrics.TargetTileSizePixels,
        int tileHeight = TileAssetMetrics.TargetTileSizePixels)
    {
        if (imageWidth <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(imageWidth));
        }

        if (imageHeight <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(imageHeight));
        }

        if (tileWidth != TileAssetMetrics.TargetTileSizePixels || tileHeight != TileAssetMetrics.TargetTileSizePixels)
        {
            throw new ArgumentOutOfRangeException(
                nameof(tileWidth),
                TileSizeMigrationPolicy.NoSilentUpscale);
        }

        var expectedLength = (long)imageWidth * imageHeight * TileAssetMetrics.BytesPerPixel;
        if (expectedLength > int.MaxValue || straightRgba.Length != expectedLength)
        {
            throw new ArgumentException("Le buffer RGBA ne correspond pas à largeur × hauteur × 4.", nameof(straightRgba));
        }

        var columns = TileAssetMetrics.GridColumns(imageWidth, tileWidth);
        var rows = TileAssetMetrics.GridRows(imageHeight, tileHeight);
        if (columns <= 0 || rows <= 0)
        {
            throw new ArgumentException("L’image est plus petite qu’une tuile 48×48.");
        }

        var cellCount = checked(columns * rows);
        var cellIds = new TileAssetId[cellCount];
        var unique = new List<TileAsset>();
        var indexById = new Dictionary<TileAssetId, int>();
        var tileByteCount = tileWidth * tileHeight * TileAssetMetrics.BytesPerPixel;
        var cell = new byte[tileByteCount];

        for (var row = 0; row < rows; row++)
        {
            for (var column = 0; column < columns; column++)
            {
                CopyCell(straightRgba, imageWidth, column * tileWidth, row * tileHeight, tileWidth, tileHeight, cell);
                var asset = TileAsset.FromStraightRgba(cell);
                if (indexById.TryGetValue(asset.Id, out var existingIndex))
                {
                    if (!unique[existingIndex].NormalizedRgba.AsSpan().SequenceEqual(asset.NormalizedRgba))
                    {
                        throw new InvalidDataException("Collision TileAssetId pendant la découpe.");
                    }
                }
                else
                {
                    indexById.Add(asset.Id, unique.Count);
                    unique.Add(asset);
                }

                cellIds[(row * columns) + column] = asset.Id;
            }
        }

        return new TileSheetSlice(
            columns,
            rows,
            tileWidth,
            tileHeight,
            imageWidth % tileWidth,
            imageHeight % tileHeight,
            unique,
            cellIds);
    }

    private static void CopyCell(
        ReadOnlySpan<byte> source,
        int imageWidth,
        int originX,
        int originY,
        int tileWidth,
        int tileHeight,
        byte[] destination)
    {
        var rowBytes = tileWidth * TileAssetMetrics.BytesPerPixel;
        for (var y = 0; y < tileHeight; y++)
        {
            var src = (((originY + y) * imageWidth) + originX) * TileAssetMetrics.BytesPerPixel;
            source.Slice(src, rowBytes).CopyTo(destination.AsSpan(y * rowBytes, rowBytes));
        }
    }
}

/// <summary>Résultat de <see cref="TileSheetSlicer.Slice"/>. <see cref="CellIds"/> est row-major ; <see cref="UniqueAssets"/> suit la première occurrence.</summary>
public sealed class TileSheetSlice
{
    internal TileSheetSlice(
        int columns,
        int rows,
        int tileWidth,
        int tileHeight,
        int discardedRightPixels,
        int discardedBottomPixels,
        IReadOnlyList<TileAsset> uniqueAssets,
        IReadOnlyList<TileAssetId> cellIds)
    {
        Columns = columns;
        Rows = rows;
        TileWidth = tileWidth;
        TileHeight = tileHeight;
        DiscardedRightPixels = discardedRightPixels;
        DiscardedBottomPixels = discardedBottomPixels;
        UniqueAssets = uniqueAssets;
        CellIds = cellIds;
    }

    public int Columns { get; }

    public int Rows { get; }

    public int TileWidth { get; }

    public int TileHeight { get; }

    public int DiscardedRightPixels { get; }

    public int DiscardedBottomPixels { get; }

    public IReadOnlyList<TileAsset> UniqueAssets { get; }

    public IReadOnlyList<TileAssetId> CellIds { get; }
}
