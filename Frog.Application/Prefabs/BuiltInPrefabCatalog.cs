using Frog.Core.Models;

namespace Frog.Application.Prefabs;

/// <summary>Catalogue MVP in-repo : canapé 4 facings + kit clôture (poteau / barreau).</summary>
public static class BuiltInPrefabCatalog
{
    public const int CatalogVersion = 1;

    public const string SofaId = "sofa";
    public const string FencePostId = "fence-post";
    public const string FenceRailId = "fence-rail";

    public static PrefabCatalog Create()
    {
        return new PrefabCatalog
        {
            CatalogVersion = CatalogVersion,
            Prefabs =
            {
                new PrefabDefinition
                {
                    Id = SofaId,
                    DisplayName = "Canapé",
                    FootprintWidthTiles = 2,
                    FootprintHeightTiles = 1,
                    WidthPixels = 64,
                    HeightPixels = 32,
                    Variants =
                    {
                        Variant(PrefabFacing.South, "sofa-south.png", 2, 1, 64, 32),
                        Variant(PrefabFacing.North, "sofa-north.png", 2, 1, 64, 32),
                        Variant(PrefabFacing.East, "sofa-east.png", 1, 2, 32, 64),
                        Variant(PrefabFacing.West, "sofa-west.png", 1, 2, 32, 64),
                    },
                },
                new PrefabDefinition
                {
                    Id = FencePostId,
                    DisplayName = "Poteau de clôture",
                    FootprintWidthTiles = 1,
                    FootprintHeightTiles = 1,
                    WidthPixels = 32,
                    HeightPixels = 32,
                    Variants = { Variant(PrefabFacing.South, "fence-post.png", 1, 1, 32, 32) },
                },
                new PrefabDefinition
                {
                    Id = FenceRailId,
                    DisplayName = "Barreau de clôture",
                    FootprintWidthTiles = 1,
                    FootprintHeightTiles = 1,
                    WidthPixels = 32,
                    HeightPixels = 32,
                    Variants =
                    {
                        Variant(PrefabFacing.South, "fence-h.png", 1, 1, 32, 32),
                        Variant(PrefabFacing.North, "fence-h.png", 1, 1, 32, 32),
                        Variant(PrefabFacing.East, "fence-v.png", 1, 1, 32, 32),
                        Variant(PrefabFacing.West, "fence-v.png", 1, 1, 32, 32),
                    },
                },
            },
        };
    }

    private static PrefabFacingVariant Variant(
        PrefabFacing facing,
        string fileName,
        int wTiles,
        int hTiles,
        int wPx,
        int hPx)
        => new()
        {
            Facing = facing,
            SpriteFileName = fileName,
            FootprintWidthTiles = wTiles,
            FootprintHeightTiles = hTiles,
            WidthPixels = wPx,
            HeightPixels = hPx,
        };
}
