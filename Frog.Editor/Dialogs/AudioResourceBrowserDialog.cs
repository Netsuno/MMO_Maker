using System.Drawing;
using System.Windows.Forms;
using Frog.Application.Assets;
using Frog.Editor.Ui;

namespace Frog.Editor.Dialogs;

/// <summary>Liste les musiques (BGM) ou sons (SE) du projet et renvoie le chemin relatif stocké.</summary>
internal sealed class AudioResourceBrowserDialog : Form
{
    internal const string EmptyHint = "Aucun fichier audio dans les dossiers du projet.";

    private readonly ListBox _list;
    private readonly Label _hint;
    private readonly AudioResourceKind _kind;
    private readonly AudioResourceEntry[] _entries;

    public AudioResourceBrowserDialog(
        string title,
        AudioResourceKind kind,
        IReadOnlyList<AudioResourceEntry> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        _kind = kind;
        _entries = entries.ToArray();
        Text = string.IsNullOrWhiteSpace(title) ? "Choisir un fichier audio" : title;
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        StartPosition = FormStartPosition.CenterParent;
        ClientSize = new Size(560, 420);
        MinimumSize = new Size(480, 320);
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
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 64f));
        root.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
        root.RowStyles.Add(new RowStyle(SizeType.Absolute, 52f));

        _hint = new Label
        {
            Text = _entries.Length == 0 ? HintFor(kind) + " " + EmptyHint : HintFor(kind),
            Dock = DockStyle.Fill,
            ForeColor = EditorChrome.LabelMuted,
            AutoSize = false,
        };
        _list = new ListBox
        {
            Dock = DockStyle.Fill,
            IntegralHeight = false,
        };
        foreach (var entry in _entries)
        {
            _list.Items.Add(entry);
        }

        if (_entries.Length > 0)
        {
            _list.SelectedIndex = 0;
        }

        _list.DoubleClick += (_, _) => AcceptSelection();

        var buttons = new FlowLayoutPanel
        {
            FlowDirection = FlowDirection.RightToLeft,
            Dock = DockStyle.Fill,
            WrapContents = false,
            Padding = new Padding(0, 8, 0, 0),
        };
        var choose = MakeButton("Choisir", primary: true);
        var cancel = MakeButton("Annuler", primary: false);
        choose.Click += (_, _) => AcceptSelection();
        cancel.Click += (_, _) =>
        {
            AcceptedAsset = null;
            DialogResult = DialogResult.Cancel;
            Close();
        };
        buttons.Controls.Add(choose);
        buttons.Controls.Add(cancel);

        root.Controls.Add(_hint, 0, 0);
        root.Controls.Add(_list, 0, 1);
        root.Controls.Add(buttons, 0, 2);
        Controls.Add(root);
        AcceptButton = choose;
        CancelButton = cancel;
    }

    internal string? AcceptedAsset { get; private set; }

    internal string HintForTest => _hint.Text;

    internal string JoinedLabelsForTest
    {
        get
        {
            var parts = new List<string>();
            CollectText(this, parts);
            return string.Join('\n', parts);
        }
    }

    internal int IndexOfStoredForTest(string stored)
    {
        for (var i = 0; i < _entries.Length; i++)
        {
            if (string.Equals(_entries[i].StoredAsset, stored, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    internal void SelectIndexForTest(int index)
    {
        if ((uint)index < (uint)_entries.Length)
        {
            _list.SelectedIndex = index;
        }
    }

    internal string? SelectedStoredForTest
        => _list.SelectedIndex >= 0 && _list.SelectedIndex < _entries.Length
            ? _entries[_list.SelectedIndex].StoredAsset
            : null;

    internal void AcceptForTest() => AcceptSelection();

    private void AcceptSelection()
    {
        if (_list.SelectedIndex < 0 || _list.SelectedIndex >= _entries.Length)
        {
            _hint.Text = _entries.Length == 0
                ? HintFor(_kind) + " " + EmptyHint
                : "Choisissez un fichier dans la liste.";
            _hint.ForeColor = EditorChrome.LabelPrimary;
            return;
        }

        AcceptedAsset = _entries[_list.SelectedIndex].StoredAsset;
        DialogResult = DialogResult.OK;
        Close();
    }

    private static string HintFor(AudioResourceKind kind)
        => kind == AudioResourceKind.Se
            ? "Sons du projet : Audio/SE et Assets/Audio."
            : "Musiques du projet : Audio/BGM et Assets/Audio.";

    private static Button MakeButton(string text, bool primary)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            MinimumSize = new Size(108, 34),
            Margin = new Padding(8, 0, 0, 0),
        };
        EditorChrome.StyleDialogButton(button, primary);
        return button;
    }

    private static void CollectText(Control parent, List<string> parts)
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
