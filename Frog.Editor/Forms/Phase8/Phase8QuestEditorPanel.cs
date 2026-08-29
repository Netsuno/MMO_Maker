using Frog.Application.Content;
using Frog.Core.Models;
using Frog.Editor.Services;

namespace Frog.Editor.Forms.Phase8;

/// <summary>Éditeur structuré de quête (étapes, objectifs, récompenses).</summary>
internal sealed class Phase8QuestEditorPanel : Phase8EditorPanelBase
{
    private readonly CheckBox _repeatable = new() { Text = "Répétable", AutoSize = true };
    private readonly TextBox _prerequisites = new() { Width = 400, PlaceholderText = "GUIDs séparés par des virgules" };
    private readonly ListBox _stages = new() { Width = 180, Height = 120 };
    private readonly TextBox _stageDescription = new() { Width = 400 };
    private readonly DataGridView _objectives = CreateGrid();
    private readonly NumericUpDown _rewardGold = new() { Minimum = 0, Maximum = int.MaxValue, Width = 120 };
    private readonly TextBox _rewardItemId = new() { Width = 280, PlaceholderText = "GUID objet (optionnel)" };
    private readonly NumericUpDown _rewardItemQty = new() { Minimum = 1, Maximum = 9999, Width = 80, Value = 1 };

    private readonly List<QuestStageDefinition> _stageModels = new();
    private int _selectedStageIndex = -1;

    public Phase8QuestEditorPanel()
    {
        _objectives.Columns.Add(new DataGridViewComboBoxColumn
        {
            HeaderText = "Kind",
            Width = 90,
            DataSource = Enum.GetValues<QuestObjectiveKind>(),
        });
        _objectives.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Description", Width = 180 });
        _objectives.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Count", Width = 60 });
        _objectives.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "TargetNpcId", Width = 200 });
        _objectives.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "TargetItemId", Width = 200 });
        _objectives.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "TargetRecipeId", Width = 200 });
        _objectives.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "TargetMapId", Width = 80 });
        _objectives.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "TargetDialogueId", Width = 200 });

        var form = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            AutoScroll = true,
            Padding = new Padding(8),
        };
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        void Row(string label, Control control)
        {
            var row = form.RowCount++;
            form.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
            form.Controls.Add(control, 1, row);
        }

        Row("Répétable", _repeatable);
        Row("Prérequis (GUIDs)", _prerequisites);

        var stagePanel = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        var stageButtons = new FlowLayoutPanel { AutoSize = true };
        var btnAddStage = new Button { Text = "Ajouter étape", AutoSize = true };
        var btnRemoveStage = new Button { Text = "Retirer étape", AutoSize = true };
        btnAddStage.Click += (_, _) => AddStage();
        btnRemoveStage.Click += (_, _) => RemoveStage();
        stageButtons.Controls.Add(btnAddStage);
        stageButtons.Controls.Add(btnRemoveStage);
        stagePanel.Controls.Add(_stages);
        stagePanel.Controls.Add(stageButtons);
        stagePanel.Controls.Add(new Label { Text = "Description étape", AutoSize = true });
        stagePanel.Controls.Add(_stageDescription);

        var objPanel = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        objPanel.Controls.Add(_objectives);
        var objButtons = new FlowLayoutPanel { AutoSize = true };
        var btnAddObj = new Button { Text = "Ajouter objectif", AutoSize = true };
        var btnRemoveObj = new Button { Text = "Retirer objectif", AutoSize = true };
        btnAddObj.Click += (_, _) => AddObjective();
        btnRemoveObj.Click += (_, _) => RemoveObjective();
        objButtons.Controls.Add(btnAddObj);
        objButtons.Controls.Add(btnRemoveObj);
        objPanel.Controls.Add(objButtons);

        stagePanel.Controls.Add(new Label { Text = "Objectifs", AutoSize = true, Margin = new Padding(0, 8, 0, 0) });
        stagePanel.Controls.Add(objPanel);
        Row("Étapes", stagePanel);

        var rewardPanel = new FlowLayoutPanel { AutoSize = true, WrapContents = true };
        rewardPanel.Controls.Add(new Label { Text = "Or", AutoSize = true, Margin = new Padding(0, 8, 4, 0) });
        rewardPanel.Controls.Add(_rewardGold);
        rewardPanel.Controls.Add(new Label { Text = "Objet", AutoSize = true, Margin = new Padding(12, 8, 4, 0) });
        rewardPanel.Controls.Add(_rewardItemId);
        rewardPanel.Controls.Add(new Label { Text = "Qté", AutoSize = true, Margin = new Padding(8, 8, 4, 0) });
        rewardPanel.Controls.Add(_rewardItemQty);
        Row("Récompense fin", rewardPanel);

        Controls.Add(form);

        _repeatable.CheckedChanged += (_, _) => NotifyChanged();
        _prerequisites.TextChanged += (_, _) => NotifyChanged();
        _stageDescription.TextChanged += (_, _) => OnStageFieldChanged();
        _stages.SelectedIndexChanged += (_, _) => SelectStage(_stages.SelectedIndex);
        _objectives.CellValueChanged += (_, _) => OnStageFieldChanged();
        _objectives.RowsRemoved += (_, _) => OnStageFieldChanged();
        _objectives.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_objectives.IsCurrentCellDirty)
            {
                _objectives.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        };
        _rewardGold.ValueChanged += (_, _) => NotifyChanged();
        _rewardItemId.TextChanged += (_, _) => NotifyChanged();
        _rewardItemQty.ValueChanged += (_, _) => NotifyChanged();
    }

    public override Phase8ContentKind Kind => Phase8ContentKind.Quest;

    public override void LoadPayload(string payloadJson)
    {
        if (!Phase8ContentPostgreSqlService.TryDeserialize(payloadJson, out QuestDefinition def, out _))
        {
            def = new QuestDefinition();
        }

        ContentId = def.Id;
        Binding = true;
        try
        {
            _repeatable.Checked = def.Repeatable;
            _prerequisites.Text = string.Join(", ", def.PrerequisiteQuestIds.Select(id => id.ToString("D")));
            _stageModels.Clear();
            _stageModels.AddRange(def.Stages.Select(CloneStage));
            _stages.Items.Clear();
            for (var i = 0; i < _stageModels.Count; i++)
            {
                _stages.Items.Add($"Étape {i + 1}");
            }

            if (_stageModels.Count > 0)
            {
                _stages.SelectedIndex = 0;
                SelectStage(0);
            }
            else
            {
                _selectedStageIndex = -1;
                _stageDescription.Clear();
                _objectives.Rows.Clear();
            }

            if (def.CompletionReward is { } reward)
            {
                _rewardGold.Value = Math.Max(0, reward.Gold);
                _rewardItemId.Text = reward.ItemId?.ToString("D") ?? string.Empty;
                _rewardItemQty.Value = Math.Max(1, reward.ItemQuantity);
            }
            else
            {
                _rewardGold.Value = 0;
                _rewardItemId.Clear();
                _rewardItemQty.Value = 1;
            }
        }
        finally
        {
            Binding = false;
        }
    }

    public override bool TryBuildPayload(out string payloadJson, out string? error)
    {
        FlushCurrentStage();
        var prereqs = ParseGuidList(_prerequisites.Text, out error);
        if (error is not null)
        {
            payloadJson = string.Empty;
            return false;
        }

        QuestRewardDefinition? reward = null;
        if (_rewardGold.Value > 0 || !string.IsNullOrWhiteSpace(_rewardItemId.Text))
        {
            Guid? itemId = null;
            if (!string.IsNullOrWhiteSpace(_rewardItemId.Text))
            {
                if (!Guid.TryParse(_rewardItemId.Text.Trim(), out var parsed))
                {
                    payloadJson = string.Empty;
                    error = "ItemId de récompense invalide.";
                    return false;
                }

                itemId = parsed;
            }

            reward = new QuestRewardDefinition
            {
                Gold = (int)_rewardGold.Value,
                ItemId = itemId,
                ItemQuantity = (int)_rewardItemQty.Value,
            };
        }

        var def = new QuestDefinition
        {
            Id = ContentId,
            Repeatable = _repeatable.Checked,
            PrerequisiteQuestIds = prereqs,
            Stages = _stageModels.Select(CloneStage).ToList(),
            CompletionReward = reward,
        };
        if (!def.Validate(out error))
        {
            payloadJson = string.Empty;
            return false;
        }

        payloadJson = Phase8ContentPostgreSqlService.Serialize(def);
        error = null;
        return true;
    }

    public override void ResetForNew(Guid newId)
    {
        LoadPayload(Phase8ContentPostgreSqlService.CreateDefaultPayload(Phase8ContentKind.Quest, newId, "Nouvelle quête"));
    }

    private void AddStage()
    {
        FlushCurrentStage();
        _stageModels.Add(new QuestStageDefinition
        {
            Description = $"Étape {_stageModels.Count + 1}",
            Objectives = Array.Empty<QuestObjectiveDefinition>(),
        });
        _stages.Items.Add($"Étape {_stageModels.Count}");
        _stages.SelectedIndex = _stageModels.Count - 1;
        NotifyChanged();
    }

    private void RemoveStage()
    {
        if (_selectedStageIndex < 0 || _selectedStageIndex >= _stageModels.Count)
        {
            return;
        }

        _stageModels.RemoveAt(_selectedStageIndex);
        _stages.Items.Clear();
        for (var i = 0; i < _stageModels.Count; i++)
        {
            _stages.Items.Add($"Étape {i + 1}");
        }

        _selectedStageIndex = Math.Min(_selectedStageIndex, _stageModels.Count - 1);
        if (_selectedStageIndex >= 0)
        {
            _stages.SelectedIndex = _selectedStageIndex;
            SelectStage(_selectedStageIndex);
        }
        else
        {
            _stageDescription.Clear();
            _objectives.Rows.Clear();
        }

        NotifyChanged();
    }

    private void SelectStage(int index)
    {
        FlushCurrentStage();
        _selectedStageIndex = index;
        if (index < 0 || index >= _stageModels.Count)
        {
            return;
        }

        Binding = true;
        try
        {
            var stage = _stageModels[index];
            _stageDescription.Text = stage.Description;
            _objectives.Rows.Clear();
            foreach (var obj in stage.Objectives)
            {
                _objectives.Rows.Add(
                    obj.Kind,
                    obj.Description,
                    obj.RequiredCount,
                    obj.TargetNpcId?.ToString("D") ?? string.Empty,
                    obj.TargetItemId?.ToString("D") ?? string.Empty,
                    obj.TargetRecipeId?.ToString("D") ?? string.Empty,
                    obj.TargetMapId?.ToString() ?? string.Empty,
                    obj.TargetDialogueId?.ToString("D") ?? string.Empty);
            }
        }
        finally
        {
            Binding = false;
        }
    }

    private void OnStageFieldChanged()
    {
        FlushCurrentStage();
        NotifyChanged();
    }

    private void FlushCurrentStage()
    {
        if (Binding || _selectedStageIndex < 0 || _selectedStageIndex >= _stageModels.Count)
        {
            return;
        }

        var objectives = new List<QuestObjectiveDefinition>();
        foreach (DataGridViewRow row in _objectives.Rows)
        {
            if (row.IsNewRow)
            {
                continue;
            }

            objectives.Add(new QuestObjectiveDefinition
            {
                Kind = row.Cells[0].Value is QuestObjectiveKind k ? k : QuestObjectiveKind.Visit,
                Description = Convert.ToString(row.Cells[1].Value) ?? string.Empty,
                RequiredCount = ParseInt(row.Cells[2].Value, 1),
                TargetNpcId = ParseOptionalGuid(row.Cells[3].Value),
                TargetItemId = ParseOptionalGuid(row.Cells[4].Value),
                TargetRecipeId = ParseOptionalGuid(row.Cells[5].Value),
                TargetMapId = ParseOptionalInt(row.Cells[6].Value),
                TargetDialogueId = ParseOptionalGuid(row.Cells[7].Value),
            });
        }

        _stageModels[_selectedStageIndex] = new QuestStageDefinition
        {
            Description = _stageDescription.Text.Trim(),
            Objectives = objectives,
        };
    }

    private void AddObjective()
    {
        _objectives.Rows.Add(QuestObjectiveKind.Visit, "Objectif", 1, string.Empty, string.Empty, string.Empty, "1", string.Empty);
        OnStageFieldChanged();
    }

    private void RemoveObjective()
    {
        if (_objectives.CurrentRow is { IsNewRow: false } row)
        {
            _objectives.Rows.Remove(row);
            OnStageFieldChanged();
        }
    }

    private static QuestStageDefinition CloneStage(QuestStageDefinition stage) =>
        new()
        {
            Description = stage.Description,
            Objectives = stage.Objectives.Select(o => new QuestObjectiveDefinition
            {
                Kind = o.Kind,
                Description = o.Description,
                RequiredCount = o.RequiredCount,
                TargetNpcId = o.TargetNpcId,
                TargetItemId = o.TargetItemId,
                TargetRecipeId = o.TargetRecipeId,
                TargetMapId = o.TargetMapId,
                TargetTileX = o.TargetTileX,
                TargetTileY = o.TargetTileY,
                TargetDialogueId = o.TargetDialogueId,
            }).ToList(),
        };

    private static List<Guid> ParseGuidList(string text, out string? error)
    {
        error = null;
        var list = new List<Guid>();
        foreach (var part in text.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            if (!Guid.TryParse(part, out var id))
            {
                error = $"GUID prérequis invalide : {part}";
                return list;
            }

            list.Add(id);
        }

        return list;
    }

    private static Guid? ParseOptionalGuid(object? value)
    {
        var raw = Convert.ToString(value)?.Trim();
        return string.IsNullOrEmpty(raw) ? null : Guid.TryParse(raw, out var id) ? id : null;
    }

    private static int? ParseOptionalInt(object? value)
    {
        var raw = Convert.ToString(value)?.Trim();
        return string.IsNullOrEmpty(raw) ? null : int.TryParse(raw, out var n) ? n : null;
    }

    private static int ParseInt(object? value, int fallback) =>
        int.TryParse(Convert.ToString(value), out var n) ? n : fallback;

    private static DataGridView CreateGrid() => new()
    {
        Width = 720,
        Height = 160,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AutoGenerateColumns = false,
        RowHeadersVisible = false,
    };
}
