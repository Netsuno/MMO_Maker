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

    /// <summary>Rôle court dans la liste. N’est pas un nom stocké : le format garde <see cref="LayerType"/>.</summary>
    public static string Role(LayerType type) => type switch
    {
        LayerType.Ground => "sol, dessiné en premier",
        LayerType.Mask => "décor au-dessus du sol",
        LayerType.Mask2 => "second décor",
        LayerType.Fringe => "devant le personnage",
        LayerType.Fringe2 => "devant, encore plus haut",
        LayerType.Attributes => "collisions et warps",
        _ => type.ToString(),
    };

    /// <summary>
    /// Rang dans la pile. L’index 0 est dessiné en premier (dessous) ; le dernier index passe par-dessus.
    /// </summary>
    public static string StackRank(int index, int count)
    {
        if (count <= 0 || index < 0 || index >= count)
        {
            return string.Empty;
        }

        var n = index + 1;
        if (count == 1)
        {
            return "1 · seule";
        }

        if (index == 0)
        {
            return $"{n} · dessous";
        }

        if (index == count - 1)
        {
            return $"{n} · dessus";
        }

        return n.ToString();
    }

    /// <summary>Indices à afficher du haut de la liste vers le bas : le dernier index (par-dessus) d’abord.</summary>
    public static int[] TopFirstIndices(int count)
    {
        if (count < 0)
        {
            throw new ArgumentOutOfRangeException(nameof(count));
        }

        var indices = new int[count];
        for (var visual = 0; visual < count; visual++)
        {
            indices[visual] = count - 1 - visual;
        }

        return indices;
    }

    /// <summary>Sous-titre de ligne : rang, type moteur si le nom affiché est personnalisé, puis rôle.</summary>
    public static string OrderHint(int index, int count, LayerType type, string? displayName)
    {
        var rank = StackRank(index, count);
        var role = Role(type);
        var french = French(type);
        var custom = !string.IsNullOrWhiteSpace(displayName)
            && !string.Equals(displayName.Trim(), french, StringComparison.Ordinal);
        if (string.IsNullOrEmpty(rank))
        {
            return custom ? $"{french} · {role}" : role;
        }

        return custom ? $"{rank} · {french} · {role}" : $"{rank} · {role}";
    }

    public static string LockCaption(bool locked) => locked ? "Verrouillée" : "Éditable";

    public static string LockHint(bool locked) =>
        locked
            ? "Verrouillée — cliquer pour autoriser la peinture"
            : "Éditable — cliquer pour verrouiller";

    public const string PanelTitle = "COUCHES";

    public const string PanelHint = "Dessus en haut · la couche marquée est peinte (1 = dessous)";

    public const string DimOthersCaption = "Atténuer les autres";

    public const string DimOthersHint =
        "Les couches qui ne sont pas peintes s’affichent plus pâles. Le fichier .fmap ne change pas.";

    public const string OpacityColumn = "Opacité";

    public const string OpacityHint = "Aperçu dans l’éditeur. N’est pas enregistré dans la carte.";

    public const string PaintBadge = "peinture";

    public const string ViewColumn = "Vue";

    public const string NameColumn = "Nom";

    public const string EditColumn = "Édition";

    /// <summary>Bouton numéroté du bandeau. L’index 0 (dessous) porte le numéro 1, comme un calque VX.</summary>
    public static string StripCaption(int index, string? displayLabel)
    {
        if (index < 0)
        {
            return string.Empty;
        }

        var name = string.IsNullOrWhiteSpace(displayLabel) ? "Couche" : displayLabel.Trim();
        return $"{index + 1} {name}";
    }

    public static string StripHint(int index, int count, string? displayLabel, bool locked)
    {
        var name = string.IsNullOrWhiteSpace(displayLabel) ? "Couche" : displayLabel.Trim();
        var rank = StackRank(index, count);
        var edit = LockHint(locked);
        return string.IsNullOrEmpty(rank) ? $"{name}. {edit}" : $"{name} · {rank}. {edit}";
    }

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
