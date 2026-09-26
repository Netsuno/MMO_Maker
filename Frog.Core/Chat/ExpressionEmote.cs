namespace Frog.Core.Chat;

/// <summary>
/// Bulle d'expression courte (style Graal) au-dessus d'un joueur.
/// Le déclencheur <c>/e</c> envoie le jeton sur <c>ChatSend</c> / <c>ChatMessage</c>
/// (canal carte). Pas de nouvel opcode. Hello inchangé.
/// </summary>
public readonly record struct ExpressionEmote(string Id, string Glyph, string Wire)
{
    /// <summary>Pleine opacité avant le fondu.</summary>
    public const int HoldMs = 2200;

    /// <summary>Fondu linéaire après <see cref="HoldMs"/>.</summary>
    public const int FadeMs = 800;

    public const string IncompleteHint =
        "Emote : /e sourire, salut, rire, triste, pense, coeur, colere, oui, non, zzz.";

    public const string UnknownHint = "Emote inconnue. Ex. : /e sourire";

    public enum Slash
    {
        None,
        Incomplete,
        Unknown,
        Ready,
    }

    private static readonly Dictionary<string, ExpressionEmote> ByAlias = new(StringComparer.OrdinalIgnoreCase);
    private static readonly Dictionary<string, ExpressionEmote> ByWire = new(StringComparer.OrdinalIgnoreCase);

    public static IReadOnlyList<ExpressionEmote> All { get; }

    static ExpressionEmote()
    {
        var all = new List<ExpressionEmote>();

        void Add(ExpressionEmote emote, params string[] aliases)
        {
            all.Add(emote);
            ByWire.Add(emote.Wire, emote);
            ByAlias.Add(emote.Id, emote);
            foreach (var alias in aliases)
            {
                ByAlias.Add(alias, emote);
            }
        }

        Add(new ExpressionEmote("smile", ":-)", "*sourit*"), "sourire", ":)", ":-)");
        Add(new ExpressionEmote("wave", "Salut", "*salut*"), "salut", "hello", "coucou");
        Add(new ExpressionEmote("laugh", ":D", "*rit*"), "rire", "lol", "mdr", ":D");
        Add(new ExpressionEmote("sad", ":(", "*triste*"), "triste", ":(");
        Add(new ExpressionEmote("think", "?", "*pense*"), "pense", "hmm");
        Add(new ExpressionEmote("heart", "\u2665", "*coeur*"), "coeur", "cœur", "love", "<3");
        Add(new ExpressionEmote("angry", "!!", "*grrr*"), "colere", "colère", "rage");
        Add(new ExpressionEmote("yes", "Oui", "*oui*"), "oui", "ok");
        Add(new ExpressionEmote("no", "Non", "*non*"), "non");
        Add(new ExpressionEmote("sleep", "zZz", "*zzz*"), "dodo", "zzz");
        All = all;
    }

    public static Slash ParseSlash(string? text, out ExpressionEmote emote)
    {
        emote = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return Slash.None;
        }

        var trimmed = text.Trim();
        string? rest = null;
        if (IsBare(trimmed))
        {
            return Slash.Incomplete;
        }

        if (Starts(trimmed, "/e "))
        {
            rest = trimmed[3..];
        }
        else if (Starts(trimmed, "/emo "))
        {
            rest = trimmed[5..];
        }
        else if (Starts(trimmed, "/emote "))
        {
            rest = trimmed[7..];
        }
        else if (Starts(trimmed, "/expression "))
        {
            rest = trimmed[12..];
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

        var token = rest;
        var space = rest.IndexOf(' ');
        if (space > 0)
        {
            token = rest[..space];
        }

        if (!ByAlias.TryGetValue(token, out emote))
        {
            emote = default;
            return Slash.Unknown;
        }

        return Slash.Ready;
    }

    /// <summary>Jeton exact relayé par le chat (casse ignorée, bords ignorés).</summary>
    public static bool TryParseWire(string? message, out ExpressionEmote emote)
    {
        emote = default;
        if (string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        return ByWire.TryGetValue(message.Trim(), out emote);
    }

    /// <summary>1 pendant le maintien, puis fondu jusqu'à 0.</summary>
    public static float Opacity(long ageMs)
    {
        if (ageMs < 0 || ageMs <= HoldMs)
        {
            return 1f;
        }

        if (ageMs >= HoldMs + FadeMs)
        {
            return 0f;
        }

        return 1f - ((ageMs - HoldMs) / (float)FadeMs);
    }

    private static bool Starts(string text, string command) =>
        text.StartsWith(command, StringComparison.OrdinalIgnoreCase);

    private static bool IsBare(string text) =>
        text.Equals("/e", StringComparison.OrdinalIgnoreCase)
        || text.Equals("/emo", StringComparison.OrdinalIgnoreCase)
        || text.Equals("/emote", StringComparison.OrdinalIgnoreCase)
        || text.Equals("/expression", StringComparison.OrdinalIgnoreCase);
}

/// <summary>
/// Une bulle par joueur, sur le fil d'UI. Le dessin lit la position courante.
/// </summary>
public sealed class ExpressionBubbleBoard
{
    private readonly Dictionary<string, Slot> _byUser = new(StringComparer.OrdinalIgnoreCase);

    private readonly record struct Slot(ExpressionEmote Emote, DateTime StartedUtc);

    public readonly record struct Visible(string Username, string Glyph, float Opacity);

    public int Count => _byUser.Count;

    public void Show(string? username, ExpressionEmote emote, DateTime utcNow)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrEmpty(emote.Glyph))
        {
            return;
        }

        _byUser[username.Trim()] = new Slot(emote, utcNow);
    }

    public void Remove(string? username)
    {
        if (string.IsNullOrWhiteSpace(username))
        {
            return;
        }

        _byUser.Remove(username.Trim());
    }

    public void Clear() => _byUser.Clear();

    /// <summary>
    /// Retire les bulles terminées. Vrai pendant le fondu, ou si une bulle vient d'être retirée
    /// (il faut encore peindre une image sans elle). Le maintien plein ne redessine pas à lui seul.
    /// </summary>
    public bool Advance(DateTime utcNow)
    {
        List<string>? drop = null;
        var fading = false;
        foreach (var pair in _byUser)
        {
            var age = (long)(utcNow - pair.Value.StartedUtc).TotalMilliseconds;
            var opacity = ExpressionEmote.Opacity(age);
            if (opacity <= 0f)
            {
                drop ??= new List<string>();
                drop.Add(pair.Key);
            }
            else if (opacity < 1f)
            {
                fading = true;
            }
        }

        if (drop is not null)
        {
            foreach (var key in drop)
            {
                _byUser.Remove(key);
            }
        }

        return drop is not null || fading;
    }

    public void CopyVisible(DateTime utcNow, List<Visible> into)
    {
        ArgumentNullException.ThrowIfNull(into);
        into.Clear();
        foreach (var pair in _byUser)
        {
            var age = (long)(utcNow - pair.Value.StartedUtc).TotalMilliseconds;
            var opacity = ExpressionEmote.Opacity(age);
            if (opacity <= 0f || string.IsNullOrEmpty(pair.Value.Emote.Glyph))
            {
                continue;
            }

            into.Add(new Visible(pair.Key, pair.Value.Emote.Glyph, opacity));
        }
    }
}
