using System.Drawing;
using System.Windows.Forms;
using Frog.Application.Maps;
using Frog.Core.Models;
using Frog.Editor.Ui;

namespace Frog.Editor.Dialogs;

/// <summary>
/// Propriétés de la carte déjà présentes sur le modèle : nom, taille, chevauchement.
/// L’identité graphique est affichée, pas éditée. Le départ playtest est une mémo locale.
/// </summary>
internal sealed class MapPropertiesDialog : Form
{
    private readonly TextBox _txtName;
    private readonly NumericUpDown _numW;
    private readonly NumericUpDown _numH;
    private readonly CheckBox _overlap;
    private readonly Label _identity;
    private readonly Label _spawn;

    public MapPropertiesDialog(Map map, Point? playtestSpawn)
    {
        ArgumentNullException.ThrowIfNull(map);
        Text = "Propriétés de la carte";
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(480, 420);
        ClientSize = new Size(540, 400);
        AutoScaleMode = AutoScaleMode.Dpi;
        EditorChrome.ApplyFormChrome(this);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 7,
            Padding = new Padding(20, 18, 20, 16),
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 188f));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        for (var i = 0; i < 6; i++)
        {
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, (i is 4 or 5) ? 48f : 42f));
        }

        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56f));

        _txtName = new TextBox
        {
            Text = map.Name,
            Dock = DockStyle.Fill,
            Margin = new Padding(10, 6, 0, 6),
        };
        _numW = new NumericUpDown
        {
            Minimum = MapEditOperations.MinDimensionTiles,
            Maximum = MapEditOperations.MaxDimensionTiles,
            Value = Math.Clamp(map.Width, MapEditOperations.MinDimensionTiles, MapEditOperations.MaxDimensionTiles),
            Dock = DockStyle.Fill,
            Margin = new Padding(10, 6, 0, 6),
            TextAlign = HorizontalAlignment.Right,
        };
        _numH = new NumericUpDown
        {
            Minimum = MapEditOperations.MinDimensionTiles,
            Maximum = MapEditOperations.MaxDimensionTiles,
            Value = Math.Clamp(map.Height, MapEditOperations.MinDimensionTiles, MapEditOperations.MaxDimensionTiles),
            Dock = DockStyle.Fill,
            Margin = new Padding(10, 6, 0, 6),
            TextAlign = HorizontalAlignment.Right,
        };
        _overlap = new CheckBox
        {
            Text = "Autoriser plusieurs joueurs sur la même tuile",
            Checked = map.AllowPlayerOverlap,
            AutoSize = true,
            ForeColor = EditorChrome.LabelPrimary,
            Margin = new Padding(10, 10, 0, 0),
        };
        _identity = new Label
        {
            Text = MapEditOperations.FormatGraphicIdentity(map),
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = EditorChrome.LabelMuted,
            AutoSize = false,
            Margin = new Padding(10, 4, 0, 0),
        };
        _spawn = new Label
        {
            Text = MapEditOperations.FormatSpawnMemo(playtestSpawn?.X, playtestSpawn?.Y)
                   + " — mémo locale (outil D), absente du fichier carte.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = EditorChrome.LabelMuted,
            AutoSize = false,
            Margin = new Padding(10, 0, 0, 0),
        };

        AddRow(root, 0, "Nom", _txtName);
        AddRow(root, 1, "Largeur (tuiles)", _numW);
        AddRow(root, 2, "Hauteur (tuiles)", _numH);
        var overlapLabel = MakeLabel("Chevauchement");
        root.Controls.Add(overlapLabel, 0, 3);
        root.Controls.Add(_overlap, 1, 3);
        var identityLabel = MakeLabel("Identité");
        root.Controls.Add(identityLabel, 0, 4);
        root.Controls.Add(_identity, 1, 4);
        var spawnLabel = MakeLabel("Départ");
        root.Controls.Add(spawnLabel, 0, 5);
        root.Controls.Add(_spawn, 1, 5);

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill,
            WrapContents = false,
            Padding = new Padding(0, 8, 0, 0),
            Margin = new Padding(0),
        };
        var ok = new Button
        {
            Text = "Appliquer",
            DialogResult = DialogResult.OK,
            AutoSize = true,
            MinimumSize = new Size(108, 34),
            Margin = new Padding(10, 0, 0, 0),
        };
        var cancel = new Button
        {
            Text = "Annuler",
            DialogResult = DialogResult.Cancel,
            AutoSize = true,
            MinimumSize = new Size(108, 34),
        };
        EditorChrome.StyleDialogButton(ok, primary: true);
        EditorChrome.StyleDialogButton(cancel, primary: false);
        buttons.Controls.Add(ok);
        buttons.Controls.Add(cancel);
        root.SetColumnSpan(buttons, 2);
        root.Controls.Add(buttons, 0, 6);

        Controls.Add(root);
        AcceptButton = ok;
        CancelButton = cancel;
    }

    public MapPropertiesEdit PendingEdit => new()
    {
        Name = _txtName.Text,
        Width = (int)_numW.Value,
        Height = (int)_numH.Value,
        AllowPlayerOverlap = _overlap.Checked,
    };

    internal string NameTextForTest
    {
        get => _txtName.Text;
        set => _txtName.Text = value;
    }

    internal int WidthForTest
    {
        get => (int)_numW.Value;
        set => _numW.Value = value;
    }

    internal int HeightForTest
    {
        get => (int)_numH.Value;
        set => _numH.Value = value;
    }

    internal bool OverlapForTest
    {
        get => _overlap.Checked;
        set => _overlap.Checked = value;
    }

    internal string IdentityTextForTest => _identity.Text;

    internal string SpawnTextForTest => _spawn.Text;

    internal string JoinedLabelsForTest
    {
        get
        {
            var parts = new System.Collections.Generic.List<string>();
            CollectText(this, parts);
            return string.Join('\n', parts);
        }
    }

    private static void AddRow(TableLayoutPanel root, int row, string caption, Control editor)
    {
        root.Controls.Add(MakeLabel(caption), 0, row);
        root.Controls.Add(editor, 1, row);
    }

    private static Label MakeLabel(string caption) => new()
    {
        Text = caption,
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleLeft,
        ForeColor = EditorChrome.LabelPrimary,
        AutoSize = false,
    };

    private static void CollectText(Control parent, System.Collections.Generic.List<string> parts)
    {
        foreach (Control child in parent.Controls)
        {
            if (!string.IsNullOrWhiteSpace(child.Text))
            {
                parts.Add(child.Text);
            }

            if (child.HasChildren)
            {
                CollectText(child, parts);
            }
        }
    }
}
