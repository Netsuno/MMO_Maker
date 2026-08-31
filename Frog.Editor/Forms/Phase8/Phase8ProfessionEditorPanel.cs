using Frog.Application.Content;
using Frog.Core.Models;
using Frog.Editor.Services;

namespace Frog.Editor.Forms.Phase8;

/// <summary>Éditeur structuré de métier (nom + niveau max).</summary>
internal sealed class Phase8ProfessionEditorPanel : Phase8EditorPanelBase
{
    private readonly TextBox _name = new() { Width = 320 };
    private readonly NumericUpDown _maxLevel = new() { Minimum = 1, Maximum = 999, Width = 100, Value = 100 };

    public Phase8ProfessionEditorPanel()
    {
        var form = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            AutoScroll = true,
            Padding = new Padding(8),
        };
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        void Row(string label, Control control)
        {
            var row = form.RowCount++;
            form.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
            form.Controls.Add(control, 1, row);
        }

        Row("Nom", _name);
        Row("MaxLevel", _maxLevel);
        Controls.Add(form);

        _name.TextChanged += (_, _) => NotifyChanged();
        _maxLevel.ValueChanged += (_, _) => NotifyChanged();
    }

    public override Phase8ContentKind Kind => Phase8ContentKind.Profession;

    public override void LoadPayload(string payloadJson)
    {
        if (!Phase8ContentPostgreSqlService.TryDeserialize(payloadJson, out ProfessionDefinition def, out _))
        {
            def = new ProfessionDefinition();
        }

        ContentId = def.Id;
        Binding = true;
        try
        {
            _name.Text = def.Name;
            _maxLevel.Value = Math.Clamp(def.MaxLevel, 1, 999);
        }
        finally
        {
            Binding = false;
        }
    }

    public override bool TryBuildPayload(out string payloadJson, out string? error)
    {
        var def = new ProfessionDefinition
        {
            Id = ContentId,
            Name = !string.IsNullOrWhiteSpace(CatalogName) ? CatalogName.Trim() : _name.Text.Trim(),
            MaxLevel = (int)_maxLevel.Value,
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
        LoadPayload(Phase8ContentPostgreSqlService.CreateDefaultPayload(Phase8ContentKind.Profession, newId, "Nouveau métier"));
    }

    internal TextBox NameForTest => _name;

    internal NumericUpDown MaxLevelForTest => _maxLevel;
}
