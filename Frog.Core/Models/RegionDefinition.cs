namespace Frog.Core.Models;

/// <summary>Région carte + profil météo (Phase 8 — P8-5).</summary>
public sealed class RegionDefinition
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int MapId { get; set; }

    public int TileXMin { get; set; }

    public int TileYMin { get; set; }

    public int TileXMax { get; set; }

    public int TileYMax { get; set; }

    public Guid WeatherProfileId { get; set; }

    public bool ContainsTile(int tileX, int tileY) =>
        tileX >= TileXMin && tileX <= TileXMax && tileY >= TileYMin && tileY <= TileYMax;

    public bool Validate(out string? error)
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            error = "Nom de région requis.";
            return false;
        }

        if (WeatherProfileId == Guid.Empty)
        {
            error = "WeatherProfileId requis.";
            return false;
        }

        if (TileXMax < TileXMin || TileYMax < TileYMin)
        {
            error = "Bornes de région invalides.";
            return false;
        }

        error = null;
        return true;
    }
}

public sealed class WeatherProfileDefinition
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    /// <summary>Identifiant météo (clear, rain, snow…).</summary>
    public string WeatherKind { get; set; } = "clear";

    /// <summary>Facteur luminosité 0.0–1.0.</summary>
    public float LightingFactor { get; set; } = 1.0f;

    public bool Validate(out string? error)
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            error = "Nom de profil météo requis.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(WeatherKind))
        {
            error = "WeatherKind requis.";
            return false;
        }

        if (LightingFactor is < 0f or > 1f)
        {
            error = "LightingFactor hors bornes.";
            return false;
        }

        error = null;
        return true;
    }
}

public sealed class WeatherSnapshot
{
    public string WeatherKind { get; init; } = "clear";

    public float LightingFactor { get; init; } = 1.0f;

    public Guid? RegionId { get; init; }
}
