using Frog.Application.Content;
using Frog.Editor.Services;

namespace Frog.Editor.Forms.Phase8;

/// <summary>Éditeur JSON pour types Phase 8 sans formulaire structuré (événement commun, métier, météo).</summary>
internal sealed class Phase8JsonEditorPanel : Phase8EditorPanelBase
{
    private readonly Phase8ContentKind _kind;
    private readonly TextBox _txtJson = new()
    {
        Multiline = true,
        Dock = DockStyle.Fill,
        Font = new Font(FontFamily.GenericMonospace, 9f),
        ScrollBars = ScrollBars.Both,
        WordWrap = false,
    };

    public Phase8JsonEditorPanel(Phase8ContentKind kind)
    {
        _kind = kind;
        var hint = new Label
        {
            Dock = DockStyle.Top,
            AutoSize = false,
            Height = 36,
            Text = "Édition JSON (validation à l'enregistrement).",
            Padding = new Padding(8, 8, 8, 0),
        };
        Controls.Add(_txtJson);
        Controls.Add(hint);
        _txtJson.TextChanged += (_, _) => NotifyChanged();
    }

    public override Phase8ContentKind Kind => _kind;

    public override void LoadPayload(string payloadJson)
    {
        Binding = true;
        try
        {
            _txtJson.Text = payloadJson;
        }
        finally
        {
            Binding = false;
        }
    }

    public override bool TryBuildPayload(out string payloadJson, out string? error)
    {
        payloadJson = _txtJson.Text.Trim();
        if (!Phase8ContentPostgreSqlService.TryValidatePayload(_kind, payloadJson, out error))
        {
            return false;
        }

        error = null;
        return true;
    }

    public override void ResetForNew(Guid newId)
    {
        base.ResetForNew(newId);
        LoadPayload(Phase8ContentPostgreSqlService.CreateDefaultPayload(_kind, newId, "Nouveau"));
    }
}
