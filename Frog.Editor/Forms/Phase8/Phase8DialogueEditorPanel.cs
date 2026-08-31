using Frog.Application.Content;
using Frog.Core.Models;
using Frog.Editor.Services;

namespace Frog.Editor.Forms.Phase8;

/// <summary>Éditeur structuré de dialogue (lignes + choix).</summary>
internal sealed class Phase8DialogueEditorPanel : Phase8EditorPanelBase
{
    private readonly DataGridView _lines = CreateGrid();
    private readonly DataGridView _choices = CreateGrid();

    public Phase8DialogueEditorPanel()
    {
        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterDistance = 220,
        };

        split.Panel1.Controls.Add(WrapSection("Lignes (speaker + texte)", _lines, AddLine, RemoveLine));
        split.Panel2.Controls.Add(WrapSection("Choix (optionnel)", _choices, AddChoice, RemoveChoice));

        _lines.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Speaker", Width = 120 });
        _lines.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Texte", Width = 360 });
        _choices.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "ChoiceId", Width = 120 });
        _choices.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Label", Width = 220 });
        _choices.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "StartQuestId (GUID)", Width = 280 });

        _lines.CellValueChanged += (_, _) => NotifyChanged();
        _choices.CellValueChanged += (_, _) => NotifyChanged();
        _lines.RowsRemoved += (_, _) => NotifyChanged();
        _choices.RowsRemoved += (_, _) => NotifyChanged();
        _lines.CurrentCellDirtyStateChanged += (_, _) => CommitDirty(_lines);
        _choices.CurrentCellDirtyStateChanged += (_, _) => CommitDirty(_choices);

        Controls.Add(split);
    }

    public override Phase8ContentKind Kind => Phase8ContentKind.Dialogue;

    public override void LoadPayload(string payloadJson)
    {
        if (!Phase8ContentPostgreSqlService.TryDeserialize(payloadJson, out DialogueDefinition def, out _))
        {
            def = new DialogueDefinition();
        }

        ContentId = def.Id;
        Binding = true;
        try
        {
            _lines.Rows.Clear();
            foreach (var line in def.Lines)
            {
                _lines.Rows.Add(line.Speaker, line.Text);
            }

            _choices.Rows.Clear();
            foreach (var choice in def.Choices)
            {
                _choices.Rows.Add(
                    choice.ChoiceId,
                    choice.Label,
                    choice.StartQuestId?.ToString("D") ?? string.Empty);
            }
        }
        finally
        {
            Binding = false;
        }
    }

    public override bool TryBuildPayload(out string payloadJson, out string? error)
    {
        var lines = new List<DialogueLineDefinition>();
        foreach (DataGridViewRow row in _lines.Rows)
        {
            if (row.IsNewRow)
            {
                continue;
            }

            lines.Add(new DialogueLineDefinition
            {
                Speaker = Convert.ToString(row.Cells[0].Value) ?? string.Empty,
                Text = Convert.ToString(row.Cells[1].Value) ?? string.Empty,
            });
        }

        var choices = new List<DialogueChoiceDefinition>();
        foreach (DataGridViewRow row in _choices.Rows)
        {
            if (row.IsNewRow)
            {
                continue;
            }

            Guid? questId = null;
            var questRaw = Convert.ToString(row.Cells[2].Value)?.Trim();
            if (!string.IsNullOrEmpty(questRaw))
            {
                if (!Guid.TryParse(questRaw, out var parsed))
                {
                    payloadJson = string.Empty;
                    error = "StartQuestId invalide (GUID attendu).";
                    return false;
                }

                questId = parsed;
            }

            choices.Add(new DialogueChoiceDefinition
            {
                ChoiceId = Convert.ToString(row.Cells[0].Value) ?? string.Empty,
                Label = Convert.ToString(row.Cells[1].Value) ?? string.Empty,
                StartQuestId = questId,
            });
        }

        var def = new DialogueDefinition
        {
            Id = ContentId,
            Name = CatalogName,
            Lines = lines,
            Choices = choices,
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
        LoadPayload(Phase8ContentPostgreSqlService.CreateDefaultPayload(Phase8ContentKind.Dialogue, newId, "Nouveau dialogue"));
    }

    private void AddLine() => _lines.Rows.Add("PNJ", "Texte…");

    private void RemoveLine()
    {
        if (_lines.CurrentRow is { IsNewRow: false } row)
        {
            _lines.Rows.Remove(row);
        }
    }

    private void AddChoice() => _choices.Rows.Add("choice_1", "Option", string.Empty);

    private void RemoveChoice()
    {
        if (_choices.CurrentRow is { IsNewRow: false } row)
        {
            _choices.Rows.Remove(row);
        }
    }

    private static DataGridView CreateGrid() => new()
    {
        Dock = DockStyle.Fill,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AutoGenerateColumns = false,
        RowHeadersVisible = false,
    };

    private static Control WrapSection(string title, DataGridView grid, Action add, Action remove)
    {
        var panel = new TableLayoutPanel { Dock = DockStyle.Fill, RowCount = 2 };
        panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
        panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        var header = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = false };
        header.Controls.Add(new Label { Text = title, AutoSize = true, Margin = new Padding(4, 6, 8, 0) });
        var btnAdd = new Button { Text = "Ajouter", AutoSize = true };
        var btnRemove = new Button { Text = "Retirer", AutoSize = true };
        btnAdd.Click += (_, _) => add();
        btnRemove.Click += (_, _) => remove();
        header.Controls.Add(btnAdd);
        header.Controls.Add(btnRemove);
        panel.Controls.Add(header, 0, 0);
        panel.Controls.Add(grid, 0, 1);
        return panel;
    }

    private static void CommitDirty(DataGridView grid)
    {
        if (grid.IsCurrentCellDirty)
        {
            grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
        }
    }
}
