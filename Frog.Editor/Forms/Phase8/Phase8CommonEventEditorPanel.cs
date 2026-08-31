using Frog.Application.Content;
using Frog.Core.Events;
using Frog.Core.Models;
using Frog.Editor.Services;

namespace Frog.Editor.Forms.Phase8;

/// <summary>Éditeur structuré d'événement commun (pages : trigger + commandes).</summary>
internal sealed class Phase8CommonEventEditorPanel : Phase8EditorPanelBase
{
    private readonly TextBox _name = new() { Width = 320 };
    private readonly ListBox _pages = new() { Width = 160, Height = 140 };
    private readonly ComboBox _trigger = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
    private readonly DataGridView _commands = CreateGrid();

    private readonly List<MapEventPageDefinition> _pageModels = new();
    private int _selectedPageIndex = -1;

    public Phase8CommonEventEditorPanel()
    {
        foreach (var kind in Phase8MapEventTriggerKinds.All.OrderBy(k => k, StringComparer.Ordinal))
        {
            _trigger.Items.Add(kind);
        }

        if (_trigger.Items.Count > 0)
        {
            _trigger.SelectedIndex = 0;
        }

        _commands.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Discriminator", Width = 160 });
        _commands.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "SchemaVersion", Width = 90 });
        _commands.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "ParameterJson", Width = 360 });

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

        Row("Nom", _name);

        var pagePanel = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false };
        var pageButtons = new FlowLayoutPanel { AutoSize = true };
        var btnAddPage = new Button { Text = "Ajouter page", AutoSize = true };
        var btnRemovePage = new Button { Text = "Retirer page", AutoSize = true };
        btnAddPage.Click += (_, _) => AddPage();
        btnRemovePage.Click += (_, _) => RemovePage();
        pageButtons.Controls.Add(btnAddPage);
        pageButtons.Controls.Add(btnRemovePage);
        pagePanel.Controls.Add(_pages);
        pagePanel.Controls.Add(pageButtons);
        pagePanel.Controls.Add(new Label { Text = "TriggerKind", AutoSize = true, Margin = new Padding(0, 8, 0, 0) });
        pagePanel.Controls.Add(_trigger);
        pagePanel.Controls.Add(new Label { Text = "Commandes", AutoSize = true, Margin = new Padding(0, 8, 0, 0) });
        pagePanel.Controls.Add(_commands);
        var cmdButtons = new FlowLayoutPanel { AutoSize = true };
        var btnAddCmd = new Button { Text = "Ajouter commande", AutoSize = true };
        var btnRemoveCmd = new Button { Text = "Retirer commande", AutoSize = true };
        btnAddCmd.Click += (_, _) => AddCommand();
        btnRemoveCmd.Click += (_, _) => RemoveCommand();
        cmdButtons.Controls.Add(btnAddCmd);
        cmdButtons.Controls.Add(btnRemoveCmd);
        pagePanel.Controls.Add(cmdButtons);
        Row("Pages", pagePanel);

        Controls.Add(form);

        _name.TextChanged += (_, _) => NotifyChanged();
        _trigger.SelectedIndexChanged += (_, _) => OnPageFieldChanged();
        _pages.SelectedIndexChanged += (_, _) => SelectPage(_pages.SelectedIndex);
        _commands.CellValueChanged += (_, _) => OnPageFieldChanged();
        _commands.RowsRemoved += (_, _) => OnPageFieldChanged();
        _commands.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (_commands.IsCurrentCellDirty)
            {
                _commands.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        };
    }

    public override Phase8ContentKind Kind => Phase8ContentKind.CommonEvent;

    public override void LoadPayload(string payloadJson)
    {
        if (!Phase8ContentPostgreSqlService.TryDeserialize(payloadJson, out CommonEventDefinition def, out _))
        {
            def = new CommonEventDefinition();
        }

        ContentId = def.Id;
        Binding = true;
        try
        {
            _name.Text = def.Name;
            _pageModels.Clear();
            _pageModels.AddRange(def.Pages.Select(ClonePage));
            RefreshPageList();
            if (_pageModels.Count > 0)
            {
                _pages.SelectedIndex = 0;
                SelectPage(0);
            }
            else
            {
                _selectedPageIndex = -1;
                _commands.Rows.Clear();
            }
        }
        finally
        {
            Binding = false;
        }
    }

    public override bool TryBuildPayload(out string payloadJson, out string? error)
    {
        FlushCurrentPage();
        var def = new CommonEventDefinition
        {
            Id = ContentId,
            Name = !string.IsNullOrWhiteSpace(CatalogName) ? CatalogName.Trim() : _name.Text.Trim(),
            Pages = _pageModels.Select(ClonePage).ToList(),
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
        LoadPayload(Phase8ContentPostgreSqlService.CreateDefaultPayload(Phase8ContentKind.CommonEvent, newId, "Nouvel événement commun"));
    }

    internal TextBox NameForTest => _name;

    internal ListBox PagesForTest => _pages;

    internal ComboBox TriggerForTest => _trigger;

    internal DataGridView CommandsForTest => _commands;

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
            _commands.Rows.Clear();
        }

        NotifyChanged();
    }

    private void SelectPage(int index)
    {
        FlushCurrentPage();
        _selectedPageIndex = index;
        if (index < 0 || index >= _pageModels.Count)
        {
            return;
        }

        Binding = true;
        try
        {
            var page = _pageModels[index];
            var triggerIndex = _trigger.Items.IndexOf(page.TriggerKind);
            _trigger.SelectedIndex = triggerIndex >= 0 ? triggerIndex : 0;
            _commands.Rows.Clear();
            foreach (var cmd in page.Commands)
            {
                _commands.Rows.Add(cmd.Discriminator, cmd.SchemaVersion.ToString(), cmd.ParameterJson);
            }
        }
        finally
        {
            Binding = false;
        }
    }

    private void OnPageFieldChanged()
    {
        FlushCurrentPage();
        NotifyChanged();
    }

    private void FlushCurrentPage()
    {
        if (Binding || _selectedPageIndex < 0 || _selectedPageIndex >= _pageModels.Count)
        {
            return;
        }

        var commands = new List<MapEventCommandDefinition>();
        foreach (DataGridViewRow row in _commands.Rows)
        {
            if (row.IsNewRow)
            {
                continue;
            }

            commands.Add(new MapEventCommandDefinition
            {
                Discriminator = Convert.ToString(row.Cells[0].Value) ?? string.Empty,
                SchemaVersion = int.TryParse(Convert.ToString(row.Cells[1].Value), out var ver) ? ver : 1,
                ParameterJson = Convert.ToString(row.Cells[2].Value) ?? "{}",
            });
        }

        var existing = _pageModels[_selectedPageIndex];
        _pageModels[_selectedPageIndex] = new MapEventPageDefinition
        {
            PageOrder = existing.PageOrder,
            Priority = existing.Priority,
            TriggerKind = _trigger.SelectedItem as string ?? Phase8MapEventTriggerKinds.Action,
            MovementKind = existing.MovementKind,
            RouteWaypoints = existing.RouteWaypoints,
            AppearanceGraphicId = existing.AppearanceGraphicId,
            AppearanceDirection = existing.AppearanceDirection,
            BlocksCollision = existing.BlocksCollision,
            Conditions = existing.Conditions,
            Commands = commands,
        };
    }

    private void AddCommand()
    {
        _commands.Rows.Add(MapEventCommandDiscriminators.ShowText, "1", "{\"text\":\"…\"}");
        OnPageFieldChanged();
    }

    private void RemoveCommand()
    {
        if (_commands.CurrentRow is { IsNewRow: false } row)
        {
            _commands.Rows.Remove(row);
            OnPageFieldChanged();
        }
    }

    private void RefreshPageList()
    {
        _pages.Items.Clear();
        for (var i = 0; i < _pageModels.Count; i++)
        {
            _pages.Items.Add($"Page {i + 1}");
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
            Conditions = page.Conditions.ToList(),
            Commands = page.Commands.Select(c => new MapEventCommandDefinition
            {
                Discriminator = c.Discriminator,
                SchemaVersion = c.SchemaVersion,
                ParameterJson = c.ParameterJson,
            }).ToList(),
        };

    private static DataGridView CreateGrid() => new()
    {
        Width = 640,
        Height = 160,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AutoGenerateColumns = false,
        RowHeadersVisible = false,
    };
}
