using Frog.Core.Models;

namespace Frog.Core.Weather;

/// <summary>
/// Résout un <see cref="WeatherOverlayPlan"/> depuis un profil Phase 8, l'état publié, ou le toggle debug.
/// </summary>
public static class WeatherResolver
{
    public static WeatherDebugOverride Cycle(WeatherDebugOverride current) =>
        (WeatherDebugOverride)(((int)current + 1) % 4);

    public static string KindIdFor(WeatherDebugOverride debug) => debug switch
    {
        WeatherDebugOverride.Rain => WeatherKindId.Rain,
        WeatherDebugOverride.Fog => WeatherKindId.Fog,
        WeatherDebugOverride.Clear => WeatherKindId.Clear,
        _ => WeatherKindId.Clear,
    };

    /// <summary>Profil éditeur / catalogue publié (tests + serveur).</summary>
    public static WeatherOverlayPlan Resolve(
        WeatherProfileDefinition profile,
        WeatherDebugOverride debug = WeatherDebugOverride.Auto)
    {
        ArgumentNullException.ThrowIfNull(profile);
        var lighting = (byte)Math.Clamp((int)(profile.LightingFactor * 255f), 0, 255);
        return Resolve(profile.Id, lighting, profile.WeatherKind, debug);
    }

    /// <summary>
    /// Chemin client : <c>EnvironmentStatePush</c> (profil + éclairage) + kind additif optionnel + F8.
    /// </summary>
    public static WeatherOverlayPlan Resolve(
        Guid? weatherProfileId,
        byte lightingLevel,
        string? publishedKind,
        WeatherDebugOverride debug = WeatherDebugOverride.Auto)
    {
        string kind;
        if (debug != WeatherDebugOverride.Auto)
        {
            kind = KindIdFor(debug);
        }
        else if (!string.IsNullOrWhiteSpace(publishedKind))
        {
            kind = WeatherCatalog.NormalizeKind(publishedKind);
        }
        else if (weatherProfileId is Guid id && WeatherCatalog.TryGetKindForProfile(id, out var mapped))
        {
            kind = mapped;
        }
        else
        {
            kind = WeatherKindId.Clear;
        }

        return ApplyLighting(WeatherCatalog.ForKind(kind), lightingLevel);
    }

    /// <summary>
    /// Override événement de session : un kind connu remplace le kind publié
    /// (éclairage et profil région inchangés). Un kind inconnu est ignoré.
    /// </summary>
    public static WeatherSnapshot ApplySessionOverride(WeatherSnapshot snapshot, string? sessionOverride)
    {
        ArgumentNullException.ThrowIfNull(snapshot);
        if (!WeatherKindId.TryCanonical(sessionOverride, out var canonical)
            || string.Equals(snapshot.WeatherKind, canonical, StringComparison.Ordinal))
        {
            return snapshot;
        }

        return new WeatherSnapshot
        {
            WeatherKind = canonical,
            LightingFactor = snapshot.LightingFactor,
            RegionId = snapshot.RegionId,
            WeatherProfileId = snapshot.WeatherProfileId,
        };
    }

    /// <summary>Assombrit la teinte quand l'éclairage publié est bas (0–255).</summary>
    public static WeatherOverlayPlan ApplyLighting(WeatherOverlayPlan plan, byte lightingLevel)
    {
        var darkness = 255 - lightingLevel;
        if (darkness <= 0)
        {
            return plan;
        }

        var extra = Math.Min(90, darkness * 90 / 255);
        var argb = (uint)plan.TintArgb;
        var a = Math.Min(180, (int)(argb >> 24) + extra);
        var r = (int)((argb >> 16) & 0xFF);
        var g = (int)((argb >> 8) & 0xFF);
        var b = (int)(argb & 0xFF);
        if (a > 0 && r == 0 && g == 0 && b == 0)
        {
            r = 12;
            g = 12;
            b = 18;
        }

        var tint = (int)((uint)(a << 24) | (uint)(r << 16) | (uint)(g << 8) | (uint)b);
        return plan with { TintArgb = tint };
    }
}
