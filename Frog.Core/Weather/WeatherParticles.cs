namespace Frog.Core.Weather;

/// <summary>Traits de pluie déterministes (viewport), plafond 16 — pas un pipeline particules.</summary>
public static class WeatherParticles
{
    public const int MaxStreaks = 16;

    /// <summary>Remplit des traits (x, y, longueur). Retourne le nombre écrit.</summary>
    public static int FillStreaks(
        WeatherOverlayPlan plan,
        int width,
        int height,
        int tickMs,
        Span<(int X, int Y, int Length)> dest)
    {
        var n = Math.Min(plan.ParticleCount, Math.Min(MaxStreaks, dest.Length));
        if (n <= 0 || width <= 0 || height <= 0)
        {
            return 0;
        }

        var frame = (uint)Math.Max(0, tickMs) / 16u;
        for (var i = 0; i < n; i++)
        {
            var seed = (uint)(i + 1) * 1664525u + frame * 1013904223u;
            seed ^= seed << 13;
            var x = (int)(seed % (uint)width);
            seed *= 22695477u;
            seed += 1;
            var y = (int)(seed % (uint)height);
            var length = 6 + (int)(seed % 9u);
            dest[i] = (x, y, length);
        }

        return n;
    }
}
