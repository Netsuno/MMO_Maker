using System.Drawing;
using System.Globalization;
using System.Text;
using System.Windows.Forms;

using Frog.Core.Maps;
using Frog.Editor.Services;
using Frog.Editor.Ui;

namespace Frog.Editor.Controls;

/// <summary>
/// Liste des zones et des monstres (quantité vivante, délai). Libellés français.
/// La persistance est celle de la carte, pas un mémo à part.
/// </summary>
internal sealed class MobSpawnZonesPanel : UserControl
{
    private readonly ListBox _zones;
    private readonly TextBox _name;
    private readonly Label _bounds;
    private readonly ListBox _entries;
    private readonly ComboBox _troops;
    private readonly TextBox _monsterId;
    private readonly NumericUpDown _quantity;
    private readonly NumericUpDown _respawn;
    private readonly Label _catalogHint;
    private readonly Label _error;
    private MobSpawnZoneDocument? _document;
    private bool _suspend;
    private IReadOnlyList<MapEncounterTroopChoice> _choices = Array.Empty<MapEncounterTroopChoice>();

    public MobSpawnZonesPanel()
    {
        Dock = DockStyle.Top;
        Height = 420;
        Visible = false;
        AutoScroll = true;
        BackColor = EditorChrome.SidebarBg;
        Font = EditorChrome.BodyFont;
        ForeColor = EditorChrome.LabelPrimary;

        var banner = EditorChrome.BuildZoneBanner(MobSpawnZoneLabels.PanelTitle);
        var hint = new Label
        {
            Dock = DockStyle.Top,
            Height = 48,
            ForeColor = EditorChrome.LabelMuted,
            Padding = new Padding(8, 2, 8, 0),
            Text = MobSpawnZoneLabels.Hint,
        };

        _zones = List();
        _zones.SelectedIndexChanged += (_, _) =>
        {
            if (_suspend || _zones.SelectedItem is not Row row)
            {
                return;
            }

            ZonePicked?.Invoke(this, row.Id);
        };

        _error = new Label
        {
            Dock = DockStyle.Top,
            Height = 32,
            ForeColor = Color.FromArgb(232, 140, 140),
            Padding = new Padding(8, 0, 8, 0),
            Text = string.Empty,
        };

        _name = Field(180);
        _name.TextChanged += (_, _) =>
        {
            if (_suspend)
            {
                return;
            }

            var zone = SelectedZone();
            if (zone is null)
            {
                return;
            }

            if (!MobSpawnZoneEdit.TryRename(zone, _name.Text, out var error))
            {
                _error.Text = error ?? string.Empty;
                return;
            }

            _error.Text = string.Empty;
            RefreshZoneRow(zone);
            Edited?.Invoke(this, EventArgs.Empty);
        };

        _bounds = new Label
        {
            AutoSize = true,
            ForeColor = EditorChrome.LabelMuted,
            Margin = new Padding(0, 6, 0, 0),
            Text = string.Empty,
        };

        _entries = List();
        _entries.Height = 72;
        _entries.SelectedIndexChanged += (_, _) => LoadSelectedEntry();

        _catalogHint = new Label
        {
            Dock = DockStyle.Top,
            Height = 32,
            ForeColor = EditorChrome.LabelMuted,
            Padding = new Padding(8, 2, 8, 0),
            Text = MobSpawnZoneLabels.NoCatalog,
        };

        _troops = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 180,
            Margin = new Padding(0, 2, 8, 0),
        };
        EditorChrome.StyleSidebarComboBox(_troops);
        _troops.SelectedIndexChanged += (_, _) => ApplyTroopChoice();

        _monsterId = Field(200);
        _quantity = Number(MobSpawnZoneDocument.MinQuantity, MobSpawnZoneDocument.MaxQuantity, 1);
        _respawn = Number(0, MobSpawnZoneDocument.MaxRespawnSeconds, 30);

        var add = ActionButton(MobSpawnZoneLabels.Add, true);
        var remove = ActionButton(MobSpawnZoneLabels.Remove, false);
        var deleteZone = ActionButton(MobSpawnZoneLabels.DeleteZone, false);
        var refresh = ActionButton(MobSpawnZoneLabels.RefreshCatalog, false);
        add.Click += (_, _) => AddEntry();
        remove.Click += (_, _) => RemoveEntry();
        deleteZone.Click += (_, _) => DeleteSelectedZone();
        refresh.Click += (_, _) => TroopsRefreshRequested?.Invoke(this, EventArgs.Empty);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 36,
            WrapContents = false,
            Padding = new Padding(8, 2, 8, 0),
            BackColor = EditorChrome.SidebarBg,
        };
        buttons.Controls.Add(add);
        buttons.Controls.Add(remove);
        buttons.Controls.Add(deleteZone);
        buttons.Controls.Add(refresh);

        Controls.Add(_error);
        Controls.Add(buttons);
        Controls.Add(LayoutRow(LabelOf(MobSpawnZoneLabels.Respawn), _respawn, LabelOf(MobSpawnZoneLabels.Quantity), _quantity));
        Controls.Add(LayoutRow(LabelOf(MobSpawnZoneLabels.Identifier), _monsterId));
        Controls.Add(LayoutRow(LabelOf(MobSpawnZoneLabels.Monster), _troops));
        Controls.Add(_catalogHint);
        Controls.Add(_entries);
        Controls.Add(Title(MobSpawnZoneLabels.Monsters));
        Controls.Add(LayoutRow(LabelOf(MobSpawnZoneLabels.Name), _name, _bounds));
        Controls.Add(_zones);
        Controls.Add(Title(MobSpawnZoneLabels.Zones));
        Controls.Add(hint);
        Controls.Add(banner);
        SetTroops(Array.Empty<MapEncounterTroopChoice>());
    }

    public event EventHandler<Guid>? ZonePicked;

    public event EventHandler? Edited;

    public event EventHandler? TroopsRefreshRequested;

    public void Bind(MobSpawnZoneDocument? document, Guid? selectedZoneId)
    {
        _document = document;
        _suspend = true;
        try
        {
            _zones.Items.Clear();
            var index = -1;
            if (document is not null)
            {
                for (var i = 0; i < document.Zones.Count; i++)
                {
                    var zone = document.Zones[i];
                    _zones.Items.Add(new Row(zone.Id, zone.Name));
                    if (zone.Id == selectedZoneId)
                    {
                        index = i;
                    }
                }
            }

            if (index >= 0)
            {
                _zones.SelectedIndex = index;
            }

            var selected = SelectedZone();
            _name.Text = selected?.Name ?? string.Empty;
            _bounds.Text = selected is null
                ? string.Empty
                : string.Create(CultureInfo.InvariantCulture, $"({selected.TileX}, {selected.TileY}) {selected.Width}×{selected.Height}");
            RefreshEntries(selected);
            _error.Text = string.Empty;
            var enabled = selected is not null;
            _name.Enabled = enabled;
            _entries.Enabled = enabled;
            _troops.Enabled = enabled;
            _monsterId.Enabled = enabled;
            _quantity.Enabled = enabled;
            _respawn.Enabled = enabled;
        }
        finally
        {
            _suspend = false;
        }
    }

    public void SetTroops(IReadOnlyList<MapEncounterTroopChoice>? troops)
    {
        _choices = troops ?? Array.Empty<MapEncounterTroopChoice>();
        _suspend = true;
        _troops.Items.Clear();
        _troops.Items.Add(new TroopItem(null, MobSpawnZoneLabels.FreeEntry));
        foreach (var choice in _choices)
        {
            var name = string.IsNullOrWhiteSpace(choice.Label) ? choice.MonsterId.ToString("D")[..8] : choice.Label.Trim();
            _troops.Items.Add(new TroopItem(choice, name));
        }

        _troops.SelectedIndex = 0;
        _catalogHint.Text = _choices.Count == 0 ? MobSpawnZoneLabels.NoCatalog : MobSpawnZoneLabels.CatalogReady;
        _suspend = false;
    }

    internal string JoinedLabelsForTest
    {
        get
        {
            var builder = new StringBuilder();
            AppendLabels(this, builder);
            return builder.ToString();
        }
    }

    internal string ZoneListForTest =>
        string.Join(" | ", _zones.Items.Cast<object>().Select(item => item.ToString()));

    internal string EntryListForTest =>
        string.Join(" | ", _entries.Items.Cast<object>().Select(item => item.ToString()));

    internal string ErrorForTest => _error.Text;

    internal bool TryAddEntryForTest(Guid monsterId, string label, int quantity, int respawnSeconds)
    {
        _monsterId.Text = monsterId.ToString("D");
        _quantity.Value = quantity;
        _respawn.Value = respawnSeconds;
        return AddEntry(label);
    }

    private bool AddEntry(string? labelOverride = null)
    {
        var zone = SelectedZone();
        if (zone is null)
        {
            _error.Text = MobSpawnZoneLabels.EmptySelection;
            return false;
        }

        if (!Guid.TryParse(_monsterId.Text.Trim(), out var monsterId) || monsterId == Guid.Empty)
        {
            _error.Text = "Choisissez un monstre.";
            return false;
        }

        var label = labelOverride ?? TroopLabel();
        if (!MobSpawnZoneEdit.TryAddEntry(zone, monsterId, label, (int)_quantity.Value, (int)_respawn.Value, out _, out var error))
        {
            _error.Text = error ?? "Monstre refusé.";
            return false;
        }

        _error.Text = string.Empty;
        RefreshEntries(zone);
        Edited?.Invoke(this, EventArgs.Empty);
        return true;
    }

    private void RemoveEntry()
    {
        var zone = SelectedZone();
        if (zone is null || _entries.SelectedItem is not Row row)
        {
            return;
        }

        if (!MobSpawnZoneEdit.TryRemoveEntry(zone, row.Id))
        {
            return;
        }

        _error.Text = string.Empty;
        RefreshEntries(zone);
        Edited?.Invoke(this, EventArgs.Empty);
    }

    private void DeleteSelectedZone()
    {
        if (_document is null || SelectedZone() is not { } zone)
        {
            return;
        }

        if (!MobSpawnZoneEdit.TryRemove(_document, zone.Id))
        {
            return;
        }

        Bind(_document, null);
        Edited?.Invoke(this, EventArgs.Empty);
    }

    private void LoadSelectedEntry()
    {
        if (_suspend || SelectedZone() is not { } zone || _entries.SelectedItem is not Row row)
        {
            return;
        }

        MobSpawnEntry? entry = null;
        foreach (var candidate in zone.Entries)
        {
            if (candidate.Id == row.Id)
            {
                entry = candidate;
                break;
            }
        }

        if (entry is null)
        {
            return;
        }

        _suspend = true;
        _monsterId.Text = entry.MonsterId.ToString("D");
        _quantity.Value = Math.Clamp(entry.Quantity, (int)_quantity.Minimum, (int)_quantity.Maximum);
        _respawn.Value = Math.Clamp(entry.RespawnSeconds, (int)_respawn.Minimum, (int)_respawn.Maximum);
        _suspend = false;
    }

    private void ApplyTroopChoice()
    {
        if (_suspend || _troops.SelectedItem is not TroopItem item || item.Choice is null)
        {
            return;
        }

        _monsterId.Text = item.Choice.MonsterId.ToString("D");
    }

    private string TroopLabel()
    {
        if (_troops.SelectedItem is TroopItem item && item.Choice is not null && !string.IsNullOrWhiteSpace(item.Choice.Label))
        {
            return item.Choice.Label.Trim();
        }

        return string.Empty;
    }

    private MobSpawnZone? SelectedZone()
    {
        if (_document is null)
        {
            return null;
        }

        if (_zones.SelectedItem is Row row)
        {
            return _document.Find(row.Id);
        }

        return null;
    }

    private void RefreshEntries(MobSpawnZone? zone)
    {
        _suspend = true;
        _entries.Items.Clear();
        if (zone is not null)
        {
            foreach (var entry in zone.Entries)
            {
                _entries.Items.Add(new Row(entry.Id, MobSpawnZoneLabels.FormatEntry(entry)));
            }
        }

        _suspend = false;
    }

    private void RefreshZoneRow(MobSpawnZone zone)
    {
        _suspend = true;
        for (var i = 0; i < _zones.Items.Count; i++)
        {
            if (_zones.Items[i] is Row row && row.Id == zone.Id)
            {
                _zones.Items[i] = new Row(zone.Id, zone.Name);
                _zones.SelectedIndex = i;
                break;
            }
        }

        _suspend = false;
    }

    private static void AppendLabels(Control parent, StringBuilder builder)
    {
        foreach (Control child in parent.Controls)
        {
            if (!string.IsNullOrWhiteSpace(child.Text) && child is not (ListBox or ComboBox or NumericUpDown or TextBox))
            {
                builder.Append(child.Text);
                builder.Append('\n');
            }

            AppendLabels(child, builder);
        }
    }

    private static ListBox List() => new()
    {
        Dock = DockStyle.Top,
        Height = 64,
        IntegralHeight = false,
        BackColor = EditorChrome.SidebarBg,
        ForeColor = EditorChrome.LabelPrimary,
        BorderStyle = BorderStyle.FixedSingle,
    };

    private static Label Title(string text) => new()
    {
        Dock = DockStyle.Top,
        Height = 22,
        ForeColor = EditorChrome.LabelPrimary,
        Padding = new Padding(8, 4, 8, 0),
        Text = text,
    };

    private static Label LabelOf(string text) => new()
    {
        Text = text,
        AutoSize = true,
        ForeColor = EditorChrome.LabelMuted,
        Margin = new Padding(0, 6, 6, 0),
    };

    private static NumericUpDown Number(int min, int max, int value) => new()
    {
        Minimum = min,
        Maximum = max,
        Value = value,
        Width = 72,
        TextAlign = HorizontalAlignment.Right,
        Margin = new Padding(0, 2, 10, 0),
    };

    private static TextBox Field(int width) => new()
    {
        Width = width,
        Margin = new Padding(0, 2, 8, 0),
    };

    private static Button ActionButton(string text, bool primary)
    {
        var button = new Button
        {
            Text = text,
            AutoSize = true,
            MinimumSize = new Size(84, 28),
            Margin = new Padding(0, 0, 6, 0),
        };
        EditorChrome.StyleDialogButton(button, primary);
        return button;
    }

    private static FlowLayoutPanel LayoutRow(params Control[] controls)
    {
        var row = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 32,
            WrapContents = false,
            Padding = new Padding(8, 0, 8, 0),
            BackColor = EditorChrome.SidebarBg,
        };
        foreach (var control in controls)
        {
            row.Controls.Add(control);
        }

        return row;
    }

    private sealed class Row
    {
        public Row(Guid id, string label)
        {
            Id = id;
            Label = label;
        }

        public Guid Id { get; }

        public string Label { get; }

        public override string ToString() => Label;
    }

    private sealed class TroopItem
    {
        public TroopItem(MapEncounterTroopChoice? choice, string label)
        {
            Choice = choice;
            Label = label;
        }

        public MapEncounterTroopChoice? Choice { get; }

        public string Label { get; }

        public override string ToString() => Label;
    }
}
