using Frog.Core.Events;
using Frog.Core.Models;

namespace Frog.Editor.Forms.Phase8;

/// <summary>Liste structurée de commandes (Then/Else d'une branche).</summary>
internal sealed class MapEventCommandListPanel : UserControl
{
    private readonly ListBox _commands = new() { Width = 160, Height = 80 };
    private readonly MapEventCommandParameterPanel _params = new() { AutoSize = true };
    private readonly Label _validationLabel = new() { AutoSize = true, ForeColor = Color.Firebrick };

    private readonly List<MapEventCommandDefinition> _models = new();
    private int _selectedIndex = -1;
    private bool _binding;

    public MapEventCommandListPanel()
    {
        AutoSize = true;
        var add = new Button { Text = "+", AutoSize = true, Width = 28 };
        var remove = new Button { Text = "-", AutoSize = true, Width = 28 };
        add.Click += (_, _) => AddCommand();
        remove.Click += (_, _) => RemoveCommand();

        var buttons = new FlowLayoutPanel { AutoSize = true };
        buttons.Controls.Add(_commands);
        buttons.Controls.Add(add);
        buttons.Controls.Add(remove);

        var layout = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
        };
        layout.Controls.Add(buttons);
        layout.Controls.Add(_params);
        layout.Controls.Add(_validationLabel);
        Controls.Add(layout);

        _commands.SelectedIndexChanged += (_, _) => SelectCommand(_commands.SelectedIndex);
        _params.ParametersChanged += () =>
        {
            FlushCurrent();
            NotifyChanged();
        };
    }

    public event Action? CommandsChanged;

    internal ListBox CommandsForTest => _commands;

    internal MapEventCommandParameterPanel ParamsForTest => _params;

    public void LoadCommands(IReadOnlyList<MapEventCommandDefinition> commands)
    {
        _binding = true;
        try
        {
            _models.Clear();
            _models.AddRange(commands.Select(Clone));
            RefreshList();
            if (_models.Count > 0)
            {
                _commands.SelectedIndex = 0;
                SelectCommand(0);
            }
            else
            {
                _selectedIndex = -1;
            }

            _validationLabel.Text = string.Empty;
        }
        finally
        {
            _binding = false;
        }
    }

    public bool TryBuildCommands(out IReadOnlyList<MapEventCommandDefinition> commands, out string? error)
    {
        FlushCurrent();
        commands = _models.Select(Clone).ToList();
        foreach (var cmd in commands)
        {
            if (!cmd.Validate(out error))
            {
                return false;
            }

            if (!MapEventCommandParameterValidator.ValidateParameters(cmd, out error))
            {
                return false;
            }
        }

        error = null;
        return true;
    }

    private void AddCommand()
    {
        FlushCurrent();
        _models.Add(new MapEventCommandDefinition
        {
            Discriminator = MapEventCommandDiscriminators.ShowText,
            SchemaVersion = 1,
            ParameterJson = """{"text":"…"}""",
        });
        RefreshList();
        _commands.SelectedIndex = _models.Count - 1;
        NotifyChanged();
    }

    private void RemoveCommand()
    {
        if (_selectedIndex < 0 || _selectedIndex >= _models.Count)
        {
            return;
        }

        _models.RemoveAt(_selectedIndex);
        RefreshList();
        _selectedIndex = Math.Min(_selectedIndex, _models.Count - 1);
        if (_selectedIndex >= 0)
        {
            _commands.SelectedIndex = _selectedIndex;
            SelectCommand(_selectedIndex);
        }

        NotifyChanged();
    }

    private void SelectCommand(int index)
    {
        FlushCurrent();
        _selectedIndex = index;
        if (index < 0 || index >= _models.Count)
        {
            return;
        }

        _binding = true;
        try
        {
            _params.LoadCommand(_models[index]);
        }
        finally
        {
            _binding = false;
        }
    }

    private void FlushCurrent()
    {
        if (_binding || _selectedIndex < 0 || _selectedIndex >= _models.Count)
        {
            return;
        }

        if (!_params.TryBuildCommand(out var cmd, out var err))
        {
            _validationLabel.Text = err ?? "Commande invalide.";
            return;
        }

        _models[_selectedIndex] = cmd;
        _validationLabel.Text = string.Empty;
        RefreshList();
        if (_commands.SelectedIndex != _selectedIndex)
        {
            _commands.SelectedIndex = _selectedIndex;
        }
    }

    private void RefreshList()
    {
        _commands.Items.Clear();
        for (var i = 0; i < _models.Count; i++)
        {
            _commands.Items.Add($"{i + 1}. {_models[i].Discriminator}");
        }
    }

    private void NotifyChanged()
    {
        if (!_binding)
        {
            CommandsChanged?.Invoke();
        }
    }

    private static MapEventCommandDefinition Clone(MapEventCommandDefinition c) =>
        new()
        {
            Discriminator = c.Discriminator,
            SchemaVersion = c.SchemaVersion,
            ParameterJson = c.ParameterJson,
        };
}
