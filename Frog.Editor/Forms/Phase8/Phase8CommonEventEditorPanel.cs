using Frog.Application.Content;
using Frog.Core.Models;
using Frog.Editor.Services;

namespace Frog.Editor.Forms.Phase8;

/// <summary>Éditeur structuré d'événement commun (pages typées, pas de JSON requis).</summary>
internal sealed class Phase8CommonEventEditorPanel : Phase8EditorPanelBase
{
    private readonly TextBox _name = new() { Width = 320 };
    private readonly MapEventPagesEditorPanel _pages = new() { Dock = DockStyle.Fill };

    public Phase8CommonEventEditorPanel()
    {
        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(4),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var nameRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false, Padding = new Padding(4) };
        nameRow.Controls.Add(new Label { Text = "Nom", AutoSize = true, Margin = new Padding(0, 8, 8, 0) });
        nameRow.Controls.Add(_name);
        root.Controls.Add(nameRow, 0, 0);
        root.Controls.Add(_pages, 0, 1);
        Controls.Add(root);

        _name.TextChanged += (_, _) => NotifyChanged();
        _pages.PagesChanged += () => NotifyChanged();
    }

    public override Phase8ContentKind Kind => Phase8ContentKind.CommonEvent;

    public override void LoadPayload(string payloadJson)
    {
        if (!Phase8ContentPostgreSqlService.TryDeserialize(payloadJson, out CommonEventDefinition def, out _))
        {
            def = new CommonEventDefinition();
        }

        ContentId = def.Id;
        Binding = true;
        try
        {
            _name.Text = def.Name;
            _pages.LoadPages(def.Pages);
        }
        finally
        {
            Binding = false;
        }
    }

    public override bool TryBuildPayload(out string payloadJson, out string? error)
    {
        if (!_pages.TryBuildPages(out var pages, out error))
        {
            payloadJson = string.Empty;
            return false;
        }

        var def = new CommonEventDefinition
        {
            Id = ContentId,
            Name = !string.IsNullOrWhiteSpace(CatalogName) ? CatalogName.Trim() : _name.Text.Trim(),
            Pages = pages.ToList(),
        };
        if (!def.Validate(out error))
        {
            payloadJson = string.Empty;
            return false;
        }

        payloadJson = Phase8ContentPostgreSqlService.Serialize(def);
        error = null;
        return true;
    }

    public override void ResetForNew(Guid newId)
    {
        LoadPayload(Phase8ContentPostgreSqlService.CreateDefaultPayload(Phase8ContentKind.CommonEvent, newId, "Nouvel événement commun"));
    }

    internal TextBox NameForTest => _name;

    internal MapEventPagesEditorPanel PagesPanelForTest => _pages;

    internal ListBox PagesForTest => _pages.PagesForTest;
}
