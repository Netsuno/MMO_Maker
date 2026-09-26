using System.Globalization;
using System.Text;
using System.Windows.Forms;

using Frog.Core.Maps;
using Frog.Core.Models;
using Frog.Editor.Services;
using Frog.Editor.Ui;

namespace Frog.Editor.Controls;

/// <summary>
/// Numéro de région à peindre et table de rencontres de la carte.
/// Les libellés suivent le panneau Propriétés / Drapeaux.
/// </summary>
internal sealed class MapRegionsPanel : UserControl
{
    private readonly NumericUpDown _regionId;
    private readonly NumericUpDown _steps;
    private readonly ComboBox _troops;
    private readonly TextBox _name;
    private readonly TextBox _monsterId;
    private readonly NumericUpDown _alias;
    private readonly NumericUpDown _weight;
    private readonly TextBox _regions;
    private readonly ListBox _list;
    private readonly Label _catalogHint;
    private readonly Button _add;
    private readonly Button _remove;
    private readonly Button _apply;
    private readonly Button _refresh;
    private Map? _map;
    private bool _suspend;
    private IReadOnlyList<MapEncounterTroopChoice> _choices = Array.Empty<MapEncounterTroopChoice>();

    public MapRegionsPanel()
    {
        Dock = DockStyle.Top;
        Height = 348;
        Visible = false;
        AutoScroll = true;
        BackColor = EditorChrome.SidebarBg;
        Font = EditorChrome.BodyFont;
        ForeColor = EditorChrome.LabelPrimary;

        var banner = EditorChrome.BuildZoneBanner(MapRegionLabels.PanelTitle);
        var hint = new Label
        {
            Dock = DockStyle.Top,
            Height = 34,
            ForeColor = EditorChrome.LabelMuted,
            Padding = new Padding(8, 2, 8, 0),
            Text = MapRegionLabels.Hint,
        };

        _regionId = Number(0, MapRegionDocument.MaxRegionId, 1);
        _steps = Number(MapRegionDocument.MinEncounterSteps, MapRegionDocument.MaxEncounterSteps, MapRegionDocument.DefaultEncounterSteps);
        _regionId.ValueChanged += (_, _) =>
        {
            if (_suspend)
            {
                return;
            }

            RegionIdChanged?.Invoke(this, (byte)_regionId.Value);
        };
        _steps.ValueChanged += (_, _) =>
        {
            if (_suspend || _map is null)
            {
                return;
            }

            var document = MapRegionEdit.Ensure(_map);
            if (document.TrySetEncounterSteps((int)_steps.Value, out _))
            {
                DocumentChanged?.Invoke(this, EventArgs.Empty);
            }
        };

        var brushRow = Row(
            LabelOf(MapRegionLabels.RegionNumber),
            _regionId,
            LabelOf(MapRegionLabels.Steps),
            _steps);

        _catalogHint = new Label
        {
            Dock = DockStyle.Top,
            Height = 32,
            ForeColor = EditorChrome.LabelMuted,
            Padding = new Padding(8, 2, 8, 0),
            Text = MapRegionLabels.NoCatalog,
        };

        _troops = new ComboBox
        {
            DropDownStyle = ComboBoxStyle.DropDownList,
            Width = 220,
            Margin = new Padding(0, 4, 8, 0),
        };
        EditorChrome.StyleSidebarComboBox(_troops);
        _troops.SelectedIndexChanged += (_, _) => ApplyTroopChoice();

        _name = Field(140);
        _monsterId = Field(210);
        _alias = Number(0, 1_000_000, 0);
        _weight = Number(MapRegionDocument.MinWeight, MapRegionDocument.MaxWeight, 10);
        _regions = Field(160);
        _regions.PlaceholderText = "1, 3";

        _list = new ListBox
        {
            Dock = DockStyle.Top,
            Height = 72,
            IntegralHeight = false,
            BackColor = EditorChrome.SidebarBg,
            ForeColor = EditorChrome.LabelPrimary,
            BorderStyle = BorderStyle.FixedSingle,
        };
        _list.SelectedIndexChanged += (_, _) => LoadSelectedEncounter();

        _add = ActionButton(MapRegionLabels.Add, false);
        _remove = ActionButton(MapRegionLabels.Remove, false);
        _apply = ActionButton(MapRegionLabels.Apply, true);
        _refresh = ActionButton(MapRegionLabels.RefreshCatalog, false);
        _add.Click += (_, _) => CommitEncounter(replace: false);
        _apply.Click += (_, _) => CommitEncounter(replace: _list.SelectedIndex >= 0);
        _remove.Click += (_, _) => RemoveSelected();
        _refresh.Click += (_, _) => TroopsRefreshRequested?.Invoke(this, EventArgs.Empty);

        var encountersTitle = new Label
        {
            Dock = DockStyle.Top,
            Height = 22,
            ForeColor = EditorChrome.LabelPrimary,
            Padding = new Padding(8, 4, 8, 0),
            Text = MapRegionLabels.Encounters,
        };

        var troopRow = Row(LabelOf(MapRegionLabels.Monster), _troops);
        var nameRow = Row(LabelOf(MapRegionLabels.Name), _name, LabelOf(MapRegionLabels.Identifier), _monsterId);
        var weightRow = Row(LabelOf(MapRegionLabels.Weight), _weight, LabelOf(MapRegionLabels.Alias), _alias);
        var regionRow = Row(LabelOf(MapRegionLabels.EncounterRegions), _regions, LabelOf(MapRegionLabels.WholeMapHint), new Label
        {
            AutoSize = true,
            ForeColor = EditorChrome.LabelMuted,
            Text = string.Empty,
            Width = 4,
        });
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            Height = 36,
            WrapContents = false,
            Padding = new Padding(8, 2, 8, 0),
            BackColor = EditorChrome.SidebarBg,
        };
        buttons.Controls.Add(_add);
        buttons.Controls.Add(_apply);
        buttons.Controls.Add(_remove);
        buttons.Controls.Add(_refresh);

        Controls.Add(_list);
        Controls.Add(buttons);
        Controls.Add(regionRow);
        Controls.Add(weightRow);
        Controls.Add(nameRow);
        Controls.Add(troopRow);
        Controls.Add(_catalogHint);
        Controls.Add(encountersTitle);
        Controls.Add(brushRow);
        Controls.Add(hint);
        Controls.Add(banner);
        SetTroops(Array.Empty<MapEncounterTroopChoice>());
    }

    public event EventHandler<byte>? RegionIdChanged;

    public event EventHandler? DocumentChanged;

    public event EventHandler? TroopsRefreshRequested;

    public byte SelectedRegionId => (byte)_regionId.Value;

    public void Bind(Map? map)
    {
        _map = map;
        _suspend = true;
        var document = map?.Regions;
        _steps.Value = document?.EncounterSteps ?? MapRegionDocument.DefaultEncounterSteps;
        if (map is not null && document is null)
        {
            _steps.Value = MapRegionDocument.DefaultEncounterSteps;
        }

        RefreshList();
        _suspend = false;
    }

    public void SetTroops(IReadOnlyList<MapEncounterTroopChoice>? troops)
    {
        _choices = troops ?? Array.Empty<MapEncounterTroopChoice>();
        _suspend = true;
        _troops.Items.Clear();
        _troops.Items.Add(new TroopItem(null, MapRegionLabels.FreeEntry));
        foreach (var choice in _choices)
        {
            var name = string.IsNullOrWhiteSpace(choice.Label) ? choice.MonsterId.ToString("D")[..8] : choice.Label.Trim();
            _troops.Items.Add(new TroopItem(choice, name));
        }

        _troops.SelectedIndex = 0;
        _catalogHint.Text = _choices.Count == 0 ? MapRegionLabels.NoCatalog : MapRegionLabels.CatalogReady;
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

    internal string CatalogHintForTest => _catalogHint.Text;

    internal byte RegionIdForTest
    {
        get => SelectedRegionId;
        set => _regionId.Value = value;
    }

    internal int StepsForTest
    {
        get => (int)_steps.Value;
        set => _steps.Value = value;
    }

    internal int EncounterCountForTest => _map?.Regions?.Encounters.Count ?? 0;

    internal string? TryAddEncounterForTest(string? label, Guid monsterId, int? aliasId, int weight, string? regions)
    {
        _name.Text = label ?? string.Empty;
        _monsterId.Text = monsterId == Guid.Empty ? string.Empty : monsterId.ToString("D");
        _alias.Value = aliasId ?? 0;
        _weight.Value = weight;
        _regions.Text = regions ?? string.Empty;
        _list.SelectedIndex = -1;
        return CommitEncounter(replace: false);
    }

    internal string ListTextForTest =>
        string.Join(" | ", _list.Items.Cast<object>().Select(item => item.ToString()));

    private string? CommitEncounter(bool replace)
    {
        if (_map is null)
        {
            return "Aucune carte chargée.";
        }

        if (!Guid.TryParse(_monsterId.Text.Trim(), out var monsterId))
        {
            if (!string.IsNullOrWhiteSpace(_monsterId.Text))
            {
                return "Identifiant de monstre invalide.";
            }

            monsterId = Guid.Empty;
        }

        var document = MapRegionEdit.Ensure(_map);

        var draft = new MapEncounterDraft
        {
            MonsterId = monsterId,
            AliasId = _alias.Value <= 0 ? null : (int)_alias.Value,
            Label = _name.Text,
            Weight = (int)_weight.Value,
            RegionsText = _regions.Text,
        };
        var index = _list.SelectedIndex;
        var ok = replace
            ? document.TryReplaceEncounter(index, draft, out var error)
            : document.TryAddEncounter(draft, out error);
        if (!ok)
        {
            return error;
        }

        if (document.TrySetEncounterSteps((int)_steps.Value, out _))
        {
            // Le pas est déjà sur le document si la carte vient d’être créée.
        }

        RefreshList();
        _list.SelectedIndex = replace ? index : document.Encounters.Count - 1;
        DocumentChanged?.Invoke(this, EventArgs.Empty);
        return null;
    }

    private void RemoveSelected()
    {
        if (_map?.Regions is not { } document || _list.SelectedIndex < 0)
        {
            return;
        }

        if (!document.TryRemoveEncounter(_list.SelectedIndex, out _))
        {
            return;
        }

        RefreshList();
        DocumentChanged?.Invoke(this, EventArgs.Empty);
    }

    private void LoadSelectedEncounter()
    {
        if (_suspend || _map?.Regions is not { } document || _list.SelectedIndex < 0)
        {
            return;
        }

        var entry = document.Encounters[_list.SelectedIndex];
        _suspend = true;
        _name.Text = entry.Label;
        _monsterId.Text = entry.MonsterId == Guid.Empty ? string.Empty : entry.MonsterId.ToString("D");
        _alias.Value = entry.AliasId ?? 0;
        _weight.Value = Math.Clamp(entry.Weight, MapRegionDocument.MinWeight, MapRegionDocument.MaxWeight);
        _regions.Text = entry.Regions.Count == 0
            ? string.Empty
            : string.Join(", ", entry.Regions.Select(id => id.ToString(CultureInfo.InvariantCulture)));
        _suspend = false;
    }

    private void ApplyTroopChoice()
    {
        if (_suspend || _troops.SelectedItem is not TroopItem { Choice: { } choice })
        {
            return;
        }

        _suspend = true;
        _name.Text = choice.Label;
        _monsterId.Text = choice.MonsterId == Guid.Empty ? string.Empty : choice.MonsterId.ToString("D");
        _alias.Value = choice.AliasId is > 0 and var alias ? alias : 0;
        _suspend = false;
    }

    private void RefreshList()
    {
        var selected = _list.SelectedIndex;
        _suspend = true;
        _list.Items.Clear();
        if (_map?.Regions is { } document)
        {
            foreach (var entry in document.Encounters)
            {
                _list.Items.Add(MapRegionEdit.FormatEncounter(entry));
            }
        }

        if (selected >= 0 && selected < _list.Items.Count)
        {
            _list.SelectedIndex = selected;
        }

        _suspend = false;
    }

    private static void AppendLabels(Control root, StringBuilder builder)
    {
        foreach (Control child in root.Controls)
        {
            if (child is Label or Button)
            {
                builder.Append(child.Text);
                builder.Append('\n');
            }

            AppendLabels(child, builder);
        }
    }

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
        Width = 64,
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

    private static FlowLayoutPanel Row(params Control[] controls)
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
