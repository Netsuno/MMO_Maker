using Frog.Application.Content;
using Frog.Core.Models;

namespace Frog.Editor.Forms.GameData;

/// <summary>
/// Fiche Système (base VX) : titre, unité monétaire, noms d’interrupteurs et de
/// variables, groupe de départ. Les clés sont celles des commandes d’événements.
/// </summary>
public sealed class SystemEditorPanel : UserControl
{
    private readonly GameDataPanelLifecycle _lifecycle = new();
    private readonly GameSystemWorkspaceSession _session;
    private readonly IPublishedActorCatalog _actors;
    private readonly ContentRepositoryCapabilities _capabilities;
    private readonly ListBox _list = new() { Dock = DockStyle.Fill };
    private readonly TextBox _search = new() { Dock = DockStyle.Top, PlaceholderText = "Rechercher…" };
    private readonly ComboBox _statusFilter = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _name = new() { Width = 280 };
    private readonly TextBox _title = new() { Width = 280 };
    private readonly TextBox _currency = new() { Width = 160 };
    private readonly TextBox _description = new()
    {
        Width = 360,
        Height = 70,
        Multiline = true,
        ScrollBars = ScrollBars.Vertical,
    };
    private readonly DataGridView _switches = CreateFlagGrid();
    private readonly DataGridView _variables = CreateFlagGrid();
    private readonly DataGridView _party = new()
    {
        Width = 520,
        Height = 110,
        AllowUserToAddRows = false,
        AllowUserToDeleteRows = false,
        AutoGenerateColumns = false,
        RowHeadersVisible = false,
    };
    private readonly DataGridViewComboBoxColumn _actorColumn = new()
    {
        HeaderText = "Héros publié",
        Width = 280,
        FlatStyle = FlatStyle.Flat,
        DisplayStyle = DataGridViewComboBoxDisplayStyle.ComboBox,
    };
    private readonly Button _btnAddSwitch = new() { Text = "Ajouter interrupteur", AutoSize = true };
    private readonly Button _btnRemoveSwitch = new() { Text = "Retirer interrupteur", AutoSize = true };
    private readonly Button _btnAddVariable = new() { Text = "Ajouter variable", AutoSize = true };
    private readonly Button _btnRemoveVariable = new() { Text = "Retirer variable", AutoSize = true };
    private readonly Button _btnAddParty = new() { Text = "Ajouter au groupe", AutoSize = true };
    private readonly Button _btnRemoveParty = new() { Text = "Retirer du groupe", AutoSize = true };
    private readonly Label _meta = new() { AutoSize = true };
    private readonly Label _validation = new() { AutoSize = true, ForeColor = Color.Firebrick };
    private readonly Button _btnNew = new() { Text = "Nouveau", AutoSize = true };
    private readonly Button _btnDup = new() { Text = "Dupliquer", AutoSize = true };
    private readonly Button _btnSave = new() { Text = "Enregistrer brouillon", AutoSize = true };
    private readonly Button _btnPublish = new() { Text = "Publier", AutoSize = true };
    private readonly Button _btnDelete = new() { Text = "Supprimer", AutoSize = true };
    private bool _suppressList;
    private bool _binding;

    public event Action<string>? StatusChanged;

    public bool IsDirty => _session.IsDirty;

    internal long CurrentRevisionForTest => _session.CurrentRevision;

    internal long? PublishedRevisionForTest => _session.PublishedRevision;

    internal ContentPublishStatus CurrentStatusForTest => _session.CurrentStatus;

    internal GameDataPanelLifecycle LifecycleForTest => _lifecycle;

    internal Task<bool> DrainAsync(TimeSpan? timeout = null) => _lifecycle.DrainAsync(timeout ?? TimeSpan.FromSeconds(30));

    internal void BeginClosing() => _lifecycle.BeginClosing();

    internal void DisposeLifecycle() => _lifecycle.Dispose();

    internal Button BtnNewForTest => _btnNew;

    internal Button BtnDupForTest => _btnDup;

    internal Button BtnSaveForTest => _btnSave;

    internal Button BtnPublishForTest => _btnPublish;

    internal Button BtnDeleteForTest => _btnDelete;

    internal Button BtnAddSwitchForTest => _btnAddSwitch;

    internal Button BtnAddVariableForTest => _btnAddVariable;

    internal Button BtnAddPartyForTest => _btnAddParty;

    internal TextBox NameForTest => _name;

    internal TextBox TitleForTest => _title;

    internal TextBox CurrencyForTest => _currency;

    internal TextBox DescriptionForTest => _description;

    internal TextBox SearchForTest => _search;

    internal ComboBox StatusFilterForTest => _statusFilter;

    internal DataGridView SwitchesForTest => _switches;

    internal DataGridView VariablesForTest => _variables;

    internal DataGridView PartyForTest => _party;

    internal int PartyChoiceCountForTest => _actorColumn.Items.Count;

    internal ListBox ListForTest => _list;

    internal Label ValidationForTest => _validation;

    public SystemEditorPanel(
        GameSystemWorkspaceSession session,
        IPublishedActorCatalog actors,
        ContentRepositoryCapabilities capabilities)
    {
        _session = session;
        _actors = actors;
        _capabilities = capabilities;

        _statusFilter.Items.AddRange(new object[] { "Tous", "Brouillon", "Publié" });
        _statusFilter.SelectedIndex = 0;
        _party.Columns.Add(_actorColumn);

        var left = new Panel { Dock = DockStyle.Left, Width = 260, Padding = new Padding(4) };
        left.Controls.Add(_list);
        left.Controls.Add(_search);
        left.Controls.Add(_statusFilter);

        var form = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(12),
            AutoScroll = true,
        };
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        void Row(string label, Control control)
        {
            var row = form.RowCount++;
            form.Controls.Add(
                new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left },
                0,
                row);
            form.Controls.Add(control, 1, row);
        }

        Row("Nom", _name);
        Row("Titre du jeu", _title);
        Row("Unité monétaire", _currency);
        Row("Notes", _description);
        Row("Interrupteurs", FlagHost(_switches, _btnAddSwitch, _btnRemoveSwitch));
        Row("Variables", FlagHost(_variables, _btnAddVariable, _btnRemoveVariable));
        Row("Groupe de départ", FlagHost(_party, _btnAddParty, _btnRemoveParty));
        Row("État", _meta);
        Row("", _validation);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 40,
            FlowDirection = FlowDirection.LeftToRight,
        };
        buttons.Controls.AddRange(new Control[] { _btnNew, _btnDup, _btnSave, _btnPublish, _btnDelete });

        Controls.Add(form);
        Controls.Add(buttons);
        Controls.Add(left);

        _search.TextChanged += (_, _) => _ = _lifecycle.RunAsync(async ct =>
        {
            _session.SearchFilter = _search.Text;
            await RefreshListAsync(ct).ConfigureAwait(true);
        }, "refresh");
        _statusFilter.SelectedIndexChanged += (_, _) => _ = _lifecycle.RunAsync(async ct =>
        {
            _session.StatusFilter = _statusFilter.SelectedIndex switch
            {
                1 => ContentPublishStatus.Draft,
                2 => ContentPublishStatus.Published,
                _ => null,
            };
            await RefreshListAsync(ct).ConfigureAwait(true);
        }, "refresh");
        _list.SelectedIndexChanged += (_, _) => _ = _lifecycle.RunAsync(async _ =>
        {
            if (_suppressList || _list.SelectedItem is not CatalogItem item)
            {
                return;
            }

            if (!GameDataListNavigation.ConfirmDiscardUnsavedChanges(this, "Système", _session.IsDirty))
            {
                GameDataListNavigation.RevertListSelection(
                    _list,
                    ref _suppressList,
                    _session.CurrentId,
                    listItem => ((CatalogItem)listItem).Id);
                return;
            }

            await _session.OpenAsync(item.Id).ConfigureAwait(true);
            BindForm();
        }, "refresh");

        void Mark()
        {
            if (_binding)
            {
                return;
            }

            ApplyFormToSession();
            _session.MarkDirty();
            LiveValidate();
            StatusChanged?.Invoke("Modifié (non enregistré)");
        }

        _name.TextChanged += (_, _) => Mark();
        _title.TextChanged += (_, _) => Mark();
        _currency.TextChanged += (_, _) => Mark();
        _description.TextChanged += (_, _) => Mark();
        AttachGrid(_switches, Mark);
        AttachGrid(_variables, Mark);
        AttachGrid(_party, Mark);

        _btnAddSwitch.Click += (_, _) =>
        {
            _switches.Rows.Add(NextFlagKey(_switches, "interrupteur"), "Nouvel interrupteur");
            Mark();
        };
        _btnRemoveSwitch.Click += (_, _) => RemoveCurrentRow(_switches);
        _btnAddVariable.Click += (_, _) =>
        {
            _variables.Rows.Add(NextFlagKey(_variables, "variable"), "Nouvelle variable");
            Mark();
        };
        _btnRemoveVariable.Click += (_, _) => RemoveCurrentRow(_variables);
        _btnAddParty.Click += (_, _) =>
        {
            var choice = NextPartyChoice();
            if (choice is null)
            {
                return;
            }

            _party.Rows.Add(choice);
            Mark();
        };
        _btnRemoveParty.Click += (_, _) => RemoveCurrentRow(_party);
        _btnNew.Click += (_, _) =>
        {
            _session.AdoptNewDraft(new GameSystemDefinition
            {
                Id = Guid.NewGuid(),
                Name = "Nouveau système",
                Title = "Nouveau jeu",
                CurrencyUnit = GameSystemDefinition.DefaultCurrencyUnit,
            });
            BindForm();
            StatusChanged?.Invoke("Nouveau brouillon");
        };
        _btnDup.Click += (_, _) =>
        {
            if (_session.Current is null)
            {
                return;
            }

            _session.DuplicateCurrent();
            BindForm();
            StatusChanged?.Invoke("Copie créée");
        };
        _btnSave.Click += (_, _) => _ = _lifecycle.TrackAsync(async _ => await SaveAsync(SaveContentIntent.SaveDraft).ConfigureAwait(true), "save");
        _btnPublish.Click += (_, _) => _ = _lifecycle.TrackAsync(async _ => await SaveAsync(SaveContentIntent.Publish).ConfigureAwait(true), "publish");
        _btnDelete.Click += (_, _) => _ = _lifecycle.RunAsync(async _ => await DeleteAsync().ConfigureAwait(true), "delete");

        var canWrite = _capabilities.AllowsSave;
        _btnSave.Enabled = canWrite;
        _btnPublish.Enabled = canWrite;
        _btnDelete.Enabled = canWrite;
        _btnAddSwitch.Enabled = canWrite;
        _btnRemoveSwitch.Enabled = canWrite;
        _btnAddVariable.Enabled = canWrite;
        _btnRemoveVariable.Enabled = canWrite;
        _btnAddParty.Enabled = canWrite;
        _btnRemoveParty.Enabled = canWrite;
    }

    public async Task InitializeAsync()
    {
        await RefreshPublishedActorsAsync().ConfigureAwait(true);
        await RefreshListAsync().ConfigureAwait(true);
        StatusChanged?.Invoke($"Backend système : {_capabilities.DisplayLabel}");
    }

    internal void QueueRefreshLinkedCatalogs()
        => _ = _lifecycle.RunAsync(async ct =>
        {
            await RefreshPublishedActorsAsync().ConfigureAwait(true);
            await RefreshListAsync(ct).ConfigureAwait(true);
        }, "refresh");

    private async Task RefreshPublishedActorsAsync()
    {
        var actors = await _actors.ListPublishedAsync().ConfigureAwait(true);
        _binding = true;
        try
        {
            var currentParty = ReadParty();
            _party.Rows.Clear();
            _actorColumn.Items.Clear();
            foreach (var actor in actors.OrderBy(actor => actor.Name, StringComparer.OrdinalIgnoreCase))
            {
                _actorColumn.Items.Add(new ActorChoice(actor.Id, actor.Name));
            }

            foreach (var actorId in currentParty)
            {
                var choice = _actorColumn.Items
                    .Cast<ActorChoice>()
                    .FirstOrDefault(actor => actor.Id == actorId);
                if (choice is null)
                {
                    choice = new ActorChoice(actorId, $"[introuvable] {actorId:N}");
                    _actorColumn.Items.Add(choice);
                }

                _party.Rows.Add(choice);
            }

            _btnAddParty.Enabled = _capabilities.AllowsSave
                && _actorColumn.Items.Cast<ActorChoice>().Any(actor =>
                    !actor.Label.StartsWith("[introuvable]", StringComparison.Ordinal));
        }
        finally
        {
            _binding = false;
        }
    }

    private async Task RefreshListAsync(CancellationToken ct = default)
    {
        await _session.RefreshCatalogAsync(ct).ConfigureAwait(true);
        if (ct.IsCancellationRequested)
        {
            return;
        }

        _suppressList = true;
        _list.Items.Clear();
        var selected = -1;
        var index = 0;
        foreach (var entry in _session.Catalog)
        {
            _list.Items.Add(new CatalogItem(
                entry.SystemId,
                $"{entry.Name} — {entry.Title} [{entry.Status}]"));
            if (_session.CurrentId == entry.SystemId)
            {
                selected = index;
            }

            index++;
        }

        if (selected >= 0)
        {
            _list.SelectedIndex = selected;
        }

        _suppressList = false;
    }

    private void BindForm()
    {
        var definition = _session.Current;
        if (definition is null)
        {
            return;
        }

        _binding = true;
        try
        {
            _name.Text = definition.Name;
            _title.Text = definition.Title;
            _currency.Text = definition.CurrencyUnit;
            _description.Text = definition.Description ?? string.Empty;
            FillFlagGrid(_switches, definition.Switches);
            FillFlagGrid(_variables, definition.Variables);
            _party.Rows.Clear();
            foreach (var actorId in definition.StartingPartyActorIds)
            {
                var choice = _actorColumn.Items
                    .Cast<ActorChoice>()
                    .FirstOrDefault(actor => actor.Id == actorId);
                if (choice is null)
                {
                    choice = new ActorChoice(actorId, $"[introuvable] {actorId:N}");
                    _actorColumn.Items.Add(choice);
                }

                _party.Rows.Add(choice);
            }

            _meta.Text =
                $"Id={definition.Id:N}  rev={_session.CurrentRevision}  statut={_session.CurrentStatus}  publié={_session.PublishedRevision?.ToString() ?? "—"}";
        }
        finally
        {
            _binding = false;
        }

        LiveValidate();
    }

    private void ApplyFormToSession()
    {
        if (_session.Current is null)
        {
            return;
        }

        _session.Current.Name = _name.Text.Trim();
        _session.Current.Title = _title.Text.Trim();
        _session.Current.CurrencyUnit = _currency.Text.Trim();
        _session.Current.Description = string.IsNullOrWhiteSpace(_description.Text)
            ? null
            : _description.Text.Trim();
        _session.Current.Switches = ReadFlagGrid(_switches);
        _session.Current.Variables = ReadFlagGrid(_variables);
        _session.Current.StartingPartyActorIds = ReadParty();
    }

    private List<Guid> ReadParty()
    {
        var ids = new List<Guid>();
        foreach (DataGridViewRow row in _party.Rows)
        {
            if (row.IsNewRow)
            {
                continue;
            }

            if (row.Cells[0].Value is ActorChoice choice && choice.Id != Guid.Empty)
            {
                ids.Add(choice.Id);
            }
        }

        return ids;
    }

    private void LiveValidate()
    {
        if (_session.Current is null)
        {
            _validation.Text = string.Empty;
            return;
        }

        ApplyFormToSession();
        _validation.Text = _session.Current.Validate(out var error) ? string.Empty : error;
    }

    private async Task SaveAsync(SaveContentIntent intent)
    {
        ApplyFormToSession();
        var result = await _session.SaveCurrentAsync(intent).ConfigureAwait(true);
        switch (result)
        {
            case SaveGameSystemResult.Success success:
                StatusChanged?.Invoke(
                    intent == SaveContentIntent.Publish
                        ? $"Publié rev={success.PublishedRevision}"
                        : $"Brouillon enregistré rev={success.NewRevision}");
                await RefreshListAsync().ConfigureAwait(true);
                BindForm();
                break;
            case SaveGameSystemResult.ValidationFailed validation:
                GameDataUiMessageBox.Show(
                    this,
                    validation.Error,
                    "Validation",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                break;
            case SaveGameSystemResult.Conflict conflict:
                GameDataUiMessageBox.Show(
                    this,
                    $"Conflit de révision (courante={conflict.CurrentRevision}).",
                    "Conflit");
                break;
            case SaveGameSystemResult.NotDurable notDurable:
                GameDataUiMessageBox.Show(this, notDurable.Message, "Persistance");
                break;
            case SaveGameSystemResult.PersistenceFailed persistence:
                GameDataUiMessageBox.Show(this, persistence.Error, "Erreur");
                break;
        }
    }

    private async Task DeleteAsync()
    {
        var result = await _session.DeleteCurrentAsync().ConfigureAwait(true);
        switch (result)
        {
            case DeleteGameSystemResult.Success:
                StatusChanged?.Invoke("Supprimé");
                await RefreshListAsync().ConfigureAwait(true);
                break;
            case DeleteGameSystemResult.NotFound:
                GameDataUiMessageBox.Show(this, "Système introuvable.");
                break;
            case DeleteGameSystemResult.PersistenceFailed persistence:
                GameDataUiMessageBox.Show(this, persistence.Error, "Erreur");
                break;
        }
    }

    private ActorChoice? NextPartyChoice()
    {
        var used = ReadParty().ToHashSet();
        return _actorColumn.Items
            .Cast<ActorChoice>()
            .FirstOrDefault(actor => !used.Contains(actor.Id) && !actor.Label.StartsWith("[introuvable]", StringComparison.Ordinal));
    }

    private static DataGridView CreateFlagGrid()
    {
        var grid = new DataGridView
        {
            Width = 520,
            Height = 110,
            AllowUserToAddRows = false,
            AllowUserToDeleteRows = false,
            AutoGenerateColumns = false,
            RowHeadersVisible = false,
        };
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Clé", Width = 180 });
        grid.Columns.Add(new DataGridViewTextBoxColumn { HeaderText = "Libellé", Width = 260 });
        return grid;
    }

    private static Control FlagHost(DataGridView grid, Button add, Button remove)
    {
        var host = new Panel { Width = 540, Height = 148 };
        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 32,
            FlowDirection = FlowDirection.LeftToRight,
        };
        buttons.Controls.Add(add);
        buttons.Controls.Add(remove);
        grid.Dock = DockStyle.Fill;
        host.Controls.Add(grid);
        host.Controls.Add(buttons);
        return host;
    }

    private static void AttachGrid(DataGridView grid, Action mark)
    {
        grid.CellValueChanged += (_, _) => mark();
        grid.RowsRemoved += (_, _) => mark();
        grid.CurrentCellDirtyStateChanged += (_, _) =>
        {
            if (grid.IsCurrentCellDirty)
            {
                grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
            }
        };
        grid.DataError += (_, eventArgs) => eventArgs.ThrowException = false;
    }

    private static void FillFlagGrid(DataGridView grid, IReadOnlyList<NamedWorldFlag> flags)
    {
        grid.Rows.Clear();
        foreach (var flag in flags)
        {
            grid.Rows.Add(flag.Key, flag.Label);
        }
    }

    private static List<NamedWorldFlag> ReadFlagGrid(DataGridView grid)
    {
        var flags = new List<NamedWorldFlag>();
        foreach (DataGridViewRow row in grid.Rows)
        {
            if (row.IsNewRow)
            {
                continue;
            }

            var key = Convert.ToString(row.Cells[0].Value)?.Trim() ?? string.Empty;
            var label = Convert.ToString(row.Cells[1].Value)?.Trim() ?? string.Empty;
            if (key.Length == 0 && label.Length == 0)
            {
                continue;
            }

            flags.Add(new NamedWorldFlag { Key = key, Label = label });
        }

        return flags;
    }

    private static string NextFlagKey(DataGridView grid, string prefix)
    {
        var used = new HashSet<string>(StringComparer.Ordinal);
        foreach (DataGridViewRow row in grid.Rows)
        {
            var key = Convert.ToString(row.Cells[0].Value)?.Trim();
            if (!string.IsNullOrEmpty(key))
            {
                used.Add(key);
            }
        }

        if (!used.Contains(prefix))
        {
            return prefix;
        }

        for (var index = 2; index < GameSystemDefinition.MaxFlagEntries + 2; index++)
        {
            var key = prefix + "_" + index.ToString(System.Globalization.CultureInfo.InvariantCulture);
            if (!used.Contains(key))
            {
                return key;
            }
        }

        return prefix;
    }

    private static void RemoveCurrentRow(DataGridView grid)
    {
        if (grid.CurrentRow is { IsNewRow: false } row)
        {
            grid.Rows.Remove(row);
        }
    }

    private sealed record CatalogItem(Guid Id, string Label)
    {
        public override string ToString() => Label;
    }

    private sealed record ActorChoice(Guid Id, string Label)
    {
        public override string ToString() => Label;
    }
}
