using System.Drawing;
using System.Windows.Forms;
using Frog.Application.Maps;
using Frog.Editor.Ui;

namespace Frog.Editor.Dialogs;

internal enum MapTemplateListChoice
{
    None = 0,
    Cursor = 1,
    Origin = 2,
    Delete = 3,
}

/// <summary>Liste des modèles enregistrés : poser au curseur, à l’origine, ou supprimer.</summary>
internal sealed class MapTemplateListDialog : Form
{
    private readonly ListBox _list;
    private readonly Label _hint;
    private readonly MapStampTemplate[] _templates;

    public MapTemplateListDialog(IReadOnlyList<MapStampTemplate> templates)
    {
        ArgumentNullException.ThrowIfNull(templates);
        _templates = templates.ToArray();
        Text = "Modèles de carte";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(560, 420);
        AutoScaleMode = AutoScaleMode.Dpi;
        ShowInTaskbar = false;
        EditorChrome.ApplyFormChrome(this);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 3,
            Padding = new Padding(16, 14, 16, 12),
        };
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 36f));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52f));

        _hint = new Label
        {
            Text = "Choisissez un modèle. La pose réécrit le rectangle, trous compris, sur les couches éditables.",
            Dock = DockStyle.Fill,
            ForeColor = EditorChrome.LabelMuted,
            AutoSize = false,
        };
        _list = new ListBox
        {
            Dock = DockStyle.Fill,
            IntegralHeight = false,
        };
        foreach (var template in _templates)
        {
            _list.Items.Add(MapStampTemplateOperations.FormatSummary(template));
        }

        if (_templates.Length > 0)
        {
            _list.SelectedIndex = 0;
        }

        _list.DoubleClick += (_, _) => Accept(MapTemplateListChoice.Cursor);

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill,
            WrapContents = false,
            Padding = new Padding(0, 8, 0, 0),
        };
        var cursor = MakeButton("Poser au curseur", primary: true);
        var origin = MakeButton("Poser à l’origine", primary: false);
        var delete = MakeButton("Supprimer", primary: false);
        var close = MakeButton("Fermer", primary: false);
        cursor.Click += (_, _) => Accept(MapTemplateListChoice.Cursor);
        origin.Click += (_, _) => Accept(MapTemplateListChoice.Origin);
        delete.Click += (_, _) => Accept(MapTemplateListChoice.Delete);
        close.Click += (_, _) =>
        {
            Choice = MapTemplateListChoice.None;
            DialogResult = DialogResult.Cancel;
            Close();
        };
        buttons.Controls.Add(cursor);
        buttons.Controls.Add(origin);
        buttons.Controls.Add(delete);
        buttons.Controls.Add(close);

        root.Controls.Add(_hint, 0, 0);
        root.Controls.Add(_list, 0, 1);
        root.Controls.Add(buttons, 0, 2);
        Controls.Add(root);
        CancelButton = close;
        AcceptButton = cursor;
    }

    public MapTemplateListChoice Choice { get; private set; }

    public MapStampTemplate? Selected
        => _list.SelectedIndex >= 0 && _list.SelectedIndex < _templates.Length
            ? _templates[_list.SelectedIndex]
            : null;

    private void Accept(MapTemplateListChoice choice)
    {
        if (Selected is null)
        {
            _hint.Text = "Choisissez un modèle dans la liste.";
            _hint.ForeColor = EditorChrome.LabelPrimary;
            return;
        }

        Choice = choice;
        DialogResult = DialogResult.OK;
        Close();
    }

    private static Button MakeButton(string text, bool primary)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            MinimumSize = new Size(120, 34),
            Margin = new Padding(8, 0, 0, 0),
        };
        EditorChrome.StyleDialogButton(button, primary);
        return button;
    }
}
