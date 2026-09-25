using System.Globalization;
using System.Text;

namespace Frog.Core.Shop;

/// <summary>Libellés joueur (FR) pour les réponses boutique et banque. Le fil (51–59) ne change pas.</summary>
public static class ShopBankPlayerMessages
{
    public static string Present(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return string.Empty;
        }

        var trimmed = raw.Trim();
        if (Map.TryGetValue(Key(trimmed), out var french))
        {
            return french;
        }

        return trimmed;
    }

    public static string FormatListing(ShopListingView listing)
    {
        var price = listing.PriceKnown ? listing.Price.ToString(CultureInfo.InvariantCulture) + " or" : "prix inconnu";
        var stock = !listing.StockKnown
            ? "stock inconnu"
            : listing.Unlimited
                ? "stock illimité"
                : listing.Stock <= 0
                    ? "rupture"
                    : "stock " + listing.Stock.ToString(CultureInfo.InvariantCulture);
        return listing.Name + " — " + price + " — " + stock;
    }

    private static string Key(string text)
    {
        var end = text.Trim().TrimEnd('.').ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(end.Length);
        foreach (var ch in end)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(ch) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            sb.Append(ch);
        }

        return sb.ToString().Normalize(NormalizationForm.FormC);
    }

    private static readonly Dictionary<string, string> Map = new(StringComparer.Ordinal)
    {
        ["achat reussi"] = "Achat réussi.",
        ["vente reussie"] = "Vente réussie.",
        ["depose en banque"] = "Déposé en banque.",
        ["retire de la banque"] = "Retiré de la banque.",
        ["operation reussie"] = "Opération réussie.",
        ["quantite invalide"] = "Quantité invalide.",
        ["montant invalide"] = "Montant invalide.",
        ["parametres invalides"] = "Paramètres invalides.",
        ["requestid requis"] = "Demande invalide.",
        ["requestid reutilise avec payload different"] = "Cette demande a déjà servi pour une autre opération.",
        ["conflit de requete concurrente, veuillez reessayer"] = "Conflit, réessayez.",
        ["or insuffisant"] = "Or insuffisant.",
        ["or banque insuffisant"] = "Or en banque insuffisant.",
        ["or reserve pour un echange"] = "Or réservé pour un échange.",
        ["stock insuffisant"] = "Stock insuffisant.",
        ["inventaire plein"] = "Inventaire plein.",
        ["banque pleine"] = "Banque pleine.",
        ["objet insuffisant"] = "Objet insuffisant.",
        ["objet insuffisant en banque"] = "Objet insuffisant en banque.",
        ["objets reserves pour un echange"] = "Objets réservés pour un échange.",
        ["article indisponible"] = "Article indisponible.",
        ["boutique inconnue"] = "Boutique inconnue.",
        ["objet inconnu"] = "Objet inconnu.",
        ["aucun personnage actif"] = "Choisissez un personnage.",
        ["personnage introuvable"] = "Personnage introuvable.",
        ["retrait inventaire echoue"] = "Le retrait de l'inventaire a échoué.",
        ["retrait banque echoue"] = "Le retrait de la banque a échoué.",
        ["shopbuyrequest invalide"] = "Demande d'achat invalide.",
        ["shopsellrequest invalide"] = "Demande de vente invalide.",
        ["bankdepositrequest invalide"] = "Demande de dépôt invalide.",
        ["bankwithdrawrequest invalide"] = "Demande de retrait invalide.",
    };
}
