namespace Frog.Core.Chat;

/// <summary>
/// Focus du chat et chuchotement. Le fil reste <c>ChatSend</c> / <c>ChatMessage</c> / <c>Error</c>
/// (pas de nouvel opcode, Hello inchangé).
/// </summary>
public static class ChatCompose
{
    public enum Key
    {
        Text,
        Enter,
        Escape,
        World,
    }

    public readonly record struct Decision(bool BlockWorldInput, bool FocusChat, bool ReleaseFocus, bool Send);

    public static Decision Decide(bool chatFocused, bool otherTextFocused, Key key)
    {
        if (chatFocused)
        {
            return key switch
            {
                Key.Escape => new Decision(true, false, true, false),
                Key.Enter => new Decision(true, true, false, true),
                _ => new Decision(true, false, false, false),
            };
        }

        if (otherTextFocused)
        {
            return new Decision(true, false, false, false);
        }

        if (key == Key.Enter)
        {
            return new Decision(true, true, false, false);
        }

        return new Decision(false, false, false, false);
    }

    public static Decision OnWorldClick(bool chatFocused) =>
        chatFocused
            ? new Decision(true, false, true, false)
            : new Decision(false, false, false, false);
}

/// <summary>Cible et retours français d'un chuchotement. Les chaînes <c>*Wire</c> sont celles du serveur.</summary>
public static class ChatWhisper
{
    public const string OfflineWire = "Joueur hors ligne.";
    public const string UnknownWire = "Joueur inconnu.";
    public const string BlockedWire = "Vous etes bloque.";
    public const string SelfWire = "Vous ne pouvez pas vous chuchoter.";
    public const string AmbiguousWire = "Plusieurs joueurs portent ce nom.";
    public const string EmptyWire = "Cible du chuchotement invalide.";
    public const string RateLimitWire = "Trop de messages.";

    public const string Offline = "Joueur hors ligne.";
    public const string Unknown = "Joueur inconnu.";
    public const string Blocked = "Vous êtes bloqué.";
    public const string Self = "Vous ne pouvez pas vous chuchoter.";
    public const string Ambiguous = "Plusieurs joueurs portent ce nom.";
    public const string EmptyTarget = "Indiquez le nom du joueur, ou sélectionnez un ami.";
    public const string SlashHint = "Exemple : /w Nom bonjour";
    public const string RateLimit = "Trop de messages. Réessayez dans un instant.";

    public enum Slash
    {
        None,
        Incomplete,
        Ready,
    }

    public static string SentTo(string name) => "Chuchotement envoyé à " + name.Trim() + ".";

    public static Slash ParseSlash(string? text, out string target, out string message)
    {
        target = string.Empty;
        message = string.Empty;
        if (string.IsNullOrWhiteSpace(text))
        {
            return Slash.None;
        }

        var trimmed = text.Trim();
        string? rest = null;
        if (StartsWithCommand(trimmed, "/w "))
        {
            rest = trimmed[3..];
        }
        else if (StartsWithCommand(trimmed, "/whisper "))
        {
            rest = trimmed[9..];
        }
        else if (StartsWithCommand(trimmed, "/chuchoter "))
        {
            rest = trimmed[11..];
        }
        else if (IsBareCommand(trimmed))
        {
            return Slash.Incomplete;
        }
        else
        {
            return Slash.None;
        }

        rest = rest.Trim();
        if (rest.Length == 0)
        {
            return Slash.Incomplete;
        }

        var space = rest.IndexOf(' ');
        if (space <= 0 || space >= rest.Length - 1)
        {
            return Slash.Incomplete;
        }

        target = rest[..space].Trim();
        message = rest[(space + 1)..].Trim();
        if (target.Length == 0 || message.Length == 0)
        {
            return Slash.Incomplete;
        }

        return Slash.Ready;
    }

    /// <summary>
    /// Nom saisi, sinon ami sélectionné, sinon cible monde (le mannequin est ignoré).
    /// </summary>
    public static bool TryResolveTarget(
        string? explicitName,
        string? selectedFriend,
        string? selectedTarget,
        string? reservedTarget,
        out string target)
    {
        if (TryUse(explicitName, reserved: null, out target))
        {
            return true;
        }

        if (TryUse(selectedFriend, reserved: null, out target))
        {
            return true;
        }

        return TryUse(selectedTarget, reservedTarget, out target);
    }

    public static bool IsSelf(string? target, string? username, string? characterName)
    {
        if (string.IsNullOrWhiteSpace(target))
        {
            return false;
        }

        var name = target.Trim();
        return EqualsName(name, username) || EqualsName(name, characterName);
    }

    public static bool TryPresent(string? raw, bool knownOffline, out string french)
    {
        french = string.Empty;
        if (string.IsNullOrWhiteSpace(raw))
        {
            return false;
        }

        var text = raw.Trim();
        if (EqualsName(text, BlockedWire) || EqualsName(text, Blocked))
        {
            french = Blocked;
            return true;
        }

        if (EqualsName(text, OfflineWire))
        {
            // Le serveur existant répond la même phrase si le compte est absent ou déconnecté.
            french = knownOffline ? Offline : "Joueur hors ligne ou inconnu.";
            return true;
        }

        if (EqualsName(text, UnknownWire))
        {
            french = knownOffline ? Offline : Unknown;
            return true;
        }

        if (EqualsName(text, EmptyWire))
        {
            french = EmptyTarget;
            return true;
        }

        if (EqualsName(text, SelfWire))
        {
            french = Self;
            return true;
        }

        if (EqualsName(text, AmbiguousWire))
        {
            french = Ambiguous;
            return true;
        }

        if (EqualsName(text, RateLimitWire))
        {
            french = RateLimit;
            return true;
        }

        return false;
    }

    private static bool TryUse(string? candidate, string? reserved, out string target)
    {
        target = string.Empty;
        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        var name = candidate.Trim();
        if (reserved is not null && EqualsName(name, reserved))
        {
            return false;
        }

        target = name;
        return true;
    }

    private static bool StartsWithCommand(string text, string command) =>
        text.StartsWith(command, StringComparison.OrdinalIgnoreCase);

    private static bool IsBareCommand(string text) =>
        text.Equals("/w", StringComparison.OrdinalIgnoreCase)
        || text.Equals("/whisper", StringComparison.OrdinalIgnoreCase)
        || text.Equals("/chuchoter", StringComparison.OrdinalIgnoreCase);

    private static bool EqualsName(string left, string? right) =>
        !string.IsNullOrWhiteSpace(right)
        && string.Equals(left, right.Trim(), StringComparison.OrdinalIgnoreCase);
}
