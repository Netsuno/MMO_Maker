namespace Frog.Core.Models;

/// <summary>Métier publié (Phase 8 — P8-4).</summary>
public sealed class ProfessionDefinition
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int MaxLevel { get; set; } = 100;

    public bool Validate(out string? error)
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            error = "Nom de métier requis.";
            return false;
        }

        if (MaxLevel < 1 || MaxLevel > 999)
        {
            error = "MaxLevel invalide.";
            return false;
        }

        error = null;
        return true;
    }
}

public sealed class RecipeDefinition
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public Guid ProfessionId { get; set; }

    public int RequiredProfessionLevel { get; set; } = 1;

    public Guid OutputItemId { get; set; }

    public int OutputQuantity { get; set; } = 1;

    /// <summary>Coût en or déduit au craft (0 = gratuit).</summary>
    public int GoldCost { get; set; }

    /// <summary>XP métier accordée au craft réussi.</summary>
    public long ProfessionExperienceReward { get; set; } = 10;

    public IReadOnlyList<RecipeIngredientDefinition> Ingredients { get; set; } = Array.Empty<RecipeIngredientDefinition>();

    public bool Validate(out string? error)
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            error = "Nom de recette requis.";
            return false;
        }

        if (ProfessionId == Guid.Empty || OutputItemId == Guid.Empty)
        {
            error = "ProfessionId et OutputItemId requis.";
            return false;
        }

        if (OutputQuantity <= 0)
        {
            error = "OutputQuantity doit être > 0.";
            return false;
        }

        if (GoldCost < 0)
        {
            error = "GoldCost doit être >= 0.";
            return false;
        }

        if (ProfessionExperienceReward < 0)
        {
            error = "ProfessionExperienceReward doit être >= 0.";
            return false;
        }

        if (Ingredients.Count == 0)
        {
            error = "Au moins un ingrédient requis.";
            return false;
        }

        foreach (var ing in Ingredients)
        {
            if (!ing.Validate(out error))
            {
                return false;
            }
        }

        error = null;
        return true;
    }
}

public sealed class RecipeIngredientDefinition
{
    public Guid ItemId { get; set; }

    public int Quantity { get; set; } = 1;

    public bool Validate(out string? error)
    {
        if (ItemId == Guid.Empty || Quantity <= 0)
        {
            error = "Ingrédient invalide.";
            return false;
        }

        error = null;
        return true;
    }
}

public sealed class CharacterProfessionProgress
{
    public Guid CharacterId { get; set; }

    public Guid ProfessionId { get; set; }

    public int Level { get; set; }

    public long Experience { get; set; }
}
