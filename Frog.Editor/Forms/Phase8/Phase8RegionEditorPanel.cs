using Frog.Application.Content;
using Frog.Core.Models;
using Frog.Editor.Services;

namespace Frog.Editor.Forms.Phase8;

/// <summary>Éditeur structuré de région carte (bornes tuiles + profil météo).</summary>
internal sealed class Phase8RegionEditorPanel : Phase8EditorPanelBase
{
    private readonly NumericUpDown _mapId = new() { Minimum = 0, Maximum = int.MaxValue, Width = 100 };
    private readonly NumericUpDown _tileXMin = new() { Minimum = int.MinValue, Maximum = int.MaxValue, Width = 90 };
    private readonly NumericUpDown _tileYMin = new() { Minimum = int.MinValue, Maximum = int.MaxValue, Width = 90 };
    private readonly NumericUpDown _tileXMax = new() { Minimum = int.MinValue, Maximum = int.MaxValue, Width = 90, Value = 10 };
    private readonly NumericUpDown _tileYMax = new() { Minimum = int.MinValue, Maximum = int.MaxValue, Width = 90, Value = 10 };
    private readonly TextBox _weatherProfileId = new() { Width = 320 };

    public Phase8RegionEditorPanel()
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

        Row("MapId (int)", _mapId);
        var bounds = new FlowLayoutPanel { AutoSize = true, WrapContents = true };
        bounds.Controls.Add(new Label { Text = "X min", AutoSize = true, Margin = new Padding(0, 8, 4, 0) });
        bounds.Controls.Add(_tileXMin);
        bounds.Controls.Add(new Label { Text = "Y min", AutoSize = true, Margin = new Padding(8, 8, 4, 0) });
        bounds.Controls.Add(_tileYMin);
        bounds.Controls.Add(new Label { Text = "X max", AutoSize = true, Margin = new Padding(8, 8, 4, 0) });
        bounds.Controls.Add(_tileXMax);
        bounds.Controls.Add(new Label { Text = "Y max", AutoSize = true, Margin = new Padding(8, 8, 4, 0) });
        bounds.Controls.Add(_tileYMax);
        Row("Bornes tuiles", bounds);
        Row("WeatherProfileId", _weatherProfileId);

        Controls.Add(form);

        _mapId.ValueChanged += (_, _) => NotifyChanged();
        _tileXMin.ValueChanged += (_, _) => NotifyChanged();
        _tileYMin.ValueChanged += (_, _) => NotifyChanged();
        _tileXMax.ValueChanged += (_, _) => NotifyChanged();
        _tileYMax.ValueChanged += (_, _) => NotifyChanged();
        _weatherProfileId.TextChanged += (_, _) => NotifyChanged();
    }

    public override Phase8ContentKind Kind => Phase8ContentKind.Region;

    public override void LoadPayload(string payloadJson)
    {
        if (!Phase8ContentPostgreSqlService.TryDeserialize(payloadJson, out RegionDefinition def, out _))
        {
            def = new RegionDefinition();
        }

        ContentId = def.Id;
        Binding = true;
        try
        {
            _mapId.Value = def.MapId;
            _tileXMin.Value = def.TileXMin;
            _tileYMin.Value = def.TileYMin;
            _tileXMax.Value = def.TileXMax;
            _tileYMax.Value = def.TileYMax;
            _weatherProfileId.Text = def.WeatherProfileId.ToString("D");
        }
        finally
        {
            Binding = false;
        }
    }

    public override bool TryBuildPayload(out string payloadJson, out string? error)
    {
        if (!Guid.TryParse(_weatherProfileId.Text.Trim(), out var weatherId))
        {
            payloadJson = string.Empty;
            error = "WeatherProfileId invalide.";
            return false;
        }

        var def = new RegionDefinition
        {
            Id = ContentId,
            MapId = (int)_mapId.Value,
            TileXMin = (int)_tileXMin.Value,
            TileYMin = (int)_tileYMin.Value,
            TileXMax = (int)_tileXMax.Value,
            TileYMax = (int)_tileYMax.Value,
            WeatherProfileId = weatherId,
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
        LoadPayload(Phase8ContentPostgreSqlService.CreateDefaultPayload(Phase8ContentKind.Region, newId, "Nouvelle région"));
    }
}
