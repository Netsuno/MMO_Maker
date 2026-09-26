using System.Drawing;
using System.Windows.Forms;
using Frog.Application.Maps;
using Frog.Core.Models;
using Frog.Editor.Ui;

namespace Frog.Editor.Dialogs;

/// <summary>
/// Taille et décalage du contenu (menu Carte), sur le modèle du changement de taille VX :
/// le contenu existant se place dans le nouveau rectangle, le surplus est coupé.
/// </summary>
internal sealed class MapResizeShiftDialog : Form
{
    private readonly NumericUpDown _numW;
    private readonly NumericUpDown _numH;
    private readonly NumericUpDown _numDx;
    private readonly NumericUpDown _numDy;
    private readonly Label _identity;

    public MapResizeShiftDialog(Map map)
    {
        ArgumentNullException.ThrowIfNull(map);
        Text = MapResizeShift.DialogTitle;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        MinimumSize = new Size(520, 460);
        ClientSize = new Size(560, 440);
        AutoScaleMode = AutoScaleMode.Dpi;
        EditorChrome.ApplyFormChrome(this);

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 7,
            Padding = new Padding(20, 16, 20, 14),
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 200f));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 40f));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52f));

        _numW = MakeNumber(map.Width);
        _numH = MakeNumber(map.Height);
        _numDx = MakeDelta();
        _numDy = MakeDelta();
        _identity = new Label
        {
            Text = MapEditOperations.FormatGraphicIdentity(map)
                   + " — inchangée. Les TileAssetId ne sont pas réécrits.",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = EditorChrome.LabelMuted,
            AutoSize = false,
            Margin = new Padding(10, 4, 0, 0),
        };
        var hint = new Label
        {
            Text = "Décalage positif : le contenu va vers la droite et le bas, puis il est coupé aux nouvelles limites. "
                   + "Tuiles, événements, entités, prefabs et départ suivent. Les destinations de warp restent celles de la carte cible. "
                   + "Ctrl+Z restaure ce paquet (tuiles, ancres, événements de l’opération).",
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.TopLeft,
            ForeColor = EditorChrome.LabelMuted,
            AutoSize = false,
            Margin = new Padding(0, 4, 0, 0),
        };

        AddRow(root, 0, "Largeur (tuiles)", _numW);
        AddRow(root, 1, "Hauteur (tuiles)", _numH);
        AddRow(root, 2, "Décalage X (tuiles)", _numDx);
        AddRow(root, 3, "Décalage Y (tuiles)", _numDy);
        var identityLabel = MakeLabel("Identité");
        root.Controls.Add(identityLabel, 0, 4);
        root.Controls.Add(_identity, 1, 4);
        root.SetColumnSpan(hint, 2);
        root.Controls.Add(hint, 0, 5);

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

    public MapResizeShiftEdit PendingEdit => new()
    {
        Width = (int)_numW.Value,
        Height = (int)_numH.Value,
        DeltaX = (int)_numDx.Value,
        DeltaY = (int)_numDy.Value,
    };

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

    internal int DeltaXForTest
    {
        get => (int)_numDx.Value;
        set => _numDx.Value = value;
    }

    internal int DeltaYForTest
    {
        get => (int)_numDy.Value;
        set => _numDy.Value = value;
    }

    internal string IdentityTextForTest => _identity.Text;

    internal string JoinedLabelsForTest
    {
        get
        {
            var parts = new System.Collections.Generic.List<string>();
            CollectText(this, parts);
            return string.Join('\n', parts);
        }
    }

    private static NumericUpDown MakeNumber(int value) => new()
    {
        Minimum = MapEditOperations.MinDimensionTiles,
        Maximum = MapEditOperations.MaxDimensionTiles,
        Value = Math.Clamp(value, MapEditOperations.MinDimensionTiles, MapEditOperations.MaxDimensionTiles),
        Dock = DockStyle.Fill,
        Margin = new Padding(10, 6, 0, 6),
        TextAlign = HorizontalAlignment.Right,
    };

    private static NumericUpDown MakeDelta() => new()
    {
        Minimum = MapResizeShift.MinDelta,
        Maximum = MapResizeShift.MaxDelta,
        Value = 0,
        Dock = DockStyle.Fill,
        Margin = new Padding(10, 6, 0, 6),
        TextAlign = HorizontalAlignment.Right,
    };

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
