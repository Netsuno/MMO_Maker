using System.Windows.Forms;

using Frog.Core.Maps;
using Frog.Editor.Services;
using Frog.Editor.Ui;

namespace Frog.Editor.Controls;

/// <summary>
/// Modes du tileset, dans l’ordre des boutons de la base VX (F9).
/// Le mode actif se lit sur les vignettes 48×48. Aucun asset ni rvdata VX.
/// </summary>
internal sealed class TileAssetFlagsPanel : UserControl
{
    private static readonly Color SelectedBack = Color.FromArgb(236, 240, 246);
    private static readonly Color SelectedFore = Color.FromArgb(28, 32, 40);

    private readonly TileAssetCatalogue _catalogue;
    private readonly Label _which;
    private readonly Label _hint;
    private readonly FlowLayoutPanel _directions;
    private readonly CheckBox _north;
    private readonly CheckBox _south;
    private readonly CheckBox _east;
    private readonly CheckBox _west;
    private readonly Dictionary<TileFlagEditMode, Button> _modes = new();
    private bool _suspend;
    private TileAssetId _id;

    public TileAssetFlagsPanel(TileAssetCatalogue catalogue)
    {
        _catalogue = catalogue ?? throw new ArgumentNullException(nameof(catalogue));
        Dock = DockStyle.Bottom;
        Height = 248;
        AutoScroll = true;
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

        var modes = new Panel
        {
            Dock = DockStyle.Top,
            Height = 168,
            Padding = new Padding(8, 0, 8, 0),
            BackColor = EditorChrome.SidebarBg,
        };
        AddMode(modes, TileAssetFlagLabels.Terrain, TileFlagEditMode.Terrain);
        AddMode(modes, TileAssetFlagLabels.Damage, TileFlagEditMode.Damage);
        AddMode(modes, TileAssetFlagLabels.Counter, TileFlagEditMode.Counter);
        AddMode(modes, TileAssetFlagLabels.Bush, TileFlagEditMode.Bush);
        AddMode(modes, TileAssetFlagLabels.Priority, TileFlagEditMode.Priority);
        AddMode(modes, TileAssetFlagLabels.PassageFour, TileFlagEditMode.PassageFourDirections);
        AddMode(modes, TileAssetFlagLabels.PassageGlobal, TileFlagEditMode.PassageGlobal);

        _north = DirectionBox(TileAssetFlagLabels.North);
        _south = DirectionBox(TileAssetFlagLabels.South);
        _east = DirectionBox(TileAssetFlagLabels.East);
        _west = DirectionBox(TileAssetFlagLabels.West);
        _directions = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 26,
            WrapContents = false,
            Visible = false,
            Padding = new Padding(8, 0, 8, 0),
            BackColor = EditorChrome.SidebarBg,
        };
        _directions.Controls.Add(_north);
        _directions.Controls.Add(_south);
        _directions.Controls.Add(_east);
        _directions.Controls.Add(_west);

        _hint = new Label
        {
            Dock = DockStyle.Top,
            Height = 32,
            ForeColor = EditorChrome.LabelMuted,
            Padding = new Padding(8, 2, 8, 0),
            Text = TileAssetFlagLabels.Hint(TileFlagEditMode.PassageGlobal),
        };

        Controls.Add(_hint);
        Controls.Add(_directions);
        Controls.Add(modes);
        Controls.Add(_which);
        Controls.Add(banner);
        SelectMode(TileFlagEditMode.PassageGlobal);
        Bind(TileAssetId.None);
    }

    public TileFlagEditMode Mode { get; private set; } = TileFlagEditMode.PassageGlobal;

    public event Action? ModeChanged;

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
        _which.Text = enabled ? Short(id) : TileAssetFlagLabels.Empty;
        SetDirectionsEnabled(enabled);
        _suspend = false;
    }

    /// <summary>Clic dans la vignette 48×48 : applique le mode actif.</summary>
    public void ApplyClick(TileAssetId id, int localX, int localY)
    {
        if (id.IsNone || !_catalogue.TryGet(id, out _))
        {
            return;
        }

        _id = id;
        var current = _catalogue.GetFlags(id);
        var next = TileFlagEdit.Apply(current, Mode, localX, localY);
        if (next.Equals(current))
        {
            Bind(id);
            return;
        }

        Apply(next);
    }

    private void SelectMode(TileFlagEditMode mode)
    {
        Mode = mode;
        foreach (var pair in _modes)
        {
            var selected = pair.Key == mode;
            pair.Value.BackColor = selected ? SelectedBack : EditorChrome.SidebarElevated;
            pair.Value.ForeColor = selected ? SelectedFore : EditorChrome.LabelPrimary;
        }

        _directions.Visible = mode == TileFlagEditMode.PassageFourDirections;
        _hint.Text = TileAssetFlagLabels.Hint(mode);
        ModeChanged?.Invoke();
    }

    private void CommitDirections()
    {
        if (_suspend || _id.IsNone || Mode != TileFlagEditMode.PassageFourDirections)
        {
            return;
        }

        var current = _catalogue.GetFlags(_id);
        Apply(current with
        {
            PassageNorth = _north.Checked,
            PassageSouth = _south.Checked,
            PassageEast = _east.Checked,
            PassageWest = _west.Checked,
            Star = false,
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

    private void AddMode(Control host, string text, TileFlagEditMode mode)
    {
        var button = new Button
        {
            Text = text,
            Dock = DockStyle.Top,
            Height = 24,
            TextAlign = ContentAlignment.MiddleCenter,
            FlatStyle = FlatStyle.Flat,
            Cursor = Cursors.Hand,
            Tag = mode,
        };
        button.FlatAppearance.BorderSize = 1;
        button.FlatAppearance.BorderColor = Color.FromArgb(88, 92, 103);
        button.Click += (_, _) => SelectMode(mode);
        _modes[mode] = button;
        host.Controls.Add(button);
    }

    private void SetDirectionsEnabled(bool enabled)
    {
        _north.Enabled = enabled;
        _south.Enabled = enabled;
        _east.Enabled = enabled;
        _west.Enabled = enabled;
    }

    private CheckBox DirectionBox(string text)
    {
        var box = new CheckBox
        {
            Text = text,
            AutoSize = true,
            Checked = true,
            ForeColor = EditorChrome.LabelPrimary,
            Margin = new Padding(0, 2, 8, 0),
        };
        box.CheckedChanged += (_, _) => CommitDirections();
        return box;
    }

    private static string Short(TileAssetId id)
    {
        var hex = id.ToHex();
        return hex.Length <= 16 ? hex : hex[..16] + "…";
    }
}
