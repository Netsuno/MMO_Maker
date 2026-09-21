using Frog.Application.Content;
using Frog.Core.Models;

namespace Frog.Application.Assets;

/// <summary>
/// Publie les tilesets utilisés par une carte (palette = <see cref="Frog.Core.Models.Tile.TilesetId"/>)
/// avec le PNG, pour que le catalogue serveur puisse envoyer <c>pngBase64</c>.
/// </summary>
public static class MapPublishedTilesetSync
{
    public static TilesetDefinition BuildDefinition(MapTilesetFile file, TilesetDefinition? existing = null)
    {
        ArgumentNullException.ThrowIfNull(file);
        if (file.Id <= 0 || file.PngBytes.Length == 0)
        {
            throw new ArgumentException("Tileset PNG invalide.", nameof(file));
        }

        var sha = TilesetDefinition.ComputeSha256Hex(file.PngBytes);
        PngImageHeader.TryRead(file.PngBytes, out var width, out var height);
        var tileSize = existing is { TileSizePixels: >= TilesetDefinition.MinTileSizePixels } t
            ? t.TileSizePixels
            : 32;
        if (width > 0 && width % tileSize != 0)
        {
            tileSize = GreatestCommonDivisor(width, height > 0 ? height : width);
            if (tileSize < TilesetDefinition.MinTileSizePixels)
            {
                tileSize = TilesetDefinition.MinTileSizePixels;
            }
        }

        return new TilesetDefinition
        {
            Id = existing?.Id is { } id && id != Guid.Empty ? id : Guid.NewGuid(),
            Name = existing is { Name: { Length: > 0 } } named
                ? named.Name
                : $"Tileset {file.Id}",
            LogicalPath = existing is { LogicalPath: { Length: > 0 } } path
                ? path.LogicalPath
                : $"tiles/palette-{file.Id}-{Guid.NewGuid():N}.png",
            TileSizePixels = tileSize,
            WidthPixels = width > 0 ? width : existing?.WidthPixels ?? tileSize,
            HeightPixels = height > 0 ? height : existing?.HeightPixels ?? tileSize,
            Sha256Hex = sha,
            EditorPaletteId = file.Id,
            PngBytes = file.PngBytes,
        };
    }

    public static async Task<IReadOnlyList<Guid>> PublishUsedAsync(
        ITilesetRepository repository,
        Map map,
        IReadOnlyList<MapTilesetFile> pngFiles,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(repository);
        ArgumentNullException.ThrowIfNull(map);
        ArgumentNullException.ThrowIfNull(pngFiles);

        var used = new HashSet<int>(MapTilesetPackage.CollectUsedTilesetIds(map));
        if (used.Count == 0)
        {
            return Array.Empty<Guid>();
        }

        var published = repository is IPublishedTilesetCatalog catalog
            ? await catalog.ListPublishedAsync(cancellationToken).ConfigureAwait(false)
            : Array.Empty<TilesetDefinition>();
        var summaries = await repository.ListSummariesAsync(cancellationToken: cancellationToken)
            .ConfigureAwait(false);

        var shaToPalette = TilesetPaletteAlignment.BuildShaToPalette(pngFiles);
        var byPalette = new Dictionary<int, TilesetCatalogEntry>();
        foreach (var row in summaries.Where(s => s.EditorPaletteId is > 0))
        {
            byPalette[row.EditorPaletteId!.Value] = row;
        }

        var publishedById = published.ToDictionary(p => p.Id);
        var publishedBySha = published
            .Where(p => !string.IsNullOrWhiteSpace(p.Sha256Hex))
            .GroupBy(p => p.Sha256Hex.ToUpperInvariant())
            .ToDictionary(g => g.Key, g => g.First());

        var ids = new List<Guid>();
        foreach (var file in pngFiles.Where(f => f.Id > 0 && f.PngBytes.Length > 0 && used.Contains(f.Id)))
        {
            TilesetDefinition? existing = null;
            Guid? tilesetId = null;
            long expectedRevision = 0;

            if (byPalette.TryGetValue(file.Id, out var summary))
            {
                tilesetId = summary.TilesetId;
                expectedRevision = summary.Revision;
                var stored = await repository.LoadByIdAsync(summary.TilesetId, cancellationToken)
                    .ConfigureAwait(false);
                existing = stored?.Definition;
            }
            else
            {
                var sha = TilesetDefinition.ComputeSha256Hex(file.PngBytes);
                if (publishedBySha.TryGetValue(sha.ToUpperInvariant(), out var bySha))
                {
                    tilesetId = bySha.Id;
                    var stored = await repository.LoadByIdAsync(bySha.Id, cancellationToken)
                        .ConfigureAwait(false);
                    existing = stored?.Definition ?? bySha;
                    expectedRevision = stored?.Revision ?? 0;
                }
                else
                {
                    foreach (var def in published)
                    {
                        var aligned = TilesetPaletteAlignment.ResolveClientTilesetId(def, used, shaToPalette);
                        if (aligned == file.Id)
                        {
                            tilesetId = def.Id;
                            var stored = await repository.LoadByIdAsync(def.Id, cancellationToken)
                                .ConfigureAwait(false);
                            existing = stored?.Definition ?? def;
                            expectedRevision = stored?.Revision ?? 0;
                            break;
                        }
                    }
                }
            }

            var definition = BuildDefinition(file, existing);
            if (tilesetId is Guid known && known != Guid.Empty)
            {
                definition.Id = known;
            }

            if (!definition.Validate(out _))
            {
                continue;
            }

            var result = await repository.SaveAsync(
                    new SaveTilesetRequest
                    {
                        TilesetId = tilesetId,
                        Definition = definition,
                        ExpectedRevision = expectedRevision,
                        Intent = SaveContentIntent.Publish,
                    },
                    cancellationToken)
                .ConfigureAwait(false);

            if (result is SaveTilesetResult.Success success)
            {
                ids.Add(success.TilesetId);
                publishedById[success.TilesetId] = definition;
            }
        }

        return ids;
    }

    private static int GreatestCommonDivisor(int a, int b)
    {
        a = Math.Abs(a);
        b = Math.Abs(b);
        while (b != 0)
        {
            var t = a % b;
            a = b;
            b = t;
        }

        return a;
    }
}
