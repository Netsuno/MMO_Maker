namespace Frog.Core.Events;

/// <summary>
/// Décide si un <c>InteractResult</c> ouvre la boîte de texte.
/// Le fil reste l'opcode 32 (Hello 11) : pas de champ ajouté.
/// </summary>
public static class EventShowTextPresentation
{
    public const string NothingHere = "Rien a interagir ici.";
    public const string PickupAck = "Ramasse.";
    public const string PagePrefix = "[Page] ";
    public const string StepPrefix = "[Marche] ";

    public readonly record struct Placement(string DisplayName, string Slug);

    public static bool ShouldOpen(
        bool success,
        string? message,
        bool dialogueActive,
        string? dialogueSpeaker,
        string? dialogueText,
        IEnumerable<Placement>? placements)
    {
        if (!success || string.IsNullOrWhiteSpace(message))
        {
            return false;
        }

        var text = message.Trim();
        if (IsReservedFallback(text, placements))
        {
            return false;
        }

        if (dialogueActive && IsDialogueEcho(text, dialogueSpeaker, dialogueText))
        {
            return false;
        }

        return true;
    }

    public static bool IsReservedFallback(string text, IEnumerable<Placement>? placements)
    {
        if (text is NothingHere or PickupAck)
        {
            return true;
        }

        if (text.StartsWith(PagePrefix, StringComparison.Ordinal)
            || text.StartsWith(StepPrefix, StringComparison.Ordinal))
        {
            return true;
        }

        if (placements is null)
        {
            return false;
        }

        foreach (var placement in placements)
        {
            var name = placement.DisplayName ?? string.Empty;
            var slug = placement.Slug ?? string.Empty;
            if (text == name + " (" + slug + ")")
            {
                return true;
            }
        }

        return false;
    }

    public static bool IsDialogueEcho(string text, string? speaker, string? dialogueText)
    {
        var body = (dialogueText ?? string.Empty).Trim();
        if (body.Length == 0)
        {
            return false;
        }

        if (text == body)
        {
            return true;
        }

        var who = (speaker ?? string.Empty).Trim();
        return who.Length > 0 && text == who + ": " + body;
    }
}
