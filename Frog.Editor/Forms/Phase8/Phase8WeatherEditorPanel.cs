using Frog.Application.Content;
using Frog.Core.Models;
using Frog.Editor.Services;

namespace Frog.Editor.Forms.Phase8;

/// <summary>Éditeur structuré de profil météo (nom, kind, luminosité).</summary>
internal sealed class Phase8WeatherEditorPanel : Phase8EditorPanelBase
{
    private readonly TextBox _name = new() { Width = 320 };
    private readonly TextBox _weatherKind = new() { Width = 200 };
    private readonly NumericUpDown _lighting = new()
    {
        DecimalPlaces = 2,
        Minimum = 0,
        Maximum = 1,
        Increment = 0.05m,
        Width = 100,
        Value = 1,
    };

    public Phase8WeatherEditorPanel()
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
        Row("WeatherKind", _weatherKind);
        Row("LightingFactor", _lighting);
        Controls.Add(form);

        _name.TextChanged += (_, _) => NotifyChanged();
        _weatherKind.TextChanged += (_, _) => NotifyChanged();
        _lighting.ValueChanged += (_, _) => NotifyChanged();
    }

    public override Phase8ContentKind Kind => Phase8ContentKind.WeatherProfile;

    public override void LoadPayload(string payloadJson)
    {
        if (!Phase8ContentPostgreSqlService.TryDeserialize(payloadJson, out WeatherProfileDefinition def, out _))
        {
            def = new WeatherProfileDefinition();
        }

        ContentId = def.Id;
        Binding = true;
        try
        {
            _name.Text = def.Name;
            _weatherKind.Text = def.WeatherKind;
            _lighting.Value = (decimal)Math.Clamp(def.LightingFactor, 0f, 1f);
        }
        finally
        {
            Binding = false;
        }
    }

    public override bool TryBuildPayload(out string payloadJson, out string? error)
    {
        var def = new WeatherProfileDefinition
        {
            Id = ContentId,
            Name = !string.IsNullOrWhiteSpace(CatalogName) ? CatalogName.Trim() : _name.Text.Trim(),
            WeatherKind = _weatherKind.Text.Trim(),
            LightingFactor = (float)_lighting.Value,
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
        LoadPayload(Phase8ContentPostgreSqlService.CreateDefaultPayload(Phase8ContentKind.WeatherProfile, newId, "Nouveau profil météo"));
    }

    internal TextBox NameForTest => _name;

    internal TextBox WeatherKindForTest => _weatherKind;

    internal NumericUpDown LightingForTest => _lighting;
}
