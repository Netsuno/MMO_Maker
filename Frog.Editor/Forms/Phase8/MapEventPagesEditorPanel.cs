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
        MaximumSize = new Size(860, 0),
        Font = EditorChrome.CaptionFont,
        ForeColor = Color.White,
        BackColor = Color.FromArgb(26, 61, 88),
        Padding = new Padding(12, 8, 12, 8),
        Margin = new Padding(0, 0, 0, 0),
    };
    private readonly Label _conditionSummary = new()
    {
        AutoSize = true,
        MaximumSize = new Size(860, 0),
        Font = EditorChrome.BodyFont,
        ForeColor = Color.FromArgb(24, 36, 48),
        BackColor = Color.FromArgb(226, 236, 246),
        Padding = new Padding(12, 6, 12, 6),
        Margin = new Padding(0, 0, 0, 8),
    };
    private readonly Button _btnPrevPage = new() { Text = "Page précédente", AutoSize = true };
    private readonly Button _btnNextPage = new() { Text = "Page suivante", AutoSize = true };
    private readonly Button _btnRemovePage = new() { Text = "Retirer la page", AutoSize = true };
    private readonly NumericUpDown _priority = new() { Width = 80, Minimum = 0, Maximum = 9999 };
    private readonly ComboBox _trigger = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 240, Font = EditorChrome.BodyFont };
    private readonly ComboBox _movement = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 240, Font = EditorChrome.BodyFont };
    private readonly ComboBox _addStepKind = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 160, Font = EditorChrome.BodyFont };
    private readonly CheckBox _routeRepeat = new() { Text = "Répéter le trajet", Checked = true, AutoSize = true };
    private readonly CheckBox _routeSkipIfBlocked = new() { Text = "Ignorer si bloqué", AutoSize = true };
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
    private readonly Label _validationLabel = new()
    {
        AutoSize = true,
        ForeColor = Color.Firebrick,
        MaximumSize = new Size(860, 0),
        Margin = new Padding(0, 0, 0, 8),
    };

    private readonly List<MapEventPageDefinition> _pageModels = new();
    private readonly List<MapEventConditionDefinition> _conditionModels = new();
    private readonly List<MapEventCommandDefinition> _commandModels = new();
    private int _selectedPageIndex = -1;
    private int _selectedConditionIndex = -1;
    private int _selectedCommandIndex = -1;
    private int _bindDepth;
    private bool _ignoreListEvents;

    private bool IsBinding => _bindDepth > 0;

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

        foreach (var step in MapEventRouteStepKinds.All)
        {
            _addStepKind.Items.Add(step);
        }

        if (_addStepKind.Items.Count > 0)
        {
            _addStepKind.SelectedIndex = 0;
        }

        EditorListDraw.UseReadableSelection(_pages);
        EditorListDraw.UseReadableSelection(_conditions);
        EditorListDraw.UseReadableSelection(_commands);
        EditorListDraw.UseReadableChoices(_trigger, MapEventEditorLabels.Trigger);
        EditorListDraw.UseReadableChoices(_movement, MapEventEditorLabels.Movement);
        EditorListDraw.UseReadableChoices(_addStepKind, MapEventEditorLabels.RouteStep);
        _activePageCaption.Text = MapEventEditorLabels.ActivePageCaption(-1, 0, null);
        _conditionSummary.Text = MapEventEditorLabels.ActivePageConditionSummary(null);
        _btnPrevPage.Enabled = false;
        _btnNextPage.Enabled = false;
        _btnPrevPage.Click += (_, _) => ShiftPage(-1);
        _btnNextPage.Click += (_, _) => ShiftPage(1);

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
        Span(_conditionSummary);
        Span(_validationLabel);
        Span(Section("Pages"));
        var pageButtons = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
            Margin = new Padding(8, 0, 0, 0),
        };
        var btnAddPage = new Button { Text = "Ajouter une page", AutoSize = true };
        btnAddPage.Click += (_, _) => AddPage();
        _btnRemovePage.Click += (_, _) => RemovePage();
        pageButtons.Controls.Add(_btnPrevPage);
        pageButtons.Controls.Add(_btnNextPage);
        pageButtons.Controls.Add(btnAddPage);
        pageButtons.Controls.Add(_btnRemovePage);
        var pageRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        pageRow.Controls.Add(_pages);
        pageRow.Controls.Add(pageButtons);
        Span(pageRow);

        Span(Section("Déclenchement"));
        Row("Priorité", _priority);
        Row("Déclencheur", _trigger);
        Span(Section("Déplacement"));
        Row("Mouvement", _movement);

        var btnAddWp = new Button { Text = "Ajouter une étape", AutoSize = true };
        var btnRemoveWp = new Button { Text = "Retirer l'étape", AutoSize = true };
        btnAddWp.Click += (_, _) =>
        {
            var kind = _addStepKind.SelectedItem as string ?? MapEventRouteStepKinds.Move;
            var wait = kind == MapEventRouteStepKinds.Wait ? 500 : 250;
            _waypoints.Rows.Add(0, 0, wait, MapEventEditorLabels.RouteStep(kind));
            OnPageFieldChanged();
        };
        btnRemoveWp.Click += (_, _) =>
        {
            if (_waypoints.CurrentRow is { IsNewRow: false } row)
            {
                _waypoints.Rows.Remove(row);
                OnPageFieldChanged();
            }
        };
        var addRow = new FlowLayoutPanel { AutoSize = true, WrapContents = false };
        addRow.Controls.Add(_addStepKind);
        addRow.Controls.Add(btnAddWp);
        addRow.Controls.Add(btnRemoveWp);
        var routeHint = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(520, 0),
            ForeColor = Color.DimGray,
            Text = "Le premier jalon est le départ. Les pas suivants se jouent dans l’ordre : tuile, attente, ou un pas d’une case.",
        };
        var routePanel = new FlowLayoutPanel
        {
            AutoSize = true,
            FlowDirection = FlowDirection.TopDown,
            WrapContents = false,
        };
        routePanel.Controls.Add(_waypoints);
        routePanel.Controls.Add(addRow);
        routePanel.Controls.Add(_routeRepeat);
        routePanel.Controls.Add(_routeSkipIfBlocked);
        routePanel.Controls.Add(routeHint);
        Row("Trajet", routePanel);

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
        var palette = new MapEventCommandPaletteBar();
        palette.EntryChosen += InsertPaletteCommand;
        Span(palette);
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

        Controls.Add(root);

        _priority.ValueChanged += (_, _) => OnPageFieldChanged();
        _trigger.SelectedIndexChanged += (_, _) => OnPageFieldChanged();
        _movement.SelectedIndexChanged += (_, _) => OnPageFieldChanged();
        _routeRepeat.CheckedChanged += (_, _) => OnPageFieldChanged();
        _routeSkipIfBlocked.CheckedChanged += (_, _) => OnPageFieldChanged();
        _appearanceGraphic.ValueChanged += (_, _) => OnPageFieldChanged();
        _appearanceDirection.ValueChanged += (_, _) => OnPageFieldChanged();
        _blocksCollision.CheckedChanged += (_, _) => OnPageFieldChanged();
        _pages.SelectedIndexChanged += (_, _) =>
        {
            if (IsBinding || _ignoreListEvents)
            {
                return;
            }

            SelectPage(_pages.SelectedIndex);
        };
        _conditions.SelectedIndexChanged += (_, _) =>
        {
            if (IsBinding || _ignoreListEvents)
            {
                return;
            }

            SelectCondition(_conditions.SelectedIndex);
        };
        _commands.SelectedIndexChanged += (_, _) =>
        {
            if (IsBinding || _ignoreListEvents)
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

    internal ComboBox AddStepKindForTest => _addStepKind;

    internal CheckBox RouteRepeatForTest => _routeRepeat;

    internal CheckBox RouteSkipIfBlockedForTest => _routeSkipIfBlocked;

    internal DataGridView WaypointsForTest => _waypoints;

    internal ListBox ConditionsForTest => _conditions;

    internal MapEventConditionParameterPanel ConditionParamsForTest => _conditionParams;

    internal ListBox CommandsForTest => _commands;

    internal MapEventCommandParameterPanel CommandParamsForTest => _commandParams;

    internal Label ValidationLabelForTest => _validationLabel;

    internal Label ActivePageCaptionForTest => _activePageCaption;

    internal Label ConditionSummaryForTest => _conditionSummary;

    internal Button PreviousPageButtonForTest => _btnPrevPage;

    internal Button NextPageButtonForTest => _btnNextPage;

    internal Button RemovePageButtonForTest => _btnRemovePage;

    public void LoadPages(IReadOnlyList<MapEventPageDefinition> pages)
    {
        PushBinding();
        try
        {
            _pageModels.Clear();
            _pageModels.AddRange(pages.Select(ClonePage));
            _selectedPageIndex = -1;
            _selectedConditionIndex = -1;
            _selectedCommandIndex = -1;
            if (_pageModels.Count > 0)
            {
                ShowPage(0);
            }
            else
            {
                RefreshPageList();
                ClearPageUi();
                UpdateActivePageCaption();
            }

            _validationLabel.Text = string.Empty;
        }
        finally
        {
            PopBinding();
        }

        NotifyChanged();
    }

    public bool TryBuildPages(out IReadOnlyList<MapEventPageDefinition> pages, out string? error)
    {
        if (!TryCommitActivePage())
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
                if (i != _selectedPageIndex)
                {
                    ShowPage(i);
                }
                else
                {
                    UpdateActivePageCaption();
                }

                return false;
            }
        }

        error = null;
        _validationLabel.Text = string.Empty;
        return true;
    }

    private void AddPage()
    {
        if (!TryCommitActivePage())
        {
            return;
        }

        _pageModels.Add(new MapEventPageDefinition
        {
            PageOrder = _pageModels.Count,
            TriggerKind = Phase8MapEventTriggerKinds.Action,
            Commands = Array.Empty<MapEventCommandDefinition>(),
        });
        ShowPage(_pageModels.Count - 1);
        NotifyChanged();
    }

    private void RemovePage()
    {
        if (_selectedPageIndex < 0 || _selectedPageIndex >= _pageModels.Count)
        {
            return;
        }

        var removed = _selectedPageIndex;
        _pageModels.RemoveAt(removed);
        for (var i = 0; i < _pageModels.Count; i++)
        {
            _pageModels[i].PageOrder = i;
        }

        if (_pageModels.Count == 0)
        {
            _selectedPageIndex = -1;
            _selectedConditionIndex = -1;
            _selectedCommandIndex = -1;
            PushBinding();
            try
            {
                RefreshPageList();
                ClearPageUi();
                UpdateActivePageCaption();
            }
            finally
            {
                PopBinding();
            }

            NotifyChanged();
            return;
        }

        ShowPage(Math.Min(removed, _pageModels.Count - 1));
        NotifyChanged();
    }

    private void ShiftPage(int delta)
    {
        if (_selectedPageIndex < 0)
        {
            return;
        }

        var target = _selectedPageIndex + delta;
        if (target < 0 || target >= _pageModels.Count)
        {
            return;
        }

        _pages.SelectedIndex = target;
    }

    private void SelectPage(int index)
    {
        if (IsBinding || index == _selectedPageIndex)
        {
            return;
        }

        if (!TryCommitActivePage())
        {
            RestorePageListSelection();
            UpdateActivePageCaption();
            return;
        }

        if (index < 0 || index >= _pageModels.Count)
        {
            _selectedPageIndex = -1;
            PushBinding();
            try
            {
                ClearPageUi();
                UpdateActivePageCaption();
            }
            finally
            {
                PopBinding();
            }

            return;
        }

        ShowPage(index);
    }

    private void ShowPage(int index)
    {
        _selectedPageIndex = index;
        RefreshPageList();
        PushBinding();
        try
        {
            BindActivePage();
        }
        finally
        {
            PopBinding();
        }
    }

    private void BindActivePage()
    {
        if (_selectedPageIndex < 0 || _selectedPageIndex >= _pageModels.Count)
        {
            ClearPageUi();
            UpdateActivePageCaption();
            return;
        }

        var page = _pageModels[_selectedPageIndex];
        _priority.Value = Math.Clamp(page.Priority, (int)_priority.Minimum, (int)_priority.Maximum);
        var triggerIndex = _trigger.Items.IndexOf(page.TriggerKind);
        _trigger.SelectedIndex = triggerIndex >= 0 ? triggerIndex : 0;
        var moveIndex = _movement.Items.IndexOf(page.MovementKind);
        _movement.SelectedIndex = moveIndex >= 0 ? moveIndex : 0;
        _routeRepeat.Checked = page.RouteRepeat != false;
        _routeSkipIfBlocked.Checked = page.RouteSkipIfBlocked;
        _appearanceGraphic.Value = page.AppearanceGraphicId;
        _appearanceDirection.Value = page.AppearanceDirection;
        _blocksCollision.Checked = page.BlocksCollision;

        _waypoints.Rows.Clear();
        foreach (var wp in page.RouteWaypoints)
        {
            _waypoints.Rows.Add(wp.TileX, wp.TileY, wp.WaitMs, MapEventEditorLabels.RouteStep(wp.StepKind));
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

        _selectedConditionIndex = -1;
        RefreshConditionList();
        if (_conditionModels.Count > 0)
        {
            _conditions.SelectedIndex = 0;
            SelectCondition(0);
        }
        else
        {
            _conditionParams.Visible = false;
        }

        _commandModels.Clear();
        _commandModels.AddRange(page.Commands.Select(c => new MapEventCommandDefinition
        {
            Discriminator = c.Discriminator,
            SchemaVersion = c.SchemaVersion,
            ParameterJson = c.ParameterJson,
        }));
        _selectedCommandIndex = -1;
        RefreshCommandList();
        if (_commandModels.Count > 0)
        {
            _commands.SelectedIndex = 0;
            SelectCommand(0);
        }
        else
        {
            _commandParams.Visible = false;
        }

        UpdateActivePageCaption();
    }

    private void SelectCommand(int index)
    {
        if (!IsBinding && index != _selectedCommandIndex && !FlushCurrentCommand())
        {
            RestoreCommandSelection();
            return;
        }

        _selectedCommandIndex = index;
        if (index < 0 || index >= _commandModels.Count)
        {
            _commandParams.Visible = false;
            return;
        }

        _commandParams.Visible = true;
        PushBinding();
        try
        {
            _commandParams.LoadCommand(_commandModels[index]);
        }
        finally
        {
            PopBinding();
        }
    }

    private void SelectCondition(int index)
    {
        if (!IsBinding && index != _selectedConditionIndex && !FlushCurrentCondition())
        {
            RestoreConditionSelection();
            return;
        }

        _selectedConditionIndex = index;
        if (index < 0 || index >= _conditionModels.Count)
        {
            _conditionParams.Visible = false;
            return;
        }

        _conditionParams.Visible = true;
        PushBinding();
        try
        {
            _conditionParams.LoadCondition(_conditionModels[index]);
        }
        finally
        {
            PopBinding();
        }
    }

    private void AddCondition()
    {
        if (!FlushCurrentCondition())
        {
            return;
        }

        _conditionModels.Add(new MapEventConditionDefinition
        {
            Kind = MapEventConditionKinds.CharacterSwitch,
            ParameterJson = """{"switchId":"gate_open","value":true}""",
        });
        RefreshConditionList();
        _conditions.SelectedIndex = _conditionModels.Count - 1;
        CommitActivePageAndNotify();
    }

    private void RemoveCondition()
    {
        if (_selectedConditionIndex < 0 || _selectedConditionIndex >= _conditionModels.Count)
        {
            return;
        }

        var next = _selectedConditionIndex;
        _conditionModels.RemoveAt(_selectedConditionIndex);
        _selectedConditionIndex = -1;
        WithIgnoredListEvents(RefreshConditionList);
        if (_conditionModels.Count == 0)
        {
            _conditionParams.Visible = false;
        }
        else
        {
            _conditions.SelectedIndex = Math.Min(next, _conditionModels.Count - 1);
        }

        CommitActivePageAndNotify();
    }

    private void OnConditionFieldChanged() => CommitActivePageAndNotify();

    private bool FlushCurrentCondition()
    {
        if (IsBinding || _selectedConditionIndex < 0 || _selectedConditionIndex >= _conditionModels.Count)
        {
            return true;
        }

        if (!_conditionParams.TryBuildCondition(out var cond, out var err))
        {
            ShowPageError(err ?? "Condition invalide.");
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

    internal void InsertPaletteForTest(string id) => InsertPaletteCommand(id);

    private void InsertPaletteCommand(string id)
    {
        if (!MapEventCommandPalette.TryCreate(id, out var command))
        {
            return;
        }

        if (!FlushCurrentCommand())
        {
            return;
        }

        _commandModels.Add(command);
        RefreshCommandList();
        _commands.SelectedIndex = _commandModels.Count - 1;
        CommitActivePageAndNotify();
    }

    private void AddCommand()
    {
        if (!FlushCurrentCommand())
        {
            return;
        }

        _commandModels.Add(new MapEventCommandDefinition
        {
            Discriminator = MapEventCommandDiscriminators.ShowText,
            SchemaVersion = 1,
            ParameterJson = "{\"text\":\"…\"}",
        });
        RefreshCommandList();
        _commands.SelectedIndex = _commandModels.Count - 1;
        CommitActivePageAndNotify();
    }

    private void RemoveCommand()
    {
        if (_selectedCommandIndex < 0 || _selectedCommandIndex >= _commandModels.Count)
        {
            return;
        }

        var next = _selectedCommandIndex;
        _commandModels.RemoveAt(_selectedCommandIndex);
        _selectedCommandIndex = -1;
        WithIgnoredListEvents(RefreshCommandList);
        if (_commandModels.Count == 0)
        {
            _commandParams.Visible = false;
        }
        else
        {
            _commands.SelectedIndex = Math.Min(next, _commandModels.Count - 1);
        }

        CommitActivePageAndNotify();
    }

    private void OnPageFieldChanged() => CommitActivePageAndNotify();

    private void OnCommandFieldChanged() => CommitActivePageAndNotify();

    private void CommitActivePageAndNotify()
    {
        if (IsBinding)
        {
            return;
        }

        if (TryCommitActivePage())
        {
            RefreshPageList();
        }
        else
        {
            UpdateActivePageCaption();
        }

        NotifyChanged();
    }

    private bool TryCommitActivePage()
    {
        if (IsBinding)
        {
            return true;
        }

        if (!FlushCurrentCommand() || !FlushCurrentCondition() || !FlushCurrentPage())
        {
            return false;
        }

        _validationLabel.Text = string.Empty;
        return true;
    }

    private bool FlushCurrentCommand()
    {
        if (IsBinding || _selectedCommandIndex < 0 || _selectedCommandIndex >= _commandModels.Count)
        {
            return true;
        }

        if (!_commandParams.TryBuildCommand(out var cmd, out var err))
        {
            ShowPageError(err ?? "Commande invalide.");
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
        if (IsBinding || _selectedPageIndex < 0 || _selectedPageIndex >= _pageModels.Count)
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
                ShowPageError(waypointError);
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
            RouteRepeat = _routeRepeat.Checked,
            RouteSkipIfBlocked = _routeSkipIfBlocked.Checked,
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
        _priority.Value = _priority.Minimum;
        if (_trigger.Items.Count > 0)
        {
            _trigger.SelectedIndex = 0;
        }

        if (_movement.Items.Count > 0)
        {
            _movement.SelectedIndex = 0;
        }

        _routeRepeat.Checked = true;
        _routeSkipIfBlocked.Checked = false;
        _appearanceGraphic.Value = 0;
        _appearanceDirection.Value = 0;
        _blocksCollision.Checked = true;
        _waypoints.Rows.Clear();
        _conditionModels.Clear();
        _conditions.Items.Clear();
        _selectedConditionIndex = -1;
        _conditionParams.Visible = false;
        _commandModels.Clear();
        _commands.Items.Clear();
        _selectedCommandIndex = -1;
        _commandParams.Visible = false;
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
        MapEventPageDefinition? page = null;
        if (_selectedPageIndex >= 0 && _selectedPageIndex < _pageModels.Count)
        {
            var stored = _pageModels[_selectedPageIndex];
            page = new MapEventPageDefinition
            {
                TriggerKind = _trigger.SelectedItem as string ?? stored.TriggerKind,
                Priority = (int)_priority.Value,
                MovementKind = _movement.SelectedItem as string ?? stored.MovementKind,
                RouteRepeat = _routeRepeat.Checked,
                Conditions = _conditionModels.ToList(),
                Commands = _commandModels.ToList(),
            };
        }

        _activePageCaption.Text = MapEventEditorLabels.ActivePageCaption(_selectedPageIndex, _pageModels.Count, page);
        _conditionSummary.Text = MapEventEditorLabels.ActivePageConditionSummary(page);
        _btnPrevPage.Enabled = _selectedPageIndex > 0;
        _btnNextPage.Enabled = _selectedPageIndex >= 0 && _selectedPageIndex < _pageModels.Count - 1;
    }

    private void ShowPageError(string? error)
    {
        var text = string.IsNullOrWhiteSpace(error) ? "Page invalide." : error.Trim();
        if (text.StartsWith("Page ", StringComparison.Ordinal))
        {
            _validationLabel.Text = text;
            return;
        }

        var pageNo = _selectedPageIndex >= 0 ? _selectedPageIndex + 1 : 0;
        _validationLabel.Text = pageNo > 0 ? $"Page {pageNo} : {text}" : text;
    }

    private void RestorePageListSelection()
    {
        WithIgnoredListEvents(() =>
        {
            if (_selectedPageIndex >= 0 && _selectedPageIndex < _pages.Items.Count)
            {
                _pages.SelectedIndex = _selectedPageIndex;
            }
        });
    }

    private void RestoreCommandSelection()
    {
        WithIgnoredListEvents(() =>
        {
            if (_selectedCommandIndex >= 0 && _selectedCommandIndex < _commands.Items.Count)
            {
                _commands.SelectedIndex = _selectedCommandIndex;
            }
        });
    }

    private void RestoreConditionSelection()
    {
        WithIgnoredListEvents(() =>
        {
            if (_selectedConditionIndex >= 0 && _selectedConditionIndex < _conditions.Items.Count)
            {
                _conditions.SelectedIndex = _selectedConditionIndex;
            }
        });
    }

    private void PushBinding() => _bindDepth++;

    private void PopBinding() => _bindDepth = Math.Max(0, _bindDepth - 1);

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
        if (!IsBinding)
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
            RouteWaypoints = page.RouteWaypoints.Select(w => w.Copy()).ToList(),
            RouteRepeat = page.RouteRepeat,
            RouteSkipIfBlocked = page.RouteSkipIfBlocked,
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
            Width = 460,
            Height = 110,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            RowHeadersVisible = false,
        };
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "X", Width = 60 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Y", Width = 60 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Attente (ms)", Width = 100 });
        var type = new DataGridViewComboBoxColumn
        {
            HeaderText = "Type",
            Width = 120,
            FlatStyle = FlatStyle.Flat,
        };
        foreach (var kind in MapEventRouteStepKinds.All)
        {
            type.Items.Add(MapEventEditorLabels.RouteStep(kind));
        }

        grid.Columns.Add(type);
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
        if (!MapEventEditorLabels.TryParseRouteStep(Convert.ToString(row.Cells[3].Value), out var stepKind))
        {
            error = $"Étape {waypointNumber} : type de pas inconnu.";
            return false;
        }

        var absolute = MapEventRouteStepKinds.UsesAbsoluteTile(stepKind);
        if (!TryParseWaypointInt(row.Cells[0].Value, min: 0, max: null, out var tileX, out var xError, allowEmpty: !absolute))
        {
            error = $"Étape {waypointNumber} : X invalide ({xError}).";
            return false;
        }

        if (!TryParseWaypointInt(row.Cells[1].Value, min: 0, max: null, out var tileY, out var yError, allowEmpty: !absolute))
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

        waypoint = new MapEventRouteWaypoint
        {
            TileX = tileX,
            TileY = tileY,
            WaitMs = waitMs,
            StepKind = stepKind == MapEventRouteStepKinds.Move ? null : stepKind,
        };
        error = string.Empty;
        return true;
    }

    private static bool TryParseWaypointInt(
        object? value,
        int min,
        int? max,
        out int parsed,
        out string error,
        bool allowEmpty = false)
    {
        parsed = 0;
        var raw = Convert.ToString(value)?.Trim();
        if (string.IsNullOrEmpty(raw))
        {
            if (allowEmpty)
            {
                error = string.Empty;
                return true;
            }

            error = "entier requis";
            return false;
        }

        if (!int.TryParse(raw, out parsed))
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
