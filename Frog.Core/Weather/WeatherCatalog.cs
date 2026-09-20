namespace Frog.Core.Weather;

/// <summary>
/// Catalogue local clear / rain / fog + profils Phase 8 / démo déjà publiés.
/// Les profils éditeur inconnus tombent sur le <c>WeatherKind</c> publié (si présent) sinon clair.
/// </summary>
public static class WeatherCatalog
{
    /// <summary>Village démo P10 — <c>Phase10DemoWorldCatalog.VillageWeatherId</c>.</summary>
    public static readonly Guid DemoVillageWeatherId = Guid.Parse("cccccccc-000b-4000-8000-000000000001");

    /// <summary>Faubourgs démo P10 — <c>Phase10DemoWorldCatalog.WildsWeatherId</c>.</summary>
    public static readonly Guid DemoWildsWeatherId = Guid.Parse("cccccccc-000b-4000-8000-000000000002");

    /// <summary>Seed Postgres Phase 8 (pluie).</summary>
    public static readonly Guid Phase8RainWeatherId = Guid.Parse("bbbbbbbb-0006-4000-8000-000000000001");

    /// <summary>Seed Postgres Phase 8 (clair).</summary>
    public static readonly Guid Phase8ClearWeatherId = Guid.Parse("bbbbbbbb-0006-4000-8000-000000000002");

    /// <summary>Bootstrap smoke Phase 8 (clair, lighting 0.7).</summary>
    public static readonly Guid Phase8SmokeWeatherId = Guid.Parse("aaaaaaaa-0006-4000-8000-000000000001");

    public static WeatherOverlayPlan Clear { get; } = new(
        WeatherKindId.Clear,
        "Clair",
        TintArgb: 0,
        ParticleCount: 0,
        WantsAmbience: false);

    public static WeatherOverlayPlan Rain { get; } = new(
        WeatherKindId.Rain,
        "Pluie",
        TintArgb: unchecked((int)0x55202A40),
        ParticleCount: 12,
        WantsAmbience: true);

    public static WeatherOverlayPlan Fog { get; } = new(
        WeatherKindId.Fog,
        "Brouillard",
        TintArgb: unchecked((int)0x6E8A9098),
        ParticleCount: 0,
        WantsAmbience: true);

    public static WeatherOverlayPlan ForKind(string? kind)
    {
        return NormalizeKind(kind) switch
        {
            WeatherKindId.Rain => Rain,
            WeatherKindId.Fog => Fog,
            _ => Clear,
        };
    }

    public static string NormalizeKind(string? kind)
    {
        if (string.IsNullOrWhiteSpace(kind))
        {
            return WeatherKindId.Clear;
        }

        var key = kind.Trim().ToLowerInvariant();
        return key switch
        {
            "rain" or "rainy" or "storm" or "pluie" => WeatherKindId.Rain,
            "fog" or "foggy" or "mist" or "brouillard" => WeatherKindId.Fog,
            "clear" or "sunny" or "clair" => WeatherKindId.Clear,
            _ => WeatherKindId.Clear,
        };
    }

    public static bool TryGetKindForProfile(Guid profileId, out string kind)
    {
        if (profileId == DemoWildsWeatherId || profileId == Phase8RainWeatherId)
        {
            kind = WeatherKindId.Rain;
            return true;
        }

        if (profileId == DemoVillageWeatherId
            || profileId == Phase8ClearWeatherId
            || profileId == Phase8SmokeWeatherId)
        {
            kind = WeatherKindId.Clear;
            return true;
        }

        kind = WeatherKindId.Clear;
        return false;
    }
}
