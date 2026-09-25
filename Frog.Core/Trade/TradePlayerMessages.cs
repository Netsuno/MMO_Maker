using System.Globalization;
using System.Text;

namespace Frog.Core.Trade;

/// <summary>Libellés joueur (FR) pour les messages d'échange du serveur, accents compris.</summary>
public static class TradePlayerMessages
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
        ["cible invalide"] = "Cible invalide.",
        ["joueur hors ligne"] = "Ce joueur n'est pas en ligne.",
        ["personnage mort"] = "Impossible : un personnage est mort.",
        ["trop loin"] = "Trop loin pour échanger.",
        ["vous etes bloque"] = "Échange impossible : l'un de vous a bloqué l'autre.",
        ["echange deja en cours"] = "Un échange est déjà en cours.",
        ["trop d'invitations sortantes"] = "Trop d'invitations en attente.",
        ["trop d'invitations entrantes pour la cible"] = "Ce joueur a trop d'invitations en attente.",
        ["trop d'invitations"] = "Trop d'invitations.",
        ["patientez avant de renvoyer une invitation"] = "Patientez avant de renvoyer une invitation.",
        ["invitation envoyee"] = "Invitation envoyée.",
        ["echange introuvable"] = "Échange introuvable.",
        ["invitation invalide ou expiree"] = "Invitation invalide ou expirée.",
        ["echange accepte"] = "Échange accepté.",
        ["invitation refusee"] = "Invitation refusée.",
        ["echange annule"] = "Échange annulé.",
        ["echange deja valide"] = "Échange déjà validé.",
        ["offre invalide"] = "Offre invalide.",
        ["or insuffisant"] = "Or insuffisant.",
        ["objets insuffisants"] = "Objets insuffisants.",
        ["revision incorrecte"] = "L'offre a changé. Vérifiez avant de confirmer.",
        ["offre mise a jour"] = "Offre mise à jour.",
        ["confirmation enregistree"] = "Confirmation enregistrée. En attente du partenaire.",
        ["confirmation retiree"] = "Confirmation retirée.",
        ["echec de l'echange"] = "L'échange a échoué. Rien n'a été transféré.",
        ["inventaire plein"] = "Échange impossible : inventaire plein.",
        ["joueur deconnecte"] = "Échange annulé : le joueur s'est déconnecté.",
        ["participants hors portee"] = "Échange annulé : trop loin.",
        ["echange expire"] = "Échange expiré.",
        ["echange valide"] = "Échange validé.",
        ["action inconnue"] = "Action inconnue.",
        ["personnage actif requis"] = "Choisissez un personnage.",
        ["personnage introuvable"] = "Personnage introuvable.",
        ["plusieurs joueurs portent ce nom"] = "Plusieurs joueurs portent ce nom.",
    };
}
