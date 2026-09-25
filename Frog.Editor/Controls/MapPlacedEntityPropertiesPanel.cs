using System.Drawing;
using System.Windows.Forms;
using Frog.Application.Maps;
using Frog.Editor.Ui;

namespace Frog.Editor.Controls;

/// <summary>
/// Panneau de propriétés des entités posées (apparition, PNJ, objet).
/// Libellés français. Aucune écriture SQL : l’hôte persiste le mémo locale.
/// </summary>
internal sealed class MapPlacedEntityPropertiesPanel : UserControl
{
    private readonly ComboBox _kindToPlace;
    private readonly ListBox _roster;
    private readonly Label _selection;
    private readonly ComboBox _type;
    private readonly TextBox _name;
    private readonly TextBox _notes;
    private readonly ComboBox _facing;
    private readonly NumericUpDown _respawn;
    private readonly NumericUpDown _level;
    private readonly Label _error;
    private readonly Button _delete;
    private bool _suspend;
    private bool _applying;

    public event EventHandler? PlaceKindChanged;

    public event EventHandler? Edited;

    public event EventHandler? DeleteRequested;

    public event EventHandler<Guid>? RosterPicked;

    public MapPlacedEntityPropertiesPanel()
    {
        Height = 320;
        MinimumSize = new Size(0, 320);
        BackColor = EditorChrome.SidebarBg;
        Padding = new Padding(8, 4, 8, 6);
        Font = EditorChrome.BodyFont;

        var title = new Label
        {
            Text = "ENTITÉS",
            Dock = DockStyle.Top,
            Height = 22,
            ForeColor = EditorChrome.LabelMuted,
            BackColor = EditorChrome.SidebarBg,
            Font = EditorChrome.CaptionFont,
            TextAlign = ContentAlignment.MiddleLeft,
        };

        var body = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 9,
            BackColor = EditorChrome.SidebarBg,
            Padding = new Padding(0, 2, 0, 0),
        };
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 118f));
        body.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 52f));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 18f));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 26f));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 26f));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 26f));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 26f));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 26f));
        body.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));

        _kindToPlace = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        EditorChrome.StyleSidebarComboBox(_kindToPlace);
        _kindToPlace.Items.Add(new KindChoice(MapPlacedKind.Spawn, "Apparition"));
        _kindToPlace.Items.Add(new KindChoice(MapPlacedKind.Npc, "PNJ"));
        _kindToPlace.Items.Add(new KindChoice(MapPlacedKind.Object, "Objet"));
        _kindToPlace.SelectedIndex = 1;
        _kindToPlace.SelectedIndexChanged += (_, _) =>
        {
            if (_suspend)
            {
                return;
            }

            PlaceKindChanged?.Invoke(this, EventArgs.Empty);
        };

        _roster = new ListBox { Dock = DockStyle.Fill, IntegralHeight = false };
        EditorChrome.StyleSidebarList(_roster);
        _roster.SelectedIndexChanged += (_, _) =>
        {
            if (_suspend || _roster.SelectedItem is not RosterRow row)
            {
                return;
            }

            RosterPicked?.Invoke(this, row.Id);
        };

        _selection = new Label
        {
            Text = "Aucune sélection.",
            Dock = DockStyle.Fill,
            ForeColor = EditorChrome.LabelMuted,
            BackColor = EditorChrome.SidebarBg,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
        };

        _type = BuildKindCombo();
        _name = BuildTextBox();
        _notes = BuildTextBox();
        _facing = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        EditorChrome.StyleSidebarComboBox(_facing);
        _facing.Items.Add(new FacingChoice(MapPlacedFacing.South, "Sud"));
        _facing.Items.Add(new FacingChoice(MapPlacedFacing.West, "Ouest"));
        _facing.Items.Add(new FacingChoice(MapPlacedFacing.East, "Est"));
        _facing.Items.Add(new FacingChoice(MapPlacedFacing.North, "Nord"));
        _facing.SelectedIndex = 0;
        _facing.SelectedIndexChanged += (_, _) => RaiseEdited();

        _respawn = BuildNumber(0, MapPlacedEntityEdit.MaxRespawnSeconds);
        _level = BuildNumber(MapPlacedEntityEdit.MinLevel, MapPlacedEntityEdit.MaxLevel);
        _type.SelectedIndexChanged += (_, _) =>
        {
            RefreshFieldAccess();
            RaiseEdited();
        };
        _name.TextChanged += (_, _) => RaiseEdited();
        _notes.TextChanged += (_, _) => RaiseEdited();

        _error = new Label
        {
            Text = string.Empty,
            Dock = DockStyle.Fill,
            ForeColor = Color.FromArgb(232, 140, 140),
            BackColor = EditorChrome.SidebarBg,
            TextAlign = ContentAlignment.MiddleLeft,
            AutoEllipsis = true,
        };
        _delete = new Button { Text = "Supprimer", Dock = DockStyle.Fill, Height = 28 };
        EditorChrome.StyleDialogButton(_delete, primary: false);
        _delete.Click += (_, _) => DeleteRequested?.Invoke(this, EventArgs.Empty);

        AddRow(body, 0, "À poser", _kindToPlace);
        body.Controls.Add(_roster, 0, 1);
        body.SetColumnSpan(_roster, 2);
        body.Controls.Add(_selection, 0, 2);
        body.SetColumnSpan(_selection, 2);
        AddRow(body, 3, "Type", _type);
        AddRow(body, 4, "Nom", _name);
        AddRow(body, 5, "Notes", _notes);
        AddRow(body, 6, "Orientation", _facing);
        AddRow(body, 7, "Réapparition (s)", _respawn);
        var levelHost = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            RowCount = 1,
            BackColor = EditorChrome.SidebarBg,
            Margin = new Padding(0),
        };
        levelHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55f));
        levelHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45f));
        levelHost.Controls.Add(_level, 0, 0);
        levelHost.Controls.Add(_delete, 1, 0);
        AddRow(body, 8, "Niveau", levelHost);
        _error.Dock = DockStyle.Bottom;
        _error.Height = 18;

        Controls.Add(body);
        Controls.Add(_error);
        Controls.Add(title);
        SetFieldsEnabled(false);
    }

    public MapPlacedKind KindToPlace
    {
        get => _kindToPlace.SelectedItem is KindChoice choice ? choice.Kind : MapPlacedKind.Npc;
    }

    public void Sync(IReadOnlyList<MapPlacedEntity> entities, MapPlacedEntity? selected, MapPlacedKind kindToPlace)
    {
        ArgumentNullException.ThrowIfNull(entities);
        _suspend = true;
        try
        {
            SelectKind(_kindToPlace, kindToPlace);
            if (_applying && selected is not null)
            {
                for (var i = 0; i < _roster.Items.Count; i++)
                {
                    if (_roster.Items[i] is RosterRow row && row.Id == selected.Id)
                    {
                        _roster.Items[i] = new RosterRow(selected.Id, FormatRoster(selected));
                        _roster.SelectedIndex = i;
                        break;
                    }
                }

                _selection.Text = MapPlacedEntityEdit.FormatSummary(selected);
                RefreshFieldAccess();
                return;
            }

            var keepId = selected?.Id;
            var selectedIndex = -1;
            _roster.BeginUpdate();
            try
            {
                _roster.Items.Clear();
                for (var i = 0; i < entities.Count; i++)
                {
                    var entity = entities[i];
                    _roster.Items.Add(new RosterRow(entity.Id, FormatRoster(entity)));
                    if (keepId == entity.Id)
                    {
                        selectedIndex = i;
                    }
                }
            }
            finally
            {
                _roster.EndUpdate();
            }

            if (selectedIndex >= 0)
            {
                _roster.SelectedIndex = selectedIndex;
            }

            if (selected is null)
            {
                _selection.Text = "Aucune sélection. Cliquez la carte pour poser.";
                _name.Text = string.Empty;
                _notes.Text = string.Empty;
                _facing.SelectedIndex = 0;
                _respawn.Value = 0;
                _level.Value = MapPlacedEntityEdit.MinLevel;
                SelectKind(_type, MapPlacedKind.Npc);
                _error.Text = string.Empty;
                SetFieldsEnabled(false);
                return;
            }

            _selection.Text = MapPlacedEntityEdit.FormatSummary(selected);
            SelectKind(_type, selected.Kind);
            _name.Text = selected.Name;
            _notes.Text = selected.Notes;
            SelectFacing(selected.Facing);
            _respawn.Value = Math.Clamp(selected.RespawnSeconds, 0, MapPlacedEntityEdit.MaxRespawnSeconds);
            _level.Value = Math.Clamp(selected.Level, MapPlacedEntityEdit.MinLevel, MapPlacedEntityEdit.MaxLevel);
            _error.Text = string.Empty;
            SetFieldsEnabled(true);
            RefreshFieldAccess();
        }
        finally
        {
            _suspend = false;
        }
    }

    public void ShowError(string? message)
    {
        _error.Text = message ?? string.Empty;
    }

    public bool TryReadEdit(
        out MapPlacedKind kind,
        out string name,
        out string notes,
        out MapPlacedFacing facing,
        out int respawnSeconds,
        out int level)
    {
        kind = _type.SelectedItem is KindChoice choice ? choice.Kind : MapPlacedKind.Npc;
        name = _name.Text;
        notes = _notes.Text;
        facing = _facing.SelectedItem is FacingChoice face ? face.Facing : MapPlacedFacing.South;
        respawnSeconds = (int)_respawn.Value;
        level = (int)_level.Value;
        return true;
    }

    internal string SelectionTextForTest => _selection.Text;

    internal string NameTextForTest => _name.Text;

    internal string DeleteButtonTextForTest => _delete.Text;

    internal int RosterCountForTest => _roster.Items.Count;

    internal bool RespawnEnabledForTest => _respawn.Enabled;

    internal bool LevelEnabledForTest => _level.Enabled;

    internal MapPlacedKind KindToPlaceForTest => KindToPlace;

    internal void SetKindToPlaceForTest(MapPlacedKind kind)
    {
        if (KindToPlace == kind)
        {
            PlaceKindChanged?.Invoke(this, EventArgs.Empty);
            return;
        }

        SelectKind(_kindToPlace, kind);
    }

    private void RaiseEdited()
    {
        if (_suspend || !_name.Enabled)
        {
            return;
        }

        _applying = true;
        try
        {
            Edited?.Invoke(this, EventArgs.Empty);
        }
        finally
        {
            _applying = false;
        }
    }

    private void RefreshFieldAccess()
    {
        if (!_name.Enabled)
        {
            _respawn.Enabled = false;
            _level.Enabled = false;
            return;
        }

        var kind = _type.SelectedItem is KindChoice choice ? choice.Kind : MapPlacedKind.Npc;
        _respawn.Enabled = kind == MapPlacedKind.Spawn;
        _level.Enabled = kind == MapPlacedKind.Npc;
    }

    private void SetFieldsEnabled(bool enabled)
    {
        _type.Enabled = enabled;
        _name.Enabled = enabled;
        _notes.Enabled = enabled;
        _facing.Enabled = enabled;
        _delete.Enabled = enabled;
        if (!enabled)
        {
            _respawn.Enabled = false;
            _level.Enabled = false;
        }
    }

    private static void SelectKind(ComboBox combo, MapPlacedKind kind)
    {
        for (var i = 0; i < combo.Items.Count; i++)
        {
            if (combo.Items[i] is KindChoice choice && choice.Kind == kind)
            {
                combo.SelectedIndex = i;
                return;
            }
        }
    }

    private void SelectFacing(MapPlacedFacing facing)
    {
        for (var i = 0; i < _facing.Items.Count; i++)
        {
            if (_facing.Items[i] is FacingChoice choice && choice.Facing == facing)
            {
                _facing.SelectedIndex = i;
                return;
            }
        }
    }

    private static ComboBox BuildKindCombo()
    {
        var combo = new ComboBox { Dock = DockStyle.Fill, DropDownStyle = ComboBoxStyle.DropDownList };
        EditorChrome.StyleSidebarComboBox(combo);
        combo.Items.Add(new KindChoice(MapPlacedKind.Spawn, "Apparition"));
        combo.Items.Add(new KindChoice(MapPlacedKind.Npc, "PNJ"));
        combo.Items.Add(new KindChoice(MapPlacedKind.Object, "Objet"));
        combo.SelectedIndex = 1;
        return combo;
    }

    private static TextBox BuildTextBox()
    {
        var box = new TextBox
        {
            Dock = DockStyle.Fill,
            BorderStyle = BorderStyle.FixedSingle,
            BackColor = EditorChrome.SidebarElevated,
            ForeColor = EditorChrome.LabelPrimary,
            Font = EditorChrome.BodyFont,
        };
        return box;
    }

    private NumericUpDown BuildNumber(int min, int max)
    {
        var number = new NumericUpDown
        {
            Minimum = min,
            Maximum = max,
            Dock = DockStyle.Fill,
            BackColor = EditorChrome.SidebarElevated,
            ForeColor = EditorChrome.LabelPrimary,
            BorderStyle = BorderStyle.FixedSingle,
            TextAlign = HorizontalAlignment.Right,
        };
        number.ValueChanged += (_, _) => RaiseEdited();
        return number;
    }

    private static void AddRow(TableLayoutPanel body, int row, string caption, Control editor)
    {
        var label = new Label
        {
            Text = caption,
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            ForeColor = EditorChrome.LabelMuted,
            BackColor = EditorChrome.SidebarBg,
            Margin = new Padding(0, 4, 4, 0),
        };
        editor.Margin = new Padding(0, 2, 0, 0);
        body.Controls.Add(label, 0, row);
        body.Controls.Add(editor, 1, row);
    }

    private static string FormatRoster(MapPlacedEntity entity) =>
        $"{MapPlacedEntityEdit.KindLabel(entity.Kind)} · {entity.Name} ({entity.TileX}, {entity.TileY})";

    private sealed record KindChoice(MapPlacedKind Kind, string Label)
    {
        public override string ToString() => Label;
    }

    private sealed record FacingChoice(MapPlacedFacing Facing, string Label)
    {
        public override string ToString() => Label;
    }

    private sealed record RosterRow(Guid Id, string Label)
    {
        public override string ToString() => Label;
    }
}
