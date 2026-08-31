namespace Frog.Core.Models;

/// <summary>Dialogue typé publié (Phase 8 — P8-3).</summary>
public sealed class DialogueDefinition
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int? EditorAliasId { get; set; }

    /// <summary>Lignes séquentielles (speaker + text).</summary>
    public IReadOnlyList<DialogueLineDefinition> Lines { get; set; } = Array.Empty<DialogueLineDefinition>();

    /// <summary>Choix finaux (optionnel).</summary>
    public IReadOnlyList<DialogueChoiceDefinition> Choices { get; set; } = Array.Empty<DialogueChoiceDefinition>();

    public bool Validate(out string? error)
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            error = "Nom de dialogue requis.";
            return false;
        }

        if (Lines.Count == 0 && Choices.Count == 0)
        {
            error = "Dialogue vide.";
            return false;
        }

        foreach (var line in Lines)
        {
            if (!line.Validate(out error))
            {
                return false;
            }
        }

        foreach (var choice in Choices)
        {
            if (!choice.Validate(out error))
            {
                return false;
            }
        }

        error = null;
        return true;
    }
}

public sealed class DialogueLineDefinition
{
    public string Speaker { get; set; } = string.Empty;

    public string Text { get; set; } = string.Empty;

    public bool Validate(out string? error)
    {
        if (string.IsNullOrWhiteSpace(Text))
        {
            error = "Texte de ligne requis.";
            return false;
        }

        if (Text.Length > 512)
        {
            error = "Texte trop long (max 512).";
            return false;
        }

        error = null;
        return true;
    }
}

public sealed class DialogueChoiceDefinition
{
    public string ChoiceId { get; set; } = string.Empty;

    public string Label { get; set; } = string.Empty;

    /// <summary>Quête démarrée si le joueur choisit cette option.</summary>
    public Guid? StartQuestId { get; set; }

    public bool Validate(out string? error)
    {
        if (string.IsNullOrWhiteSpace(ChoiceId))
        {
            error = "ChoiceId requis.";
            return false;
        }

        if (string.IsNullOrWhiteSpace(Label))
        {
            error = "Label requis.";
            return false;
        }

        error = null;
        return true;
    }
}
