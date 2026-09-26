using System.Windows.Forms;

using Frog.Core.Maps;
using Frog.Editor.Services;
using Frog.Editor.Ui;

namespace Frog.Editor.Controls;

/// <summary>
/// Panneau mode tileset : passage 4 directions, priorité, buisson, comptoir, dégâts.
/// Une tuile du catalogue (48×48) partage ses drapeaux partout où son <see cref="TileAssetId"/> est posé.
/// </summary>
internal sealed class TileAssetFlagsPanel : UserControl
{
    private readonly TileAssetCatalogue _catalogue;
    private readonly Label _which;
    private readonly CheckBox _north;
    private readonly CheckBox _south;
    private readonly CheckBox _east;
    private readonly CheckBox _west;
    private readonly NumericUpDown _priority;
    private readonly CheckBox _bush;
    private readonly CheckBox _counter;
    private readonly CheckBox _damage;
    private readonly Label _hint;
    private bool _suspend;
    private TileAssetId _id;

    public TileAssetFlagsPanel(TileAssetCatalogue catalogue)
    {
        _catalogue = catalogue ?? throw new ArgumentNullException(nameof(catalogue));
        Dock = DockStyle.Bottom;
        Height = 156;
        BackColor = EditorChrome.SidebarBg;
        Font = EditorChrome.BodyFont;
        ForeColor = EditorChrome.LabelPrimary;

        var banner = EditorChrome.BuildZoneBanner(TileAssetFlagLabels.PanelTitle);
        _which = new Label
        {
            Dock = DockStyle.Top,
            Height = 18,
            ForeColor = EditorChrome.LabelMuted,
            Padding = new Padding(8, 0, 8, 0),
            Text = TileAssetFlagLabels.Empty,
        };

        _north = PassageBox(TileAssetFlagLabels.North);
        _south = PassageBox(TileAssetFlagLabels.South);
        _east = PassageBox(TileAssetFlagLabels.East);
        _west = PassageBox(TileAssetFlagLabels.West);
        var passage = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 26,
            WrapContents = false,
            Padding = new Padding(8, 0, 8, 0),
            BackColor = EditorChrome.SidebarBg,
        };
        var passageLabel = new Label
        {
            Text = TileAssetFlagLabels.Passage,
            AutoSize = true,
            ForeColor = EditorChrome.LabelPrimary,
            Margin = new Padding(0, 4, 8, 0),
        };
        passage.Controls.Add(passageLabel);
        passage.Controls.Add(_north);
        passage.Controls.Add(_south);
        passage.Controls.Add(_east);
        passage.Controls.Add(_west);

        _priority = new NumericUpDown
        {
            Minimum = 0,
            Maximum = TileAssetFlags.MaxPriority,
            Width = 48,
            Margin = new Padding(0, 2, 0, 0),
        };
        _bush = MarkBox(TileAssetFlagLabels.Bush);
        _counter = MarkBox(TileAssetFlagLabels.Counter);
        _damage = MarkBox(TileAssetFlagLabels.Damage);
        var commands = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 30,
            WrapContents = false,
            Padding = new Padding(8, 0, 8, 0),
            BackColor = EditorChrome.SidebarBg,
        };
        var open = SmallButton(TileAssetFlagLabels.OpenAll);
        open.Click += (_, _) => Apply(TileAssetFlags.Default with
        {
            Priority = (byte)_priority.Value,
            Bush = _bush.Checked,
            Counter = _counter.Checked,
            Damage = _damage.Checked,
        });
        var block = SmallButton(TileAssetFlagLabels.BlockAll);
        block.Click += (_, _) => Apply(TileAssetFlags.Blocked with
        {
            Priority = (byte)_priority.Value,
            Bush = _bush.Checked,
            Counter = _counter.Checked,
            Damage = _damage.Checked,
        });
        var priorityLabel = new Label
        {
            Text = TileAssetFlagLabels.Priority,
            AutoSize = true,
            ForeColor = EditorChrome.LabelPrimary,
            Margin = new Padding(8, 6, 4, 0),
        };
        _priority.ValueChanged += (_, _) => CommitFromControls();
        commands.Controls.Add(open);
        commands.Controls.Add(block);
        commands.Controls.Add(priorityLabel);
        commands.Controls.Add(_priority);

        var marks = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 24,
            WrapContents = false,
            Padding = new Padding(8, 0, 8, 0),
            BackColor = EditorChrome.SidebarBg,
        };
        marks.Controls.Add(_bush);
        marks.Controls.Add(_counter);
        marks.Controls.Add(_damage);

        _hint = new Label
        {
            Dock = DockStyle.Top,
            Height = 18,
            ForeColor = EditorChrome.LabelMuted,
            Padding = new Padding(8, 0, 8, 0),
            Text = TileAssetFlagLabels.Hint,
        };

        Controls.Add(_hint);
        Controls.Add(marks);
        Controls.Add(commands);
        Controls.Add(passage);
        Controls.Add(_which);
        Controls.Add(banner);
        Bind(TileAssetId.None);
    }

    public void Bind(TileAssetId id)
    {
        _id = id;
        _suspend = true;
        var enabled = !id.IsNone && _catalogue.TryGet(id, out _);
        var flags = enabled ? _catalogue.GetFlags(id) : TileAssetFlags.Default;
        _north.Checked = flags.PassageNorth;
        _south.Checked = flags.PassageSouth;
        _east.Checked = flags.PassageEast;
        _west.Checked = flags.PassageWest;
        _priority.Value = Math.Clamp((int)flags.Priority, 0, TileAssetFlags.MaxPriority);
        _bush.Checked = flags.Bush;
        _counter.Checked = flags.Counter;
        _damage.Checked = flags.Damage;
        _which.Text = enabled
            ? Short(id)
            : TileAssetFlagLabels.Empty;
        SetEnabled(enabled);
        _suspend = false;
    }

    private void CommitFromControls()
    {
        if (_suspend || _id.IsNone)
        {
            return;
        }

        Apply(new TileAssetFlags
        {
            PassageNorth = _north.Checked,
            PassageSouth = _south.Checked,
            PassageEast = _east.Checked,
            PassageWest = _west.Checked,
            Priority = (byte)_priority.Value,
            Bush = _bush.Checked,
            Counter = _counter.Checked,
            Damage = _damage.Checked,
        });
    }

    private void Apply(TileAssetFlags flags)
    {
        if (_id.IsNone)
        {
            return;
        }

        if (!_catalogue.TrySetFlags(_id, flags, out var error))
        {
            _which.Text = error ?? "Drapeaux refusés.";
            return;
        }

        Bind(_id);
    }

    private void SetEnabled(bool enabled)
    {
        _north.Enabled = enabled;
        _south.Enabled = enabled;
        _east.Enabled = enabled;
        _west.Enabled = enabled;
        _priority.Enabled = enabled;
        _bush.Enabled = enabled;
        _counter.Enabled = enabled;
        _damage.Enabled = enabled;
        foreach (Control control in Controls)
        {
            EnableButtons(control, enabled);
        }
    }

    private static void EnableButtons(Control control, bool enabled)
    {
        if (control is Button button)
        {
            button.Enabled = enabled;
        }

        foreach (Control child in control.Controls)
        {
            EnableButtons(child, enabled);
        }
    }

    private CheckBox PassageBox(string text)
    {
        var box = MarkBox(text);
        box.Checked = true;
        return box;
    }

    private CheckBox MarkBox(string text)
    {
        var box = new CheckBox
        {
            Text = text,
            AutoSize = true,
            ForeColor = EditorChrome.LabelPrimary,
            Margin = new Padding(0, 2, 8, 0),
        };
        box.CheckedChanged += (_, _) => CommitFromControls();
        return box;
    }

    private static Button SmallButton(string text)
    {
        var button = new Button { Text = text, AutoSize = true, Margin = new Padding(0, 0, 6, 0) };
        EditorChrome.StyleDialogButton(button, primary: false);
        return button;
    }

    private static string Short(TileAssetId id)
    {
        var hex = id.ToHex();
        return hex.Length <= 16 ? hex : hex[..16] + "…";
    }
}
