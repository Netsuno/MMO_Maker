namespace Frog.Core.Enums;

/// <summary>Libellés français des couches (éditeur et messages de validation). Le format de carte stocke l’enum, pas ce texte.</summary>
public static class LayerTypeLabels
{
    public static string French(LayerType type) => type switch
    {
        LayerType.Ground => "Sol",
        LayerType.Mask => "Masque",
        LayerType.Mask2 => "Masque 2",
        LayerType.Fringe => "Frange",
        LayerType.Fringe2 => "Frange 2",
        LayerType.Attributes => "Attributs",
        _ => type.ToString(),
    };

    public static bool TryParse(string? text, out LayerType type)
    {
        type = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();
        if (Enum.TryParse(trimmed, ignoreCase: true, out type) && Enum.IsDefined(type))
        {
            return true;
        }

        foreach (var candidate in Enum.GetValues<LayerType>())
        {
            if (string.Equals(French(candidate), trimmed, StringComparison.OrdinalIgnoreCase))
            {
                type = candidate;
                return true;
            }
        }

        type = default;
        return false;
    }
}
