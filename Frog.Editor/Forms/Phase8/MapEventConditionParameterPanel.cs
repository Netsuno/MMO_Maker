using System.Text.Json;
using Frog.Core.Events;
using Frog.Core.Models;

namespace Frog.Editor.Forms.Phase8;

/// <summary>Champs typés pour une condition événement (P8-I2 / J2).</summary>
internal sealed class MapEventConditionParameterPanel : UserControl
{
    private readonly ComboBox _kind = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
    private readonly Panel _fieldsHost = new() { AutoSize = true, Dock = DockStyle.Top };
    private readonly TextBox _advancedJson = new()
    {
        Multiline = true,
        Width = 480,
        Height = 60,
        ScrollBars = ScrollBars.Vertical,
        Visible = false,
    };
    private readonly CheckBox _showAdvanced = new() { Text = "JSON avancé", AutoSize = true };

    private bool _binding;
    private readonly List<Control> _dynamicFields = new();

    public MapEventConditionParameterPanel()
    {
        AutoSize = true;
        foreach (var k in MapEventConditionKinds.All.OrderBy(x => x, StringComparer.Ordinal))
        {
            _kind.Items.Add(k);
        }

        if (_kind.Items.Count > 0)
        {
            _kind.SelectedIndex = 0;
        }

        var layout = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Dock = DockStyle.Fill,
        };
        layout.Controls.Add(new Label { Text = "Condition", AutoSize = true });
        layout.Controls.Add(_kind);
        layout.Controls.Add(_fieldsHost);
        layout.Controls.Add(_showAdvanced);
        layout.Controls.Add(_advancedJson);
        Controls.Add(layout);

        _kind.SelectedIndexChanged += (_, _) =>
        {
            if (!_binding)
            {
                RebuildFields();
                NotifyChanged();
            }
        };
        _showAdvanced.CheckedChanged += (_, _) =>
        {
            _advancedJson.Visible = _showAdvanced.Checked;
            _fieldsHost.Visible = !_showAdvanced.Checked;
            NotifyChanged();
        };
        _advancedJson.TextChanged += (_, _) => NotifyChanged();
    }

    public event Action? ParametersChanged;

    internal ComboBox KindForTest => _kind;

    internal TextBox AdvancedJsonForTest => _advancedJson;

    public void LoadCondition(MapEventConditionDefinition condition)
    {
        _binding = true;
        try
        {
            var idx = _kind.Items.IndexOf(condition.Kind);
            _kind.SelectedIndex = idx >= 0 ? idx : 0;
            RebuildFields();
            ApplyParameterJson(condition.ParameterJson);
            _advancedJson.Text = condition.ParameterJson;
        }
        finally
        {
            _binding = false;
        }
    }

    public bool TryBuildCondition(out MapEventConditionDefinition condition, out string? error)
    {
        var kind = _kind.SelectedItem as string ?? MapEventConditionKinds.CharacterSwitch;
        string parameterJson;
        if (_showAdvanced.Checked)
        {
            parameterJson = _advancedJson.Text.Trim();
        }
        else if (!TryBuildParameterJson(kind, out parameterJson, out error))
        {
            condition = new MapEventConditionDefinition();
            return false;
        }

        condition = new MapEventConditionDefinition
        {
            Kind = kind,
            ParameterJson = string.IsNullOrWhiteSpace(parameterJson) ? "{}" : parameterJson,
        };

        if (!condition.Validate(out error))
        {
            return false;
        }

        error = null;
        return true;
    }

    private void NotifyChanged()
    {
        if (!_binding)
        {
            ParametersChanged?.Invoke();
        }
    }

    private void RebuildFields()
    {
        foreach (var c in _dynamicFields)
        {
            _fieldsHost.Controls.Remove(c);
            c.Dispose();
        }

        _dynamicFields.Clear();
        AddFieldsForKind(_kind.SelectedItem as string ?? MapEventConditionKinds.CharacterSwitch);
    }

    private void AddFieldsForKind(string kind)
    {
        switch (kind)
        {
            case MapEventConditionKinds.CharacterSwitch:
                AddLabeled("switchId", new TextBox { Width = 200, Text = "gate_open" });
                AddLabeled("value", new CheckBox { Text = "true", Checked = true });
                break;
            case MapEventConditionKinds.CharacterVariableCompare:
                AddLabeled("variableId", new TextBox { Width = 200, Text = "var1" });
                AddLabeled("op", new ComboBox
                {
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Width = 80,
                });
                var opCombo = (ComboBox)_dynamicFields[^1];
                foreach (var op in new[] { "eq", "ne", "lt", "lte", "gt", "gte" })
                {
                    opCombo.Items.Add(op);
                }

                opCombo.SelectedIndex = 0;
                AddLabeled("value", new NumericUpDown { Width = 100, Minimum = int.MinValue, Maximum = int.MaxValue });
                break;
            case MapEventConditionKinds.QuestStatus:
                AddLabeled("questId", new TextBox { Width = 280, Text = Guid.Empty.ToString() });
                AddLabeled("status", new ComboBox
                {
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Width = 140,
                });
                var statusCombo = (ComboBox)_dynamicFields[^1];
                foreach (var s in new[] { "not_started", "active", "ready", "completed" })
                {
                    statusCombo.Items.Add(s);
                }

                statusCombo.SelectedIndex = 1;
                break;
            case MapEventConditionKinds.ItemQuantity:
                AddLabeled("itemId", new TextBox { Width = 280, Text = Guid.Empty.ToString() });
                AddLabeled("minQuantity", new NumericUpDown { Width = 80, Minimum = 1, Maximum = 9999, Value = 1 });
                break;
            case MapEventConditionKinds.CharacterLevel:
                AddLabeled("minLevel", new NumericUpDown { Width = 80, Minimum = 1, Maximum = 999, Value = 1 });
                break;
            case MapEventConditionKinds.ProfessionLevel:
                AddLabeled("professionId", new TextBox { Width = 280, Text = Guid.Empty.ToString() });
                AddLabeled("minLevel", new NumericUpDown { Width = 80, Minimum = 1, Maximum = 999, Value = 1 });
                break;
            case MapEventConditionKinds.MapOrRegion:
                AddLabeled("mapId", new NumericUpDown { Width = 80, Minimum = 0, Maximum = int.MaxValue, Value = 0 });
                AddLabeled("regionId", new TextBox { Width = 280, Text = string.Empty });
                break;
        }
    }

    private void AddLabeled(string key, Control control)
    {
        var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        row.Controls.Add(new Label { Text = key, AutoSize = true, Width = 120 });
        row.Controls.Add(control);
        row.Tag = key;
        _fieldsHost.Controls.Add(row);
        _dynamicFields.Add(row);
        if (control is TextBox tb)
        {
            tb.TextChanged += (_, _) => NotifyChanged();
        }
        else if (control is NumericUpDown nud)
        {
            nud.ValueChanged += (_, _) => NotifyChanged();
        }
        else if (control is CheckBox cb)
        {
            cb.CheckedChanged += (_, _) => NotifyChanged();
        }
        else if (control is ComboBox combo)
        {
            combo.SelectedIndexChanged += (_, _) => NotifyChanged();
        }
    }

    private Control? FindFieldControl(string key)
    {
        foreach (Control row in _fieldsHost.Controls)
        {
            if (row.Tag as string != key || row.Controls.Count < 2)
            {
                continue;
            }

            return row.Controls[1];
        }

        return null;
    }

    private string GetText(string key) =>
        FindFieldControl(key) is TextBox tb ? tb.Text.Trim() : string.Empty;

    private int GetInt(string key) =>
        FindFieldControl(key) is NumericUpDown nud ? (int)nud.Value : 0;

    private bool GetBool(string key) =>
        FindFieldControl(key) is CheckBox cb && cb.Checked;

    private string GetCombo(string key) =>
        FindFieldControl(key) is ComboBox combo ? combo.SelectedItem as string ?? string.Empty : string.Empty;

    private void SetText(string key, string value)
    {
        if (FindFieldControl(key) is TextBox tb)
        {
            tb.Text = value;
        }
    }

    private void SetInt(string key, int value)
    {
        if (FindFieldControl(key) is NumericUpDown nud)
        {
            nud.Value = Math.Clamp(value, (int)nud.Minimum, (int)nud.Maximum);
        }
    }

    private void SetBool(string key, bool value)
    {
        if (FindFieldControl(key) is CheckBox cb)
        {
            cb.Checked = value;
        }
    }

    private void SetCombo(string key, string value)
    {
        if (FindFieldControl(key) is ComboBox combo)
        {
            var idx = combo.Items.IndexOf(value);
            combo.SelectedIndex = idx >= 0 ? idx : 0;
        }
    }

    private void ApplyParameterJson(string json)
    {
        var kind = _kind.SelectedItem as string ?? string.Empty;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            switch (kind)
            {
                case MapEventConditionKinds.CharacterSwitch:
                    if (root.TryGetProperty("switchId", out var swId))
                    {
                        SetText("switchId", swId.GetString() ?? string.Empty);
                    }

                    if (root.TryGetProperty("value", out var swVal))
                    {
                        SetBool("value", swVal.GetBoolean());
                    }

                    break;
                case MapEventConditionKinds.CharacterVariableCompare:
                    if (root.TryGetProperty("variableId", out var varId))
                    {
                        SetText("variableId", varId.GetString() ?? string.Empty);
                    }

                    if (root.TryGetProperty("op", out var opEl))
                    {
                        SetCombo("op", opEl.GetString() ?? "eq");
                    }

                    if (root.TryGetProperty("value", out var varVal))
                    {
                        SetInt("value", varVal.GetInt32());
                    }

                    break;
                case MapEventConditionKinds.QuestStatus:
                    if (root.TryGetProperty("questId", out var qId))
                    {
                        SetText("questId", qId.GetString() ?? string.Empty);
                    }

                    if (root.TryGetProperty("status", out var status))
                    {
                        SetCombo("status", status.GetString() ?? "active");
                    }

                    break;
                case MapEventConditionKinds.ItemQuantity:
                    if (root.TryGetProperty("itemId", out var itemId))
                    {
                        SetText("itemId", itemId.GetString() ?? string.Empty);
                    }

                    if (root.TryGetProperty("minQuantity", out var qty))
                    {
                        SetInt("minQuantity", qty.GetInt32());
                    }

                    break;
                case MapEventConditionKinds.CharacterLevel:
                    if (root.TryGetProperty("minLevel", out var minLvl))
                    {
                        SetInt("minLevel", minLvl.GetInt32());
                    }

                    break;
                case MapEventConditionKinds.ProfessionLevel:
                    if (root.TryGetProperty("professionId", out var profId))
                    {
                        SetText("professionId", profId.GetString() ?? string.Empty);
                    }

                    if (root.TryGetProperty("minLevel", out var profLvl))
                    {
                        SetInt("minLevel", profLvl.GetInt32());
                    }

                    break;
                case MapEventConditionKinds.MapOrRegion:
                    if (root.TryGetProperty("mapId", out var mapId))
                    {
                        SetInt("mapId", mapId.GetInt32());
                    }

                    if (root.TryGetProperty("regionId", out var regionId))
                    {
                        SetText("regionId", regionId.GetString() ?? string.Empty);
                    }

                    break;
            }
        }
        catch
        {
            // invalid JSON — advanced mode handles it
        }
    }

    private bool TryBuildParameterJson(string kind, out string json, out string? error)
    {
        error = null;
        try
        {
            json = kind switch
            {
                MapEventConditionKinds.CharacterSwitch =>
                    JsonSerializer.Serialize(new { switchId = GetText("switchId"), value = GetBool("value") }),
                MapEventConditionKinds.CharacterVariableCompare =>
                    JsonSerializer.Serialize(new
                    {
                        variableId = GetText("variableId"),
                        op = GetCombo("op"),
                        value = GetInt("value"),
                    }),
                MapEventConditionKinds.QuestStatus =>
                    JsonSerializer.Serialize(new { questId = GetText("questId"), status = GetCombo("status") }),
                MapEventConditionKinds.ItemQuantity =>
                    JsonSerializer.Serialize(new { itemId = GetText("itemId"), minQuantity = GetInt("minQuantity") }),
                MapEventConditionKinds.CharacterLevel =>
                    JsonSerializer.Serialize(new { minLevel = GetInt("minLevel") }),
                MapEventConditionKinds.ProfessionLevel =>
                    JsonSerializer.Serialize(new { professionId = GetText("professionId"), minLevel = GetInt("minLevel") }),
                MapEventConditionKinds.MapOrRegion => BuildMapOrRegionJson(),
                _ => "{}",
            };
            return true;
        }
        catch (Exception ex)
        {
            json = "{}";
            error = ex.Message;
            return false;
        }
    }

    private string BuildMapOrRegionJson()
    {
        var regionId = GetText("regionId");
        var mapId = GetInt("mapId");
        if (string.IsNullOrWhiteSpace(regionId))
        {
            return JsonSerializer.Serialize(new { mapId });
        }

        return JsonSerializer.Serialize(new { mapId, regionId });
    }
}
