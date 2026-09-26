namespace Frog.Core.Weather;

/// <summary>Identifiants météo du catalogue MVP (clear / rain / fog).</summary>
public static class WeatherKindId
{
    public const string Clear = "clear";

    public const string Rain = "rain";

    public const string Fog = "fog";

    public static readonly IReadOnlyList<string> All = [Clear, Rain, Fog];

    /// <summary>
    /// Accepte uniquement les ids canoniques (casse ignorée). Les alias catalogue
    /// (<c>pluie</c>, <c>storm</c>…) ne sont pas des kinds de commande.
    /// </summary>
    public static bool TryCanonical(string? kind, out string canonical)
    {
        canonical = string.Empty;
        if (string.IsNullOrWhiteSpace(kind))
        {
            return false;
        }

        var key = kind.Trim();
        if (string.Equals(key, Clear, StringComparison.OrdinalIgnoreCase))
        {
            canonical = Clear;
            return true;
        }

        if (string.Equals(key, Rain, StringComparison.OrdinalIgnoreCase))
        {
            canonical = Rain;
            return true;
        }

        if (string.Equals(key, Fog, StringComparison.OrdinalIgnoreCase))
        {
            canonical = Fog;
            return true;
        }

        return false;
    }
}
