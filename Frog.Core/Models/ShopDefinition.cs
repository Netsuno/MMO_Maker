namespace Frog.Core.Models;

/// <summary>Définition éditable d’une boutique, sans logique d’achat ou de vente.</summary>
public sealed class ShopDefinition
{
    public const int MaxNameLength = 120;
    public const int MaxDescriptionLength = 4000;

    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public string? Description { get; set; }

    public List<ShopListing> Listings { get; set; } = new();

    public bool Validate(out string? error)
    {
        if (Id == Guid.Empty)
        {
            error = "Identifiant de boutique manquant.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Name) || Name.Length > MaxNameLength)
        {
            error = $"Nom de boutique invalide (1–{MaxNameLength} caractères).";
            return false;
        }

        if (Description?.Length > MaxDescriptionLength)
        {
            error = $"Description trop longue ({MaxDescriptionLength} caractères maximum).";
            return false;
        }

        if (Listings is null)
        {
            error = "La liste des articles de boutique est manquante.";
            return false;
        }

        var itemIds = new HashSet<Guid>();
        foreach (var listing in Listings)
        {
            if (listing is null)
            {
                error = "Un article de boutique est manquant.";
                return false;
            }

            if (!listing.Validate(out error))
            {
                return false;
            }

            if (!itemIds.Add(listing.ItemId))
            {
                error = "Un objet ne peut apparaître qu’une fois dans une boutique.";
                return false;
            }
        }

        error = null;
        return true;
    }
}

public sealed class ShopListing
{
    public Guid ItemId { get; set; }

    public int Price { get; set; }

    /// <summary>Stock disponible ; null signifie illimité.</summary>
    public int? Stock { get; set; }

    public bool Validate(out string? error)
    {
        if (ItemId == Guid.Empty)
        {
            error = "Identifiant d’objet de boutique manquant.";
            return false;
        }

        if (Price < 0)
        {
            error = "Le prix d’un article doit être positif ou nul.";
            return false;
        }

        if (Stock < 0)
        {
            error = "Le stock d’un article doit être positif, nul ou illimité.";
            return false;
        }

        error = null;
        return true;
    }
}
