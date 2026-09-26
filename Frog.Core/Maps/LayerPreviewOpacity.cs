namespace Frog.Core.Maps;

/// <summary>
/// Aperçu d’opacité des couches dans l’éditeur.
/// La couche peinte reste nette ; les autres peuvent être atténuées, comme les calques d’un éditeur VX.
/// Ce n’est pas un champ de couche : le .fmap (v5 et v6) et le protocole Hello ne le portent pas.
/// </summary>
public static class LayerPreviewOpacity
{
    public const float Opaque = 1f;

    /// <summary>Voile des couches qui ne sont pas la cible de peinture, quand « Atténuer les autres » est coché.</summary>
    public const float DimOthersFactor = 0.4f;

    public static float Clamp(float opacity)
    {
        if (float.IsNaN(opacity) || opacity < 0f)
        {
            return 0f;
        }

        if (opacity > Opaque)
        {
            return Opaque;
        }

        return opacity;
    }

    public static int ToPercent(float opacity)
    {
        var clamped = Clamp(opacity);
        return (int)MathF.Round(clamped * 100f, MidpointRounding.AwayFromZero);
    }

    public static float FromPercent(int percent)
    {
        if (percent <= 0)
        {
            return 0f;
        }

        if (percent >= 100)
        {
            return Opaque;
        }

        return percent / 100f;
    }

    public static string PercentCaption(int percent) => $"{Math.Clamp(percent, 0, 100)} %";

    /// <summary>
    /// Alpha de dessin. Une couche masquée vaut 0.
    /// <paramref name="previewOpacity"/> est l’aperçu de cette couche.
    /// Si <paramref name="dimOthers"/> est vrai, les couches qui ne sont pas peintes sont multipliées par <see cref="DimOthersFactor"/>.
    /// </summary>
    public static float DrawAlpha(bool visible, bool paintTarget, float previewOpacity, bool dimOthers)
    {
        if (!visible)
        {
            return 0f;
        }

        var alpha = Clamp(previewOpacity);
        if (dimOthers && !paintTarget)
        {
            alpha *= DimOthersFactor;
        }

        return Clamp(alpha);
    }
}
