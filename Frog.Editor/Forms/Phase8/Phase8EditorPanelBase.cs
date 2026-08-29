using Frog.Application.Content;

namespace Frog.Editor.Forms.Phase8;

/// <summary>Panneau d'édition structuré pour un type de contenu Phase 8.</summary>
internal abstract class Phase8EditorPanelBase : UserControl
{
    protected bool Binding;
    public Guid ContentId { get; set; } = Guid.Empty;

    public abstract Phase8ContentKind Kind { get; }

    public event Action? ContentChanged;

    public abstract void LoadPayload(string payloadJson);

    public abstract bool TryBuildPayload(out string payloadJson, out string? error);

    public virtual void ResetForNew(Guid newId)
    {
        ContentId = newId;
    }

    protected void NotifyChanged()
    {
        if (!Binding)
        {
            ContentChanged?.Invoke();
        }
    }
}
