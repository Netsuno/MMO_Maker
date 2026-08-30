using Frog.Core.Events;
using Frog.Core.Models;

namespace Frog.Editor.Forms.Phase8;

/// <summary>Éditeur structuré de pages d'événement (P8-I2).</summary>
internal sealed class MapEventPagesEditorPanel : UserControl
{
    private readonly ListBox _pages = new() { Width = 160, Height = 120 };
    private readonly NumericUpDown _priority = new() { Width = 80, Minimum = 0, Maximum = 9999 };
    private readonly ComboBox _trigger = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
    private readonly ComboBox _movement = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
    private readonly DataGridView _waypoints = CreateWaypointGrid();
    private readonly NumericUpDown _appearanceGraphic = new() { Width = 60, Minimum = 0, Maximum = 255 };
    private readonly NumericUpDown _appearanceDirection = new() { Width = 60, Minimum = 0, Maximum = 7 };
    private readonly CheckBox _blocksCollision = new() { Text = "Bloque collision", Checked = true, AutoSize = true };
    private readonly DataGridView _conditions = CreateConditionGrid();
    private readonly ListBox _commands = new() { Width = 160, Height = 100 };
    private readonly MapEventCommandParameterPanel _commandParams = new() { AutoSize = true };
    private readonly Label _validationLabel = new() { AutoSize = true, ForeColor = Color.Firebrick, MaximumSize = new Size(640, 0) };

    private readonly List<MapEventPageDefinition> _pageModels = new();
    private readonly List<MapEventCommandDefinition> _commandModels = new();
    private int _selectedPageIndex = -1;
    private int _selectedCommandIndex = -1;
    private bool _binding;

    public MapEventPagesEditorPanel()
    {
        Dock = DockStyle.Fill;
        AutoScroll = true;

        foreach (var kind in Phase8MapEventTriggerKinds.All.OrderBy(k => k, StringComparer.Ordinal))
        {
            _trigger.Items.Add(kind);
        }

        foreach (var mk in MapEventMovementKinds.All.OrderBy(k => k, StringComparer.Ordinal))
        {
            _movement.Items.Add(mk);
        }

        if (_trigger.Items.Count > 0)
        {
            _trigger.SelectedIndex = 0;
        }

        if (_movement.Items.Count > 0)
        {
            _movement.SelectedIndex = 0;
        }

        _conditions.Columns.Add(new DataGridViewComboBoxColumn
        {
            HeaderText = "Kind",
            Width = 180,
            DataSource = MapEventConditionKinds.All.OrderBy(k => k, StringComparer.Ordinal).ToList(),
        });
        _conditions.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "ParameterJson", Width = 360 });

        var root = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            AutoSize = true,
            AutoScroll = true,
            Padding = new Padding(8),
        };
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));

        void Row(string label, Control control)
        {
            var row = root.RowCount++;
            root.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
            root.Controls.Add(control, 1, row);
        }

        var pageButtons = new FlowLayoutPanel { AutoSize = true };
        var btnAddPage = new Button { Text = "Ajouter page", AutoSize = true };
        var btnRemovePage = new Button { Text = "Retirer page", AutoSize = true };
        btnAddPage.Click += (_, _) => AddPage();
        btnRemovePage.Click += (_, _) => RemovePage();
        pageButtons.Controls.Add(_pages);
        pageButtons.Controls.Add(btnAddPage);
        pageButtons.Controls.Add(btnRemovePage);
        Row("Pages", pageButtons);

        Row("Priorité", _priority);
        Row("TriggerKind", _trigger);
        Row("MovementKind", _movement);

        var wpButtons = new FlowLayoutPanel { AutoSize = true };
        var btnAddWp = new Button { Text = "+ waypoint", AutoSize = true };
        var btnRemoveWp = new Button { Text = "- waypoint", AutoSize = true };
        btnAddWp.Click += (_, _) => { _waypoints.Rows.Add(0, 0, 250); OnPageFieldChanged(); };
        btnRemoveWp.Click += (_, _) =>
        {
            if (_waypoints.CurrentRow is { IsNewRow: false } row)
            {
                _waypoints.Rows.Remove(row);
                OnPageFieldChanged();
            }
        };
        wpButtons.Controls.Add(_waypoints);
        wpButtons.Controls.Add(btnAddWp);
        wpButtons.Controls.Add(btnRemoveWp);
        Row("Route waypoints", wpButtons);

        var appearance = new FlowLayoutPanel { AutoSize = true };
        appearance.Controls.Add(new Label { Text = "GraphicId", AutoSize = true });
        appearance.Controls.Add(_appearanceGraphic);
        appearance.Controls.Add(new Label { Text = "Direction", AutoSize = true, Margin = new Padding(12, 0, 0, 0) });
        appearance.Controls.Add(_appearanceDirection);
        appearance.Controls.Add(_blocksCollision);
        Row("Apparence / collision", appearance);

        var condButtons = new FlowLayoutPanel { AutoSize = true };
        var btnAddCond = new Button { Text = "+ condition", AutoSize = true };
        var btnRemoveCond = new Button { Text = "- condition", AutoSize = true };
        btnAddCond.Click += (_, _) =>
        {
            _conditions.Rows.Add(MapEventConditionKinds.CharacterSwitch, "{\"switchId\":\"x\",\"value\":true}");
            OnPageFieldChanged();
        };
        btnRemoveCond.Click += (_, _) =>
        {
            if (_conditions.CurrentRow is { IsNewRow: false } row)
            {
                _conditions.Rows.Remove(row);
                OnPageFieldChanged();
            }
        };
        condButtons.Controls.Add(_conditions);
        condButtons.Controls.Add(btnAddCond);
        condButtons.Controls.Add(btnRemoveCond);
        Row("Conditions", condButtons);

        var cmdButtons = new FlowLayoutPanel { AutoSize = true };
        var btnAddCmd = new Button { Text = "+ commande", AutoSize = true };
        var btnRemoveCmd = new Button { Text = "- commande", AutoSize = true };
        btnAddCmd.Click += (_, _) => AddCommand();
        btnRemoveCmd.Click += (_, _) => RemoveCommand();
        cmdButtons.Controls.Add(_commands);
        cmdButtons.Controls.Add(btnAddCmd);
        cmdButtons.Controls.Add(btnRemoveCmd);
        Row("Commandes", cmdButtons);
        Row("Paramètres commande", _commandParams);
        Row("Validation", _validationLabel);

        Controls.Add(root);

        _priority.ValueChanged += (_, _) => OnPageFieldChanged();
        _trigger.SelectedIndexChanged += (_, _) => OnPageFieldChanged();
        _movement.SelectedIndexChanged += (_, _) => OnPageFieldChanged();
        _appearanceGraphic.ValueChanged += (_, _) => OnPageFieldChanged();
        _appearanceDirection.ValueChanged += (_, _) => OnPageFieldChanged();
        _blocksCollision.CheckedChanged += (_, _) => OnPageFieldChanged();
        _pages.SelectedIndexChanged += (_, _) => SelectPage(_pages.SelectedIndex);
        _commands.SelectedIndexChanged += (_, _) => SelectCommand(_commands.SelectedIndex);
        _commandParams.ParametersChanged += () => OnCommandFieldChanged();
        _waypoints.CellValueChanged += (_, _) => OnPageFieldChanged();
        _conditions.CellValueChanged += (_, _) => OnPageFieldChanged();
        _waypoints.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_waypoints.IsCurrentCellDirty)
            {
                _waypoints.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        };
        _conditions.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_conditions.IsCurrentCellDirty)
            {
                _conditions.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        };
    }

    public event Action? PagesChanged;

    internal ListBox PagesForTest => _pages;

    internal ComboBox TriggerForTest => _trigger;

    internal NumericUpDown PriorityForTest => _priority;

    internal ComboBox MovementForTest => _movement;

    internal DataGridView WaypointsForTest => _waypoints;

    internal DataGridView ConditionsForTest => _conditions;

    internal ListBox CommandsForTest => _commands;

    internal MapEventCommandParameterPanel CommandParamsForTest => _commandParams;

    internal Label ValidationLabelForTest => _validationLabel;

    public void LoadPages(IReadOnlyList<MapEventPageDefinition> pages)
    {
        _binding = true;
        try
        {
            _pageModels.Clear();
            _pageModels.AddRange(pages.Select(ClonePage));
            RefreshPageList();
            if (_pageModels.Count > 0)
            {
                _pages.SelectedIndex = 0;
                SelectPage(0);
            }
            else
            {
                _selectedPageIndex = -1;
                ClearPageUi();
            }

            _validationLabel.Text = string.Empty;
        }
        finally
        {
            _binding = false;
        }
    }

    public bool TryBuildPages(out IReadOnlyList<MapEventPageDefinition> pages, out string? error)
    {
        FlushCurrentCommand();
        FlushCurrentPage();
        pages = _pageModels.Select(ClonePage).ToList();
        for (var i = 0; i < pages.Count; i++)
        {
            if (!pages[i].Validate(out error))
            {
                error = $"Page {i + 1}: {error}";
                _validationLabel.Text = error;
                return false;
            }
        }

        error = null;
        _validationLabel.Text = string.Empty;
        return true;
    }

    private void AddPage()
    {
        FlushCurrentPage();
        _pageModels.Add(new MapEventPageDefinition
        {
            PageOrder = _pageModels.Count,
            TriggerKind = Phase8MapEventTriggerKinds.Action,
            Commands = Array.Empty<MapEventCommandDefinition>(),
        });
        RefreshPageList();
        _pages.SelectedIndex = _pageModels.Count - 1;
        NotifyChanged();
    }

    private void RemovePage()
    {
        if (_selectedPageIndex < 0 || _selectedPageIndex >= _pageModels.Count)
        {
            return;
        }

        _pageModels.RemoveAt(_selectedPageIndex);
        for (var i = 0; i < _pageModels.Count; i++)
        {
            _pageModels[i].PageOrder = i;
        }

        RefreshPageList();
        _selectedPageIndex = Math.Min(_selectedPageIndex, _pageModels.Count - 1);
        if (_selectedPageIndex >= 0)
        {
            _pages.SelectedIndex = _selectedPageIndex;
            SelectPage(_selectedPageIndex);
        }
        else
        {
            ClearPageUi();
        }

        NotifyChanged();
    }

    private void SelectPage(int index)
    {
        FlushCurrentCommand();
        FlushCurrentPage();
        _selectedPageIndex = index;
        if (index < 0 || index >= _pageModels.Count)
        {
            return;
        }

        _binding = true;
        try
        {
            var page = _pageModels[index];
            _priority.Value = Math.Clamp(page.Priority, (int)_priority.Minimum, (int)_priority.Maximum);
            var triggerIndex = _trigger.Items.IndexOf(page.TriggerKind);
            _trigger.SelectedIndex = triggerIndex >= 0 ? triggerIndex : 0;
            var moveIndex = _movement.Items.IndexOf(page.MovementKind);
            _movement.SelectedIndex = moveIndex >= 0 ? moveIndex : 0;
            _appearanceGraphic.Value = page.AppearanceGraphicId;
            _appearanceDirection.Value = page.AppearanceDirection;
            _blocksCollision.Checked = page.BlocksCollision;

            _waypoints.Rows.Clear();
            foreach (var wp in page.RouteWaypoints)
            {
                _waypoints.Rows.Add(wp.TileX, wp.TileY, wp.WaitMs);
            }

            _conditions.Rows.Clear();
            foreach (var cond in page.Conditions)
            {
                _conditions.Rows.Add(cond.Kind, cond.ParameterJson);
            }

            _commandModels.Clear();
            _commandModels.AddRange(page.Commands.Select(c => new MapEventCommandDefinition
            {
                Discriminator = c.Discriminator,
                SchemaVersion = c.SchemaVersion,
                ParameterJson = c.ParameterJson,
            }));
            RefreshCommandList();
            if (_commandModels.Count > 0)
            {
                _commands.SelectedIndex = 0;
                SelectCommand(0);
            }
            else
            {
                _selectedCommandIndex = -1;
            }
        }
        finally
        {
            _binding = false;
        }
    }

    private void SelectCommand(int index)
    {
        FlushCurrentCommand();
        _selectedCommandIndex = index;
        if (index < 0 || index >= _commandModels.Count)
        {
            return;
        }

        _binding = true;
        try
        {
            _commandParams.LoadCommand(_commandModels[index]);
        }
        finally
        {
            _binding = false;
        }
    }

    private void AddCommand()
    {
        FlushCurrentCommand();
        _commandModels.Add(new MapEventCommandDefinition
        {
            Discriminator = MapEventCommandDiscriminators.ShowText,
            SchemaVersion = 1,
            ParameterJson = "{\"text\":\"…\"}",
        });
        RefreshCommandList();
        _commands.SelectedIndex = _commandModels.Count - 1;
        OnPageFieldChanged();
    }

    private void RemoveCommand()
    {
        if (_selectedCommandIndex < 0 || _selectedCommandIndex >= _commandModels.Count)
        {
            return;
        }

        _commandModels.RemoveAt(_selectedCommandIndex);
        RefreshCommandList();
        _selectedCommandIndex = Math.Min(_selectedCommandIndex, _commandModels.Count - 1);
        if (_selectedCommandIndex >= 0)
        {
            _commands.SelectedIndex = _selectedCommandIndex;
            SelectCommand(_selectedCommandIndex);
        }

        OnPageFieldChanged();
    }

    private void OnPageFieldChanged()
    {
        FlushCurrentCommand();
        FlushCurrentPage();
        NotifyChanged();
    }

    private void OnCommandFieldChanged()
    {
        FlushCurrentCommand();
        NotifyChanged();
    }

    private void FlushCurrentCommand()
    {
        if (_binding || _selectedCommandIndex < 0 || _selectedCommandIndex >= _commandModels.Count)
        {
            return;
        }

        if (!_commandParams.TryBuildCommand(out var cmd, out var err))
        {
            _validationLabel.Text = err ?? "Commande invalide.";
            return;
        }

        _commandModels[_selectedCommandIndex] = cmd;
        _binding = true;
        try
        {
            RefreshCommandList();
            if (_commands.SelectedIndex != _selectedCommandIndex)
            {
                _commands.SelectedIndex = _selectedCommandIndex;
            }
        }
        finally
        {
            _binding = false;
        }
    }

    private void FlushCurrentPage()
    {
        if (_binding || _selectedPageIndex < 0 || _selectedPageIndex >= _pageModels.Count)
        {
            return;
        }

        var conditions = new List<MapEventConditionDefinition>();
        foreach (DataGridViewRow row in _conditions.Rows)
        {
            if (row.IsNewRow)
            {
                continue;
            }

            var kind = Convert.ToString(row.Cells[0].Value) ?? string.Empty;
            var paramJson = Convert.ToString(row.Cells[1].Value) ?? "{}";
            conditions.Add(new MapEventConditionDefinition { Kind = kind, ParameterJson = paramJson });
        }

        var waypoints = new List<MapEventRouteWaypoint>();
        foreach (DataGridViewRow row in _waypoints.Rows)
        {
            if (row.IsNewRow)
            {
                continue;
            }

            waypoints.Add(new MapEventRouteWaypoint
            {
                TileX = int.TryParse(Convert.ToString(row.Cells[0].Value), out var tx) ? tx : 0,
                TileY = int.TryParse(Convert.ToString(row.Cells[1].Value), out var ty) ? ty : 0,
                WaitMs = int.TryParse(Convert.ToString(row.Cells[2].Value), out var wait) ? wait : 250,
            });
        }

        var existing = _pageModels[_selectedPageIndex];
        _pageModels[_selectedPageIndex] = new MapEventPageDefinition
        {
            PageOrder = existing.PageOrder,
            Priority = (int)_priority.Value,
            TriggerKind = _trigger.SelectedItem as string ?? Phase8MapEventTriggerKinds.Action,
            MovementKind = _movement.SelectedItem as string ?? MapEventMovementKinds.Fixed,
            RouteWaypoints = waypoints,
            AppearanceGraphicId = (byte)_appearanceGraphic.Value,
            AppearanceDirection = (byte)_appearanceDirection.Value,
            BlocksCollision = _blocksCollision.Checked,
            Conditions = conditions,
            Commands = _commandModels.Select(c => new MapEventCommandDefinition
            {
                Discriminator = c.Discriminator,
                SchemaVersion = c.SchemaVersion,
                ParameterJson = c.ParameterJson,
            }).ToList(),
        };
    }

    private void ClearPageUi()
    {
        _waypoints.Rows.Clear();
        _conditions.Rows.Clear();
        _commandModels.Clear();
        _commands.Items.Clear();
    }

    private void RefreshPageList()
    {
        _pages.Items.Clear();
        for (var i = 0; i < _pageModels.Count; i++)
        {
            var page = _pageModels[i];
            _pages.Items.Add($"P{i + 1} pri={page.Priority} {page.TriggerKind}");
        }
    }

    private void RefreshCommandList()
    {
        _commands.Items.Clear();
        for (var i = 0; i < _commandModels.Count; i++)
        {
            _commands.Items.Add($"{i + 1}. {_commandModels[i].Discriminator}");
        }
    }

    private void NotifyChanged()
    {
        if (!_binding)
        {
            PagesChanged?.Invoke();
        }
    }

    private static MapEventPageDefinition ClonePage(MapEventPageDefinition page) =>
        new()
        {
            PageOrder = page.PageOrder,
            Priority = page.Priority,
            TriggerKind = page.TriggerKind,
            MovementKind = page.MovementKind,
            RouteWaypoints = page.RouteWaypoints.ToList(),
            AppearanceGraphicId = page.AppearanceGraphicId,
            AppearanceDirection = page.AppearanceDirection,
            BlocksCollision = page.BlocksCollision,
            Conditions = page.Conditions.Select(c => new MapEventConditionDefinition
            {
                Kind = c.Kind,
                ParameterJson = c.ParameterJson,
            }).ToList(),
            Commands = page.Commands.Select(c => new MapEventCommandDefinition
            {
                Discriminator = c.Discriminator,
                SchemaVersion = c.SchemaVersion,
                ParameterJson = c.ParameterJson,
            }).ToList(),
        };

    private static DataGridView CreateWaypointGrid()
    {
        var grid = new DataGridView
        {
            Width = 360,
            Height = 90,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            RowHeadersVisible = false,
        };
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "TileX", Width = 60 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "TileY", Width = 60 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "WaitMs", Width = 80 });
        return grid;
    }

    private static DataGridView CreateConditionGrid() => new()
    {
        Width = 560,
        Height = 90,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AutoGenerateColumns = false,
        RowHeadersVisible = false,
    };
}
