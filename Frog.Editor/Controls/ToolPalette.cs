using System;
using System.Drawing;
using System.Windows.Forms;

using Frog.Editor.Enums;
using Frog.Editor.Ui;

namespace Frog.Editor.Controls;

public sealed class ToolPalette : UserControl
{
    private readonly ComboBox _combo;
    private bool _suspendCombo;

    public event Action<EditorTool>? ToolChanged;

    public EditorTool SelectedTool { get; private set; } = EditorTool.Brush;

    public ToolPalette()
    {
        AutoSize = true;
        Dock = DockStyle.Top;
        BackColor = EditorChrome.SidebarBg;
        Padding = new Padding(10, 12, 10, 8);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            ColumnCount = 1,
            RowCount = 2,
            BackColor = EditorChrome.SidebarBg,
        };
        root.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 34f));

        var title = EditorChrome.BuildSectionCaption("OUTIL");
        title.Margin = new Padding(0, 0, 0, 6);

        var row = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            AutoSize = true,
            BackColor = EditorChrome.SidebarBg,
        };
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 52f));
        row.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));

        var lbl = new Label
        {
            Text = "Outil",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = EditorChrome.LabelMuted,
            AutoSize = false,
            Font = EditorChrome.BodyFont,
        };

        _combo = new ComboBox
        {
            Dock = DockStyle.Fill,
            DropDownStyle = ComboBoxStyle.DropDownList,
            IntegralHeight = false,
            Margin = new Padding(0, 2, 0, 2),
        };
        EditorChrome.StyleSidebarComboBox(_combo);
        foreach (EditorTool t in Enum.GetValues<EditorTool>())
        {
            _combo.Items.Add(new ToolChoice(t, ToolLabel(t)));
        }

        _combo.SelectedIndex = 0;
        _combo.SelectedIndexChanged += OnComboSelectedIndexChanged;

        row.Controls.Add(lbl, 0, 0);
        row.Controls.Add(_combo, 1, 0);

        root.Controls.Add(title, 0, 0);
        root.Controls.Add(row, 0, 1);

        Controls.Add(root);
    }

    private static string ToolLabel(EditorTool t) =>
        t switch
        {
            EditorTool.Brush => "Pinceau",
            EditorTool.Eraser => "Gomme",
            EditorTool.Cursor => "Curseur",
            EditorTool.Fill => "Pot (remplissage)",
            EditorTool.Rectangle => "Rectangle",
            EditorTool.Selection => "Sélection",
            _ => t.ToString(),
        };

    private void OnComboSelectedIndexChanged(object? sender, EventArgs e)
    {
        if (_suspendCombo || _combo.SelectedItem is not ToolChoice ch)
        {
            return;
        }

        SelectedTool = ch.Tool;
        ToolChanged?.Invoke(SelectedTool);
    }

    /// <summary>Synchronise la liste si l’outil actif change par ailleurs (raccourcis futurs).</summary>
    public void SetSelectedTool(EditorTool tool)
    {
        for (var i = 0; i < _combo.Items.Count; i++)
        {
            if (_combo.Items[i] is ToolChoice ch && ch.Tool == tool)
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

                SelectedTool = tool;
                return;
            }
        }
    }

    private sealed record ToolChoice(EditorTool Tool, string Label)
    {
        public override string ToString() => Label;
    }
}
