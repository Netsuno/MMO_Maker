using Frog.Core.Events;
using Frog.Core.Models;
using Frog.Editor.Ui;

namespace Frog.Editor.Forms.Phase8;

/// <summary>Éditeur structuré de pages d'événement (P8-I2).</summary>
internal sealed class MapEventPagesEditorPanel : UserControl
{
    private readonly ListBox _pages = new()
    {
        Width = 460,
        Height = 112,
        Font = EditorChrome.BodyFont,
    };
    private readonly Label _activePageCaption = new()
    {
        AutoSize = true,
        MaximumSize = new Size(820, 0),
        Font = EditorChrome.CaptionFont,
        ForeColor = Color.White,
        BackColor = Color.FromArgb(26, 61, 88),
        Padding = new Padding(10, 6, 10, 6),
        Margin = new Padding(0, 0, 0, 6),
    };
    private readonly NumericUpDown _priority = new() { Width = 80, Minimum = 0, Maximum = 9999 };
    private readonly ComboBox _trigger = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 240, Font = EditorChrome.BodyFont };
    private readonly ComboBox _movement = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 240, Font = EditorChrome.BodyFont };
    private readonly DataGridView _waypoints = CreateWaypointGrid();
    private readonly NumericUpDown _appearanceGraphic = new() { Width = 60, Minimum = 0, Maximum = 255 };
    private readonly NumericUpDown _appearanceDirection = new() { Width = 60, Minimum = 0, Maximum = 7 };
    private readonly CheckBox _blocksCollision = new() { Text = "Bloque la collision", Checked = true, AutoSize = true };
    private readonly ListBox _conditions = new()
    {
        Width = 460,
        Height = 96,
        Font = EditorChrome.BodyFont,
    };
    private readonly MapEventConditionParameterPanel _conditionParams = new() { AutoSize = true };
    private readonly ListBox _commands = new()
    {
        Width = 460,
        Height = 120,
        Font = EditorChrome.BodyFont,
    };
    private readonly MapEventCommandParameterPanel _commandParams = new() { AutoSize = true };
    private readonly Label _validationLabel = new() { AutoSize = true, ForeColor = Color.Firebrick, MaximumSize = new Size(820, 0) };

    private readonly List<MapEventPageDefinition> _pageModels = new();
    private readonly List<MapEventConditionDefinition> _conditionModels = new();
    private readonly List<MapEventCommandDefinition> _commandModels = new();
    private int _selectedPageIndex = -1;
    private int _selectedConditionIndex = -1;
    private int _selectedCommandIndex = -1;
    private bool _binding;
    private bool _ignoreListEvents;

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

        EditorListDraw.UseReadableSelection(_pages);
        EditorListDraw.UseReadableSelection(_conditions);
        EditorListDraw.UseReadableSelection(_commands);
        EditorListDraw.UseReadableChoices(_trigger, MapEventEditorLabels.Trigger);
        EditorListDraw.UseReadableChoices(_movement, MapEventEditorLabels.Movement);
        _activePageCaption.Text = MapEventEditorLabels.ActivePageCaption(-1, 0, null);

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
            root.Controls.Add(new Label
            {
                Text = label,
                AutoSize = true,
                Anchor = AnchorStyles.Left,
                Margin = new Padding(0, 6, 8, 0),
            }, 0, row);
            root.Controls.Add(control, 1, row);
        }

        void Span(Control control)
        {
            var row = root.RowCount++;
            root.Controls.Add(control, 0, row);
            root.SetColumnSpan(control, 2);
        }

        Label Section(string title) => new()
        {
            Text = title,
            AutoSize = true,
            Font = EditorChrome.SectionFont,
            ForeColor = EditorChrome.RibbonAccentDim,
            Margin = new Padding(0, 12, 0, 4),
        };

        Span(_activePageCaption);
        Span(Section("Pages"));
        var pageButtons = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(8, 0, 0, 0),
        };
        var btnAddPage = new Button { Text = "Ajouter une page", AutoSize = true };
        var btnRemovePage = new Button { Text = "Retirer la page", AutoSize = true };
        btnAddPage.Click += (_, _) => AddPage();
        btnRemovePage.Click += (_, _) => RemovePage();
        pageButtons.Controls.Add(btnAddPage);
        pageButtons.Controls.Add(btnRemovePage);
        var pageRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        pageRow.Controls.Add(_pages);
        pageRow.Controls.Add(pageButtons);
        Span(pageRow);

        Span(Section("Déclenchement"));
        Row("Priorité", _priority);
        Row("Déclencheur", _trigger);
        Span(Section("Déplacement"));
        Row("Mouvement", _movement);

        var wpButtons = new FlowLayoutPanel { AutoSize = true };
        var btnAddWp = new Button { Text = "Ajouter une étape", AutoSize = true };
        var btnRemoveWp = new Button { Text = "Retirer l'étape", AutoSize = true };
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
        Row("Trajet", wpButtons);

        Span(Section("Apparence"));
        var appearance = new FlowLayoutPanel { AutoSize = true };
        appearance.Controls.Add(new Label { Text = "Graphisme", AutoSize = true, Margin = new Padding(0, 6, 4, 0) });
        appearance.Controls.Add(_appearanceGraphic);
        appearance.Controls.Add(new Label { Text = "Direction (0–7)", AutoSize = true, Margin = new Padding(12, 6, 4, 0) });
        appearance.Controls.Add(_appearanceDirection);
        appearance.Controls.Add(_blocksCollision);
        Span(appearance);

        Span(Section("Conditions — toutes doivent être vraies"));
        var condButtons = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        var condListRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        var btnAddCond = new Button { Text = "Ajouter une condition", AutoSize = true };
        var btnRemoveCond = new Button { Text = "Retirer", AutoSize = true };
        btnAddCond.Click += (_, _) => AddCondition();
        btnRemoveCond.Click += (_, _) => RemoveCondition();
        var condButtonCol = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(8, 0, 0, 0),
        };
        condButtonCol.Controls.Add(btnAddCond);
        condButtonCol.Controls.Add(btnRemoveCond);
        condListRow.Controls.Add(_conditions);
        condListRow.Controls.Add(condButtonCol);
        condButtons.Controls.Add(condListRow);
        condButtons.Controls.Add(_conditionParams);
        Span(condButtons);

        Span(Section("Commandes — exécutées dans l'ordre"));
        var cmdButtons = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        var btnAddCmd = new Button { Text = "Ajouter une commande", AutoSize = true };
        var btnRemoveCmd = new Button { Text = "Retirer", AutoSize = true };
        btnAddCmd.Click += (_, _) => AddCommand();
        btnRemoveCmd.Click += (_, _) => RemoveCommand();
        var cmdButtonCol = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(8, 0, 0, 0),
        };
        cmdButtonCol.Controls.Add(btnAddCmd);
        cmdButtonCol.Controls.Add(btnRemoveCmd);
        cmdButtons.Controls.Add(_commands);
        cmdButtons.Controls.Add(cmdButtonCol);
        var cmdBlock = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
        };
        cmdBlock.Controls.Add(cmdButtons);
        cmdBlock.Controls.Add(_commandParams);
        Span(cmdBlock);
        Span(Section("Validation"));
        Span(_validationLabel);

        Controls.Add(root);

        _priority.ValueChanged += (_, _) => OnPageFieldChanged();
        _trigger.SelectedIndexChanged += (_, _) => OnPageFieldChanged();
        _movement.SelectedIndexChanged += (_, _) => OnPageFieldChanged();
        _appearanceGraphic.ValueChanged += (_, _) => OnPageFieldChanged();
        _appearanceDirection.ValueChanged += (_, _) => OnPageFieldChanged();
        _blocksCollision.CheckedChanged += (_, _) => OnPageFieldChanged();
        _pages.SelectedIndexChanged += (_, _) =>
        {
            if (_binding || _ignoreListEvents)
            {
                return;
            }

            SelectPage(_pages.SelectedIndex);
        };
        _conditions.SelectedIndexChanged += (_, _) =>
        {
            if (_binding || _ignoreListEvents)
            {
                return;
            }

            SelectCondition(_conditions.SelectedIndex);
        };
        _commands.SelectedIndexChanged += (_, _) =>
        {
            if (_binding || _ignoreListEvents)
            {
                return;
            }

            SelectCommand(_commands.SelectedIndex);
        };
        _commandParams.ParametersChanged += () => OnCommandFieldChanged();
        _conditionParams.ParametersChanged += () => OnConditionFieldChanged();
        _waypoints.CellValueChanged += (_, _) => OnPageFieldChanged();
        _waypoints.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_waypoints.IsCurrentCellDirty)
            {
                _waypoints.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        };
    }

    public event Action? PagesChanged;

    internal ListBox PagesForTest => _pages;

    internal ComboBox TriggerForTest => _trigger;

    internal NumericUpDown PriorityForTest => _priority;

    internal ComboBox MovementForTest => _movement;

    internal DataGridView WaypointsForTest => _waypoints;

    internal ListBox ConditionsForTest => _conditions;

    internal MapEventConditionParameterPanel ConditionParamsForTest => _conditionParams;

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
                UpdateActivePageCaption();
            }

            _validationLabel.Text = string.Empty;
        }
        finally
        {
            _binding = false;
        }

        NotifyChanged();
    }

    public bool TryBuildPages(out IReadOnlyList<MapEventPageDefinition> pages, out string? error)
    {
        if (!FlushCurrentCommand() || !FlushCurrentCondition() || !FlushCurrentPage())
        {
            pages = Array.Empty<MapEventPageDefinition>();
            error = string.IsNullOrWhiteSpace(_validationLabel.Text)
                ? "Pages invalides."
                : _validationLabel.Text;
            return false;
        }

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

            _conditionModels.Clear();
            foreach (var cond in page.Conditions)
            {
                _conditionModels.Add(new MapEventConditionDefinition
                {
                    Kind = cond.Kind,
                    ParameterJson = cond.ParameterJson,
                });
            }

            RefreshConditionList();
            if (_conditionModels.Count > 0)
            {
                _conditions.SelectedIndex = 0;
                SelectCondition(0);
            }
            else
            {
                _selectedConditionIndex = -1;
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

            UpdateActivePageCaption();
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

    private void SelectCondition(int index)
    {
        FlushCurrentCondition();
        _selectedConditionIndex = index;
        if (index < 0 || index >= _conditionModels.Count)
        {
            return;
        }

        _binding = true;
        try
        {
            _conditionParams.LoadCondition(_conditionModels[index]);
        }
        finally
        {
            _binding = false;
        }
    }

    private void AddCondition()
    {
        FlushCurrentCondition();
        _conditionModels.Add(new MapEventConditionDefinition
        {
            Kind = MapEventConditionKinds.CharacterSwitch,
            ParameterJson = """{"switchId":"gate_open","value":true}""",
        });
        RefreshConditionList();
        _conditions.SelectedIndex = _conditionModels.Count - 1;
        OnPageFieldChanged();
    }

    private void RemoveCondition()
    {
        if (_selectedConditionIndex < 0 || _selectedConditionIndex >= _conditionModels.Count)
        {
            return;
        }

        _conditionModels.RemoveAt(_selectedConditionIndex);
        RefreshConditionList();
        _selectedConditionIndex = Math.Min(_selectedConditionIndex, _conditionModels.Count - 1);
        if (_selectedConditionIndex >= 0)
        {
            _conditions.SelectedIndex = _selectedConditionIndex;
            SelectCondition(_selectedConditionIndex);
        }

        OnPageFieldChanged();
    }

    private void OnConditionFieldChanged()
    {
        if (_binding)
        {
            return;
        }

        if (FlushCurrentCondition() && FlushCurrentPage())
        {
            RefreshPageList();
        }

        NotifyChanged();
    }

    private bool FlushCurrentCondition()
    {
        if (_binding || _selectedConditionIndex < 0 || _selectedConditionIndex >= _conditionModels.Count)
        {
            return true;
        }

        if (!_conditionParams.TryBuildCondition(out var cond, out var err))
        {
            _validationLabel.Text = err ?? "Condition invalide.";
            return false;
        }

        _conditionModels[_selectedConditionIndex] = cond;
        WithIgnoredListEvents(() =>
        {
            RefreshConditionList();
            if (_selectedConditionIndex >= 0 && _selectedConditionIndex < _conditions.Items.Count)
            {
                _conditions.SelectedIndex = _selectedConditionIndex;
            }
        });

        return true;
    }

    private void RefreshConditionList()
    {
        _conditions.Items.Clear();
        for (var i = 0; i < _conditionModels.Count; i++)
        {
            var cond = _conditionModels[i];
            _conditions.Items.Add(MapEventEditorLabels.ConditionListLine(i, cond.Kind, cond.ParameterJson));
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
        if (_binding)
        {
            return;
        }

        FlushCurrentCommand();
        FlushCurrentCondition();
        if (FlushCurrentPage())
        {
            RefreshPageList();
        }
        else
        {
            UpdateActivePageCaption();
        }

        NotifyChanged();
    }

    private void OnCommandFieldChanged()
    {
        if (_binding)
        {
            return;
        }

        if (FlushCurrentCommand() && FlushCurrentPage())
        {
            RefreshPageList();
        }

        NotifyChanged();
    }

    private bool FlushCurrentCommand()
    {
        if (_binding || _selectedCommandIndex < 0 || _selectedCommandIndex >= _commandModels.Count)
        {
            return true;
        }

        if (!_commandParams.TryBuildCommand(out var cmd, out var err))
        {
            _validationLabel.Text = err ?? "Commande invalide.";
            return false;
        }

        _commandModels[_selectedCommandIndex] = cmd;
        WithIgnoredListEvents(() =>
        {
            RefreshCommandList();
            if (_selectedCommandIndex >= 0 && _selectedCommandIndex < _commands.Items.Count)
            {
                _commands.SelectedIndex = _selectedCommandIndex;
            }
        });

        return true;
    }

    private bool FlushCurrentPage()
    {
        if (_binding || _selectedPageIndex < 0 || _selectedPageIndex >= _pageModels.Count)
        {
            return true;
        }

        var conditions = _conditionModels.Select(c => new MapEventConditionDefinition
        {
            Kind = c.Kind,
            ParameterJson = c.ParameterJson,
        }).ToList();

        var waypoints = new List<MapEventRouteWaypoint>();
        var waypointNumber = 0;
        foreach (DataGridViewRow row in _waypoints.Rows)
        {
            if (row.IsNewRow)
            {
                continue;
            }

            waypointNumber++;
            if (!TryParseWaypointRow(row, waypointNumber, out var waypoint, out var waypointError))
            {
                _validationLabel.Text = waypointError;
                return false;
            }

            waypoints.Add(waypoint);
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
        return true;
    }

    private void ClearPageUi()
    {
        _waypoints.Rows.Clear();
        _conditionModels.Clear();
        _conditions.Items.Clear();
        _commandModels.Clear();
        _commands.Items.Clear();
    }

    private void RefreshPageList()
    {
        var selected = _selectedPageIndex >= 0 ? _selectedPageIndex : _pages.SelectedIndex;
        WithIgnoredListEvents(() =>
        {
            _pages.Items.Clear();
            for (var i = 0; i < _pageModels.Count; i++)
            {
                _pages.Items.Add(MapEventEditorLabels.PageListLine(i, _pageModels[i]));
            }

            if (selected >= 0 && selected < _pages.Items.Count)
            {
                _pages.SelectedIndex = selected;
            }
        });
        UpdateActivePageCaption();
    }

    private void UpdateActivePageCaption()
    {
        var page = _selectedPageIndex >= 0 && _selectedPageIndex < _pageModels.Count
            ? _pageModels[_selectedPageIndex]
            : null;
        _activePageCaption.Text = MapEventEditorLabels.ActivePageCaption(_selectedPageIndex, _pageModels.Count, page);
    }

    private void RefreshCommandList()
    {
        _commands.Items.Clear();
        for (var i = 0; i < _commandModels.Count; i++)
        {
            var cmd = _commandModels[i];
            _commands.Items.Add(MapEventEditorLabels.CommandListLine(i, cmd.Discriminator, cmd.ParameterJson));
        }
    }

    private void WithIgnoredListEvents(Action action)
    {
        var previous = _ignoreListEvents;
        _ignoreListEvents = true;
        try
        {
            action();
        }
        finally
        {
            _ignoreListEvents = previous;
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
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "X", Width = 60 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Y", Width = 60 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Attente (ms)", Width = 100 });
        grid.DataError += (_, e) => e.ThrowException = false;
        return grid;
    }

    private static bool TryParseWaypointRow(
        DataGridViewRow row,
        int waypointNumber,
        out MapEventRouteWaypoint waypoint,
        out string error)
    {
        waypoint = new MapEventRouteWaypoint();
        if (!TryParseWaypointInt(row.Cells[0].Value, min: 0, max: null, out var tileX, out var xError))
        {
            error = $"Étape {waypointNumber} : X invalide ({xError}).";
            return false;
        }

        if (!TryParseWaypointInt(row.Cells[1].Value, min: 0, max: null, out var tileY, out var yError))
        {
            error = $"Étape {waypointNumber} : Y invalide ({yError}).";
            return false;
        }

        if (!TryParseWaypointInt(
                row.Cells[2].Value,
                min: 0,
                max: MapEventRuntimeLimits.MaxWaitMs,
                out var waitMs,
                out var waitError))
        {
            error = $"Étape {waypointNumber} : attente invalide ({waitError}).";
            return false;
        }

        waypoint = new MapEventRouteWaypoint { TileX = tileX, TileY = tileY, WaitMs = waitMs };
        error = string.Empty;
        return true;
    }

    private static bool TryParseWaypointInt(object? value, int min, int? max, out int parsed, out string error)
    {
        parsed = 0;
        var raw = Convert.ToString(value)?.Trim();
        if (string.IsNullOrEmpty(raw) || !int.TryParse(raw, out parsed))
        {
            error = "entier requis";
            return false;
        }

        if (parsed < min || (max is int cap && parsed > cap))
        {
            error = max is int bound ? $"hors bornes {min}–{bound}" : $"doit être ≥ {min}";
            return false;
        }

        error = string.Empty;
        return true;
    }
}
