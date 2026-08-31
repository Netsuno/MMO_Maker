using System.Text.Json;
using Frog.Core.Events;
using Frog.Core.Models;

namespace Frog.Editor.Forms.Phase8;

/// <summary>Champs typés pour une commande événement (P8-I2).</summary>
internal sealed class MapEventCommandParameterPanel : UserControl
{
    private readonly ComboBox _discriminator = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 200 };
    private readonly Panel _fieldsHost = new() { AutoSize = true, Dock = DockStyle.Top };
    private readonly TextBox _advancedJson = new()
    {
        Multiline = true,
        Width = 480,
        Height = 80,
        ScrollBars = ScrollBars.Vertical,
        Visible = false,
    };
    private readonly CheckBox _showAdvanced = new() { Text = "JSON avancé", AutoSize = true };

    private bool _binding;
    private readonly List<Control> _dynamicFields = new();
    private MapEventConditionParameterPanel? _branchCondition;
    private MapEventCommandListPanel? _branchThen;
    private MapEventCommandListPanel? _branchElse;

    public MapEventCommandParameterPanel()
    {
        AutoSize = true;
        foreach (var d in MapEventCommandDiscriminators.All.OrderBy(x => x, StringComparer.Ordinal))
        {
            _discriminator.Items.Add(d);
        }

        if (_discriminator.Items.Count > 0)
        {
            _discriminator.SelectedIndex = 0;
        }

        var layout = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Dock = DockStyle.Fill,
        };
        layout.Controls.Add(new Label { Text = "Commande", AutoSize = true });
        layout.Controls.Add(_discriminator);
        layout.Controls.Add(_fieldsHost);
        layout.Controls.Add(_showAdvanced);
        layout.Controls.Add(_advancedJson);
        Controls.Add(layout);

        _discriminator.SelectedIndexChanged += (_, _) =>
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

    internal ComboBox DiscriminatorForTest => _discriminator;

    internal TextBox AdvancedJsonForTest => _advancedJson;

    public void LoadCommand(MapEventCommandDefinition command)
    {
        _binding = true;
        try
        {
            var idx = _discriminator.Items.IndexOf(command.Discriminator);
            _discriminator.SelectedIndex = idx >= 0 ? idx : 0;
            RebuildFields();
            ApplyParameterJson(command.ParameterJson);
            _advancedJson.Text = command.ParameterJson;
        }
        finally
        {
            _binding = false;
        }
    }

    public bool TryBuildCommand(out MapEventCommandDefinition command, out string? error)
    {
        var discriminator = _discriminator.SelectedItem as string ?? MapEventCommandDiscriminators.ShowText;
        string parameterJson;
        if (_showAdvanced.Checked)
        {
            parameterJson = _advancedJson.Text.Trim();
        }
        else
        {
            if (!TryBuildParameterJson(discriminator, out parameterJson, out error))
            {
                command = new MapEventCommandDefinition();
                return false;
            }
        }

        command = new MapEventCommandDefinition
        {
            Discriminator = discriminator,
            SchemaVersion = 1,
            ParameterJson = string.IsNullOrWhiteSpace(parameterJson) ? "{}" : parameterJson,
        };

        if (!command.Validate(out error))
        {
            return false;
        }

        if (!MapEventCommandParameterValidator.ValidateParameters(command, out error))
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
        _branchCondition = null;
        _branchThen = null;
        _branchElse = null;
        var discriminator = _discriminator.SelectedItem as string ?? MapEventCommandDiscriminators.ShowText;
        AddFieldsForDiscriminator(discriminator);
    }

    private void AddFieldsForDiscriminator(string discriminator)
    {
        switch (discriminator)
        {
            case MapEventCommandDiscriminators.ShowText:
                AddLabeled("text", new TextBox { Width = 360, Text = "…" });
                break;
            case MapEventCommandDiscriminators.SetSwitch:
                AddLabeled("switchId", new TextBox { Width = 200, Text = "gate_open" });
                AddLabeled("value", new CheckBox { Text = "true", Checked = true });
                break;
            case MapEventCommandDiscriminators.SetVariable:
                AddLabeled("variableId", new TextBox { Width = 200, Text = "var1" });
                AddLabeled("value", new NumericUpDown { Width = 100, Minimum = int.MinValue, Maximum = int.MaxValue });
                break;
            case MapEventCommandDiscriminators.AddVariable:
            case MapEventCommandDiscriminators.SubVariable:
                AddLabeled("variableId", new TextBox { Width = 200, Text = "var1" });
                AddLabeled("delta", new NumericUpDown { Width = 100, Minimum = 0, Maximum = int.MaxValue, Value = 1 });
                break;
            case MapEventCommandDiscriminators.GiveItem:
            case MapEventCommandDiscriminators.TakeItem:
                AddLabeled("itemId", new TextBox { Width = 280, Text = Guid.Empty.ToString() });
                AddLabeled("quantity", new NumericUpDown { Width = 80, Minimum = 1, Maximum = 9999, Value = 1 });
                AddLabeled("onceKey", new TextBox { Width = 200, Text = string.Empty });
                break;
            case MapEventCommandDiscriminators.GiveGold:
            case MapEventCommandDiscriminators.TakeGold:
                AddLabeled("amount", new NumericUpDown { Width = 100, Minimum = 0, Maximum = int.MaxValue, Value = 10 });
                AddLabeled("onceKey", new TextBox { Width = 200, Text = string.Empty });
                break;
            case MapEventCommandDiscriminators.StartDialogue:
                AddLabeled("dialogueId", new TextBox { Width = 280, Text = Guid.Empty.ToString() });
                break;
            case MapEventCommandDiscriminators.StartQuest:
            case MapEventCommandDiscriminators.TurnInQuest:
                AddLabeled("questId", new TextBox { Width = 280, Text = Guid.Empty.ToString() });
                break;
            case MapEventCommandDiscriminators.AdvanceQuest:
                AddLabeled("questId", new TextBox { Width = 280, Text = Guid.Empty.ToString() });
                AddLabeled("stageIndex", new NumericUpDown { Width = 80, Minimum = 0, Maximum = 99, Value = 0 });
                break;
            case MapEventCommandDiscriminators.Teleport:
                AddLabeled("mapId", new NumericUpDown { Width = 80, Minimum = 1, Maximum = int.MaxValue, Value = 1 });
                AddLabeled("tileX", new NumericUpDown { Width = 80, Minimum = 0, Maximum = 9999 });
                AddLabeled("tileY", new NumericUpDown { Width = 80, Minimum = 0, Maximum = 9999 });
                break;
            case MapEventCommandDiscriminators.Wait:
                AddLabeled("waitMs", new NumericUpDown { Width = 100, Minimum = 0, Maximum = 600000, Value = 500 });
                break;
            case MapEventCommandDiscriminators.CallCommonEvent:
                AddLabeled("commonEventId", new TextBox { Width = 280, Text = Guid.Empty.ToString() });
                AddLabeled("commonEventAliasId", new NumericUpDown { Width = 80, Minimum = 0, Maximum = int.MaxValue, Value = 0 });
                break;
            case MapEventCommandDiscriminators.LearnProfession:
                AddLabeled("professionId", new TextBox { Width = 280, Text = Guid.Empty.ToString() });
                break;
            case MapEventCommandDiscriminators.Branch:
                _branchCondition = new MapEventConditionParameterPanel();
                _branchThen = new MapEventCommandListPanel();
                _branchElse = new MapEventCommandListPanel();
                _branchCondition.ParametersChanged += () => NotifyChanged();
                _branchThen.CommandsChanged += () => NotifyChanged();
                _branchElse.CommandsChanged += () => NotifyChanged();
                AddLabeled("condition", _branchCondition);
                AddLabeled("thenCommands", _branchThen);
                AddLabeled("elseCommands", _branchElse);
                break;
            default:
                AddLabeled("parameterJson", new TextBox { Width = 360, Text = "{}" });
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

    private void ApplyParameterJson(string json)
    {
        var discriminator = _discriminator.SelectedItem as string ?? string.Empty;
        try
        {
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            switch (discriminator)
            {
                case MapEventCommandDiscriminators.ShowText:
                    if (root.TryGetProperty("text", out var textEl))
                    {
                        SetText("text", textEl.GetString() ?? string.Empty);
                    }

                    break;
                case MapEventCommandDiscriminators.SetSwitch:
                    if (root.TryGetProperty("switchId", out var swId))
                    {
                        SetText("switchId", swId.GetString() ?? string.Empty);
                    }

                    if (root.TryGetProperty("value", out var swVal))
                    {
                        SetBool("value", swVal.GetBoolean());
                    }

                    break;
                case MapEventCommandDiscriminators.SetVariable:
                    if (root.TryGetProperty("variableId", out var varId))
                    {
                        SetText("variableId", varId.GetString() ?? string.Empty);
                    }

                    if (root.TryGetProperty("value", out var varVal))
                    {
                        SetInt("value", varVal.GetInt32());
                    }

                    break;
                case MapEventCommandDiscriminators.AddVariable:
                case MapEventCommandDiscriminators.SubVariable:
                    if (root.TryGetProperty("variableId", out var addId))
                    {
                        SetText("variableId", addId.GetString() ?? string.Empty);
                    }

                    if (root.TryGetProperty("delta", out var delta))
                    {
                        SetInt("delta", delta.GetInt32());
                    }

                    break;
                case MapEventCommandDiscriminators.GiveItem:
                case MapEventCommandDiscriminators.TakeItem:
                    if (root.TryGetProperty("itemId", out var itemId))
                    {
                        SetText("itemId", itemId.GetString() ?? string.Empty);
                    }

                    if (root.TryGetProperty("quantity", out var qty))
                    {
                        SetInt("quantity", qty.GetInt32());
                    }

                    if (root.TryGetProperty("onceKey", out var once))
                    {
                        SetText("onceKey", once.GetString() ?? string.Empty);
                    }

                    break;
                case MapEventCommandDiscriminators.GiveGold:
                case MapEventCommandDiscriminators.TakeGold:
                    if (root.TryGetProperty("amount", out var amount))
                    {
                        SetInt("amount", amount.GetInt32());
                    }

                    if (root.TryGetProperty("onceKey", out var goldOnce))
                    {
                        SetText("onceKey", goldOnce.GetString() ?? string.Empty);
                    }

                    break;
                case MapEventCommandDiscriminators.StartDialogue:
                    if (root.TryGetProperty("dialogueId", out var dlg))
                    {
                        SetText("dialogueId", dlg.GetString() ?? string.Empty);
                    }

                    break;
                case MapEventCommandDiscriminators.StartQuest:
                case MapEventCommandDiscriminators.TurnInQuest:
                    if (root.TryGetProperty("questId", out var qId))
                    {
                        SetText("questId", qId.GetString() ?? string.Empty);
                    }

                    break;
                case MapEventCommandDiscriminators.AdvanceQuest:
                    if (root.TryGetProperty("questId", out var advQ))
                    {
                        SetText("questId", advQ.GetString() ?? string.Empty);
                    }

                    if (root.TryGetProperty("stageIndex", out var stage))
                    {
                        SetInt("stageIndex", stage.GetInt32());
                    }

                    break;
                case MapEventCommandDiscriminators.Teleport:
                    if (root.TryGetProperty("mapId", out var mapId))
                    {
                        SetInt("mapId", mapId.GetInt32());
                    }

                    if (root.TryGetProperty("tileX", out var tx))
                    {
                        SetInt("tileX", tx.GetInt32());
                    }

                    if (root.TryGetProperty("tileY", out var ty))
                    {
                        SetInt("tileY", ty.GetInt32());
                    }

                    break;
                case MapEventCommandDiscriminators.Wait:
                    if (root.TryGetProperty("waitMs", out var wait))
                    {
                        SetInt("waitMs", wait.GetInt32());
                    }

                    break;
                case MapEventCommandDiscriminators.CallCommonEvent:
                    if (root.TryGetProperty("commonEventId", out var ceId))
                    {
                        SetText("commonEventId", ceId.GetString() ?? string.Empty);
                    }

                    if (root.TryGetProperty("commonEventAliasId", out var ceAlias))
                    {
                        SetInt("commonEventAliasId", ceAlias.GetInt32());
                    }

                    break;
                case MapEventCommandDiscriminators.LearnProfession:
                    if (root.TryGetProperty("professionId", out var prof))
                    {
                        SetText("professionId", prof.GetString() ?? string.Empty);
                    }

                    break;
                case MapEventCommandDiscriminators.Branch:
                    if (_branchCondition is not null
                        && root.TryGetProperty("conditionKind", out var condKindEl))
                    {
                        var condKind = condKindEl.GetString() ?? MapEventConditionKinds.CharacterSwitch;
                        var condParam = root.TryGetProperty("conditionParameterJson", out var condParamEl)
                            ? condParamEl.GetString() ?? "{}"
                            : "{}";
                        _branchCondition.LoadCondition(new MapEventConditionDefinition
                        {
                            Kind = condKind,
                            ParameterJson = condParam,
                        });
                    }

                    if (_branchThen is not null && root.TryGetProperty("thenCommands", out var thenEl))
                    {
                        _branchThen.LoadCommands(ParseCommandArray(thenEl));
                    }

                    if (_branchElse is not null && root.TryGetProperty("elseCommands", out var elseEl))
                    {
                        _branchElse.LoadCommands(ParseCommandArray(elseEl));
                    }

                    break;
            }
        }
        catch
        {
            // invalid JSON — advanced mode handles it
        }
    }

    private static IReadOnlyList<MapEventCommandDefinition> ParseCommandArray(JsonElement arrayEl)
    {
        if (arrayEl.ValueKind != JsonValueKind.Array)
        {
            return Array.Empty<MapEventCommandDefinition>();
        }

        var list = new List<MapEventCommandDefinition>();
        foreach (var item in arrayEl.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
            {
                continue;
            }

            var discriminator = item.TryGetProperty("discriminator", out var discEl)
                ? discEl.GetString() ?? MapEventCommandDiscriminators.ShowText
                : MapEventCommandDiscriminators.ShowText;
            var paramJson = item.TryGetProperty("parameterJson", out var paramEl)
                ? paramEl.GetString() ?? "{}"
                : "{}";
            list.Add(new MapEventCommandDefinition
            {
                Discriminator = discriminator,
                SchemaVersion = 1,
                ParameterJson = paramJson,
            });
        }

        return list;
    }

    private bool TryBuildParameterJson(string discriminator, out string json, out string? error)
    {
        error = null;
        try
        {
            if (discriminator == MapEventCommandDiscriminators.Branch)
            {
                json = BuildBranchJson(out error);
                return error is null;
            }

            json = discriminator switch
            {
                MapEventCommandDiscriminators.ShowText =>
                    JsonSerializer.Serialize(new { text = GetText("text") }),
                MapEventCommandDiscriminators.SetSwitch =>
                    JsonSerializer.Serialize(new { switchId = GetText("switchId"), value = GetBool("value") }),
                MapEventCommandDiscriminators.SetVariable =>
                    JsonSerializer.Serialize(new { variableId = GetText("variableId"), value = GetInt("value") }),
                MapEventCommandDiscriminators.AddVariable =>
                    JsonSerializer.Serialize(new { variableId = GetText("variableId"), delta = GetInt("delta") }),
                MapEventCommandDiscriminators.SubVariable =>
                    JsonSerializer.Serialize(new { variableId = GetText("variableId"), delta = GetInt("delta") }),
                MapEventCommandDiscriminators.GiveItem or MapEventCommandDiscriminators.TakeItem =>
                    BuildItemMutationJson(),
                MapEventCommandDiscriminators.GiveGold or MapEventCommandDiscriminators.TakeGold =>
                    BuildGoldMutationJson(),
                MapEventCommandDiscriminators.StartDialogue =>
                    JsonSerializer.Serialize(new { dialogueId = GetText("dialogueId") }),
                MapEventCommandDiscriminators.StartQuest or MapEventCommandDiscriminators.TurnInQuest =>
                    JsonSerializer.Serialize(new { questId = GetText("questId") }),
                MapEventCommandDiscriminators.AdvanceQuest =>
                    JsonSerializer.Serialize(new { questId = GetText("questId"), stageIndex = GetInt("stageIndex") }),
                MapEventCommandDiscriminators.Teleport =>
                    JsonSerializer.Serialize(new
                    {
                        mapId = GetInt("mapId"),
                        tileX = GetInt("tileX"),
                        tileY = GetInt("tileY"),
                    }),
                MapEventCommandDiscriminators.Wait =>
                    JsonSerializer.Serialize(new { waitMs = GetInt("waitMs") }),
                MapEventCommandDiscriminators.CallCommonEvent =>
                    JsonSerializer.Serialize(new
                    {
                        commonEventId = GetText("commonEventId"),
                        commonEventAliasId = GetInt("commonEventAliasId"),
                    }),
                MapEventCommandDiscriminators.LearnProfession =>
                    JsonSerializer.Serialize(new { professionId = GetText("professionId") }),
                _ => GetText("parameterJson"),
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

    private string BuildItemMutationJson()
    {
        var once = GetText("onceKey");
        if (string.IsNullOrEmpty(once))
        {
            return JsonSerializer.Serialize(new { itemId = GetText("itemId"), quantity = GetInt("quantity") });
        }

        return JsonSerializer.Serialize(new
        {
            itemId = GetText("itemId"),
            quantity = GetInt("quantity"),
            onceKey = once,
        });
    }

    private string BuildGoldMutationJson()
    {
        var once = GetText("onceKey");
        if (string.IsNullOrEmpty(once))
        {
            return JsonSerializer.Serialize(new { amount = GetInt("amount") });
        }

        return JsonSerializer.Serialize(new { amount = GetInt("amount"), onceKey = once });
    }

    private string BuildBranchJson(out string? error)
    {
        error = null;
        if (_branchCondition is null || _branchThen is null || _branchElse is null)
        {
            error = "branch: éditeur incomplet.";
            return "{}";
        }

        if (!_branchCondition.TryBuildCondition(out var condition, out error))
        {
            return "{}";
        }

        if (!_branchThen.TryBuildCommands(out var thenCommands, out error))
        {
            return "{}";
        }

        if (!_branchElse.TryBuildCommands(out var elseCommands, out error))
        {
            return "{}";
        }

        return JsonSerializer.Serialize(new
        {
            conditionKind = condition.Kind,
            conditionParameterJson = condition.ParameterJson,
            thenCommands = thenCommands.Select(c => new { discriminator = c.Discriminator, parameterJson = c.ParameterJson }),
            elseCommands = elseCommands.Select(c => new { discriminator = c.Discriminator, parameterJson = c.ParameterJson }),
        });
    }
}
