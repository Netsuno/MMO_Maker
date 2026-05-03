using System;
using System.Drawing;
using System.Windows.Forms;

using Frog.Core.Enums;
using Frog.Editor.Ui;

namespace Frog.Editor.Controls;

/// <summary>Type de tuile logique : liste déroulante compacte pour colonnes étroites.</summary>
public sealed class TileTypePalette : UserControl
{
    private readonly ComboBox _combo;
    private bool _suspendCombo;

    public event Action<TileType>? SelectedTileTypeChanged;

    public TileType SelectedTileType { get; private set; } = TileType.Ground;

    public TileTypePalette()
    {
        AutoSize = true;
        Dock = DockStyle.Top;
        BackColor = EditorChrome.SidebarBg;
        Padding = new Padding(12, 10, 14, 14);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = EditorChrome.SidebarBg,
            Padding = new Padding(2, 0, 2, 0),
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));

        var title = EditorChrome.BuildSectionCaption("TYPE DE TUILE");
        title.Margin = new Padding(0, 0, 0, 10);

        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            AutoSize = true,
            BackColor = EditorChrome.SidebarBg,
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 86f));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        var lbl = new Label
        {
            Text = "Type",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = EditorChrome.LabelMuted,
            AutoSize = false,
            Font = EditorChrome.BodyFont,
            Margin = new Padding(6, 0, 4, 0),
        };

        _combo = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            IntegralHeight = false,
        };
        EditorChrome.StyleSidebarComboBox(_combo);

        foreach (var (type, label) in TileChoices)
        {
            _combo.Items.Add(new TypeChoice(type, label));
        }

        _combo.SelectedIndex = 0;
        _combo.SelectedIndexChanged += OnComboSelectedIndexChanged;

        row.Controls.Add(lbl, 0, 0);
        row.Controls.Add(_combo, 1, 0);

        root.Controls.Add(title, 0, 0);
        root.Controls.Add(row, 0, 1);

        Controls.Add(root);
    }

    private static readonly (TileType Type, string Label)[] TileChoices =
    {
        (TileType.Ground, "Terrain"),
        (TileType.Block, "Blocage"),
        (TileType.Warp, "Warp"),
        (TileType.Resource, "Ressource"),
        (TileType.Script, "Script"),
    };

    private void OnComboSelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_suspendCombo || _combo.SelectedItem is not TypeChoice ch)
        {
            return;
        }

        SelectedTileType = ch.Type;
        SelectedTileTypeChanged?.Invoke(SelectedTileType);
    }

    public void SetSelectedTileType(TileType type)
    {
        for (var i = 0; i < _combo.Items.Count; i++)
        {
            if (_combo.Items[i] is TypeChoice ch && ch.Type == type)
            {
                _suspendCombo = true;
                try
                {
                    _combo.SelectedIndex = i;
                }
                finally
                {
                    _suspendCombo = false;
                }

                SelectedTileType = type;
                return;
            }
        }
    }

    private sealed record TypeChoice(TileType Type, string Label)
    {
        public override string ToString() => Label;
    }
}
