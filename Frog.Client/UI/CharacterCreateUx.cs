using Frog.Core.Character;

namespace Frog.Client.UI;

/// <summary>
/// Texte et décisions de la page « créer / choisir un personnage ».
/// Les messages d'erreur de nom reprennent <see cref="CharacterDisplayNameRules"/>.
/// </summary>
public static class CharacterCreateUx
{
    public const string PageTitle = "Choisir votre personnage";
    public const string ExistingHeading = "Vos personnages";
    public const string CreateHeading = "Nouveau personnage";
    public const string NameLabel = "Nom";
    public const string ClassLabel = "Classe";
    public const string NamePlaceholder = "Nom du personnage";
    public const string CreateButton = "Créer le personnage";
    public const string EnterButton = "Entrer dans le jeu";
    public const string RefreshButton = "Liste persos";
    public const string HintRules = "Lettres, chiffres, espaces, tiret ou souligné · 32 caractères max.";
    public const string HintNeedClass = "En attente des classes du serveur.";
    public const string HintReady = "Entrée crée le personnage.";
    public const string NeedNameStatus = "Saisissez un nom pour le nouveau personnage.";
    public const string NeedConnectionStatus = "Connexion requise pour créer un personnage.";
    public const string CreatedStatus = "Personnage créé.";
    public const string CreateRejectedStatus = "Création refusée.";
    public const string NeedPickStatus = "Choisissez un personnage dans la liste.";

    public enum EnterAction
    {
        None = 0,
        Create = 1,
        EnterGame = 2,
    }

    public readonly record struct NameFeedback(bool Ok, string Normalized, string Message, bool IsError);

    public static NameFeedback Describe(string? rawName, bool classSelected)
    {
        if (!CharacterDisplayNameRules.TryNormalize(rawName, out var name, out var error))
        {
            var empty = string.IsNullOrWhiteSpace(rawName);
            return new NameFeedback(false, string.Empty, empty ? HintRules : error, IsError: !empty);
        }

        if (!classSelected)
        {
            return new NameFeedback(false, name, HintNeedClass, IsError: false);
        }

        return new NameFeedback(true, name, HintReady, IsError: false);
    }

    /// <summary>
    /// Hint affiché. Tant que le catalogue n'a pas de classe, la phrase d'attente
    /// s'ajoute au rappel des règles (le combo reste désactivé).
    /// </summary>
    public static string HintFor(string? rawName, bool classSelected, bool classesAvailable, bool sessionOpen)
    {
        var feedback = Describe(rawName, classSelected);
        if (!sessionOpen || classesAvailable || feedback.IsError)
        {
            return feedback.Message;
        }

        if (string.IsNullOrWhiteSpace(rawName))
        {
            return HintRules + " " + HintNeedClass;
        }

        return feedback.Message;
    }

    public static string BlockedCreateMessage(string? rawName, bool classSelected)
    {
        if (string.IsNullOrWhiteSpace(rawName))
        {
            return NeedNameStatus;
        }

        return Describe(rawName, classSelected).Message;
    }

    public static bool CanCreate(string? rawName, bool classSelected, bool sessionOpen)
        => sessionOpen && classSelected && CharacterDisplayNameRules.TryNormalize(rawName, out _, out _);

    public static bool CanEnter(bool sessionOpen, bool hasSelection)
        => sessionOpen && hasSelection;

    /// <summary>
    /// Entrée dans le champ nom ou la classe crée ; Entrée dans la liste entre en jeu.
    /// Un bouton ou une liste déroulante ouverte garde le comportement natif.
    /// </summary>
    public static EnterAction DecideEnter(
        bool nameFocused,
        bool classFocused,
        bool classDroppedDown,
        bool characterFocused,
        bool characterDroppedDown,
        bool buttonFocused)
    {
        if (buttonFocused || classDroppedDown || characterDroppedDown)
        {
            return EnterAction.None;
        }

        if (nameFocused || classFocused)
        {
            return EnterAction.Create;
        }

        if (characterFocused)
        {
            return EnterAction.EnterGame;
        }

        return EnterAction.None;
    }
}
