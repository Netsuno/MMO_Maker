using Frog.Core.Events;
using Frog.Core.Models;
using Frog.Editor.Ui;

namespace Frog.Editor.Forms.Phase8;

/// <summary>Afficher choix : 2 à 4 libellés, annulation, pages de branche.</summary>
internal sealed class MapEventShowChoicesEditor : UserControl
{
    private readonly NumericUpDown _count = new()
    {
        Width = 64,
        Minimum = MapEventShowChoices.MinCount,
        Maximum = MapEventShowChoices.MaxCount,
        Value = MapEventShowChoices.MinCount,
    };

    private readonly ComboBox _cancel = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160 };
    private readonly TextBox[] _labels = new TextBox[MapEventShowChoices.MaxCount];
    private readonly MapEventCommandListPanel[] _branches = new MapEventCommandListPanel[MapEventShowChoices.MaxCount];
    private readonly Control[] _labelRows = new Control[MapEventShowChoices.MaxCount];
    private readonly Control[] _branchRows = new Control[MapEventShowChoices.MaxCount];
    private readonly MapEventCommandListPanel _cancelCommands = new();
    private readonly Control _cancelRow;
    private bool _binding;

    public MapEventShowChoicesEditor()
    {
        AutoSize = true;
        foreach (var cancel in MapEventShowChoices.CancelValues)
        {
            _cancel.Items.Add(cancel);
        }

        _cancel.SelectedItem = MapEventShowChoices.CancelDisallow;
        EditorListDraw.UseReadableChoices(_cancel, MapEventEditorLabels.CancelType);

        var layout = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
        };

        layout.Controls.Add(Row("Nombre", _count));
        for (var i = 0; i < MapEventShowChoices.MaxCount; i++)
        {
            _labels[i] = new TextBox { Width = 220, Text = DefaultLabel(i) };
            _branches[i] = new MapEventCommandListPanel();
            _labelRows[i] = Row($"Choix {i + 1}", _labels[i]);
            _branchRows[i] = Row($"Quand choix {i + 1}", _branches[i]);
            layout.Controls.Add(_labelRows[i]);
            layout.Controls.Add(_branchRows[i]);
            _labels[i].TextChanged += (_, _) => Notify();
            _branches[i].CommandsChanged += () => Notify();
        }

        layout.Controls.Add(Row("Annulation", _cancel));
        _cancelRow = Row("Si annulation", _cancelCommands);
        layout.Controls.Add(_cancelRow);
        Controls.Add(layout);

        _count.ValueChanged += (_, _) =>
        {
            ApplyVisibility();
            Notify();
        };
        _cancel.SelectedIndexChanged += (_, _) =>
        {
            ApplyVisibility();
            Notify();
        };
        _cancelCommands.CommandsChanged += () => Notify();
        ApplyVisibility();
    }

    public event Action? ParametersChanged;

    internal NumericUpDown CountForTest => _count;

    internal ComboBox CancelForTest => _cancel;

    internal TextBox ChoiceTextForTest(int index) => _labels[index];

    internal MapEventCommandListPanel BranchForTest(int index) => _branches[index];

    internal MapEventCommandListPanel CancelCommandsForTest => _cancelCommands;

    public void LoadChoices(string parameterJson)
    {
        _binding = true;
        try
        {
            if (!MapEventParameterSchemas.TryParseShowChoices(
                    parameterJson,
                    out var choices,
                    out var cancel,
                    out var branches,
                    out var cancelCommands,
                    out _))
            {
                return;
            }

            _count.Value = Math.Clamp(choices.Count, (int)_count.Minimum, (int)_count.Maximum);
            for (var i = 0; i < MapEventShowChoices.MaxCount; i++)
            {
                _labels[i].Text = i < choices.Count ? choices[i] : DefaultLabel(i);
                _branches[i].LoadCommands(i < branches.Count ? branches[i] : Array.Empty<MapEventCommandDefinition>());
            }

            var cancelIndex = _cancel.Items.IndexOf(cancel);
            _cancel.SelectedIndex = cancelIndex >= 0 ? cancelIndex : 0;
            _cancelCommands.LoadCommands(cancelCommands);
            ApplyVisibility();
        }
        finally
        {
            _binding = false;
        }
    }

    public bool TryBuild(out string json, out string? error)
    {
        error = null;
        var count = (int)_count.Value;
        var labels = new string[count];
        var branches = new IReadOnlyList<MapEventCommandDefinition>[count];
        for (var i = 0; i < count; i++)
        {
            labels[i] = _labels[i].Text.Trim();
            if (!_branches[i].TryBuildCommands(out var commands, out error))
            {
                json = "{}";
                return false;
            }

            branches[i] = commands;
        }

        var cancel = _cancel.SelectedItem as string ?? MapEventShowChoices.CancelDisallow;
        IReadOnlyList<MapEventCommandDefinition> cancelCommands = Array.Empty<MapEventCommandDefinition>();
        if (cancel == MapEventShowChoices.CancelBranch
            && !_cancelCommands.TryBuildCommands(out cancelCommands, out error))
        {
            json = "{}";
            return false;
        }

        json = MapEventParameterSchemas.SerializeShowChoices(labels, cancel, branches, cancelCommands);
        return true;
    }

    private void ApplyVisibility()
    {
        var count = (int)_count.Value;
        for (var i = 0; i < MapEventShowChoices.MaxCount; i++)
        {
            var visible = i < count;
            _labelRows[i].Visible = visible;
            _branchRows[i].Visible = visible;
        }

        var cancel = _cancel.SelectedItem as string;
        if (MapEventShowChoices.CancelChoiceIndex(cancel) >= count)
        {
            _cancel.SelectedItem = MapEventShowChoices.CancelDisallow;
            cancel = MapEventShowChoices.CancelDisallow;
        }

        _cancelRow.Visible = cancel == MapEventShowChoices.CancelBranch;
    }

    private void Notify()
    {
        if (!_binding)
        {
            ParametersChanged?.Invoke();
        }
    }

    private static string DefaultLabel(int index) => index switch
    {
        0 => "Oui",
        1 => "Non",
        2 => "Peut-être",
        _ => "Autre",
    };

    private static Control Row(string caption, Control editor)
    {
        var row = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        row.Controls.Add(new Label
        {
            Text = caption,
            AutoSize = true,
            MinimumSize = new Size(120, 0),
            Margin = new Padding(0, 6, 8, 0),
        });
        row.Controls.Add(editor);
        return row;
    }
}
