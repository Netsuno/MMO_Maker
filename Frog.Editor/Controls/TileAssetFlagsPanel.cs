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
    private readonly Panel _autotileHost;
    private readonly TextBox _group;
    private readonly ComboBox _role;
    private readonly Button _applyAutotile;
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
        Height = 272;
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
            Height = 192,
            Padding = new Padding(8, 0, 8, 0),
            BackColor = EditorChrome.SidebarBg,
        };
        AddMode(modes, TileAssetFlagLabels.Autotile, TileFlagEditMode.Autotile);
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

        _group = new TextBox { Width = 110, Margin = new Padding(0, 2, 6, 0) };
        _role = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 148,
            Margin = new Padding(0, 2, 6, 0),
        };
        EditorChrome.StyleSidebarComboBox(_role);
        foreach (AutotileRole role in Enum.GetValues<AutotileRole>())
        {
            _role.Items.Add(new RoleItem(role));
        }

        _role.SelectedIndex = 0;
        _applyAutotile = new Button
        {
            Text = TileAssetFlagLabels.AutotileApply,
            AutoSize = true,
            Margin = new Padding(0, 2, 0, 0),
        };
        EditorChrome.StyleDialogButton(_applyAutotile, primary: true);
        _applyAutotile.Click += (_, _) => CommitAutotile();
        var groupLabel = new Label
        {
            Text = TileAssetFlagLabels.AutotileGroup,
            AutoSize = true,
            ForeColor = EditorChrome.LabelMuted,
            Margin = new Padding(0, 6, 4, 0),
        };
        var fields = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 30,
            WrapContents = false,
            BackColor = EditorChrome.SidebarBg,
        };
        fields.Controls.Add(groupLabel);
        fields.Controls.Add(_group);
        fields.Controls.Add(_role);
        fields.Controls.Add(_applyAutotile);
        _autotileHost = new Panel
        {
            Dock = DockStyle.Top,
            Height = 34,
            Visible = false,
            BackColor = EditorChrome.SidebarBg,
            Padding = new Padding(8, 0, 8, 0),
        };
        _autotileHost.Controls.Add(fields);

        _hint = new Label
        {
            Dock = DockStyle.Top,
            Height = 32,
            ForeColor = EditorChrome.LabelMuted,
            Padding = new Padding(8, 2, 8, 0),
            Text = TileAssetFlagLabels.Hint(TileFlagEditMode.PassageGlobal),
        };

        Controls.Add(_autotileHost);
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
        _group.Text = flags.AutotileGroup ?? string.Empty;
        SelectRole(flags.AutotileRole);
        _group.Enabled = enabled;
        _role.Enabled = enabled;
        _applyAutotile.Enabled = enabled;
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
        _autotileHost.Visible = mode == TileFlagEditMode.Autotile;
        Height = mode == TileFlagEditMode.Autotile ? 310 : 272;
        _hint.Text = TileAssetFlagLabels.Hint(mode);
        ModeChanged?.Invoke();
    }

    public void SelectAutotileMode() => SelectMode(TileFlagEditMode.Autotile);

    internal string AutotileModeTextForTest => _modes[TileFlagEditMode.Autotile].Text;

    internal string ApplyAutotileTextForTest => _applyAutotile.Text;

    internal bool AutotileEditorVisibleForTest => _autotileHost.Visible;

    internal string? ApplyAutotileForTest(string group, AutotileRole role)
    {
        _group.Text = group;
        SelectRole(role);
        CommitAutotile();
        return _catalogue.GetFlags(_id).AutotileRole == role
               && string.Equals(_catalogue.GetFlags(_id).AutotileGroup, group.Trim(), StringComparison.Ordinal)
            ? null
            : _which.Text;
    }

    private void CommitAutotile()
    {
        if (_suspend || _id.IsNone)
        {
            return;
        }

        var role = (_role.SelectedItem as RoleItem)?.Role ?? AutotileRole.None;
        TileAssetFlags next;
        try
        {
            next = _catalogue.GetFlags(_id).WithAutotile(_group.Text, role);
        }
        catch (ArgumentException ex)
        {
            _which.Text = ex.Message;
            return;
        }

        Apply(next);
    }

    private void SelectRole(AutotileRole role)
    {
        for (var i = 0; i < _role.Items.Count; i++)
        {
            if (_role.Items[i] is RoleItem item && item.Role == role)
            {
                _role.SelectedIndex = i;
                return;
            }
        }

        _role.SelectedIndex = 0;
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

    private sealed class RoleItem
    {
        public RoleItem(AutotileRole role) => Role = role;

        public AutotileRole Role { get; }

        public override string ToString() => TileAssetFlagLabels.RoleLabel(Role);
    }
}
