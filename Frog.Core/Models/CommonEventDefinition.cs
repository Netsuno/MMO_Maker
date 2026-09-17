namespace Frog.Core.Models;

/// <summary>Événement commun réutilisable (Phase 8 — P8-6).</summary>
public sealed class CommonEventDefinition
{
    public Guid Id { get; set; }

    public string Name { get; set; } = string.Empty;

    public int? EditorAliasId { get; set; }

    public IReadOnlyList<MapEventPageDefinition> Pages { get; set; } = Array.Empty<MapEventPageDefinition>();

    public bool Validate(out string? error)
    {
        if (string.IsNullOrWhiteSpace(Name))
        {
            error = "Nom requis.";
            return false;
        }

        if (Pages.Count > MapEventRuntimeLimits.MaxPagesPerEvent)
        {
            error = "Trop de pages.";
            return false;
        }

        foreach (var page in Pages)
        {
            if (!page.Validate(out error))
            {
                return false;
            }
        }

        error = null;
        return true;
    }
}
