using Frog.Application.Content;
using Frog.Core.Models;

namespace Frog.Editor.Forms.GameData;

/// <summary>
/// Fiche Système (base VX) : catalogues d’interrupteurs et de variables nommés.
/// Brouillon / publication via le dépôt Phase 8 déjà utilisé par le contenu nommé.
/// </summary>
public sealed class SystemEditorPanel : UserControl
{
    private readonly GameDataPanelLifecycle _lifecycle = new();
    private readonly SystemCatalogWorkspaceSession _switches;
    private readonly SystemCatalogWorkspaceSession _variables;
    private readonly ContentRepositoryCapabilities _capabilities;
    private readonly ComboBox _slot = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
    private readonly ListBox _list = new() { Dock = DockStyle.Fill };
    private readonly TextBox _search = new() { Dock = DockStyle.Top, PlaceholderText = "Rechercher…" };
    private readonly ComboBox _statusFilter = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _key = new() { Width = 280 };
    private readonly TextBox _label = new() { Width = 280 };
    private readonly Label _kind = new() { AutoSize = true, Text = "Interrupteur" };
    private readonly TextBox _note = new()
    {
        Width = 360,
        Height = 72,
        Multiline = true,
        ScrollBars = ScrollBars.Vertical,
    };
    private readonly Label _meta = new() { AutoSize = true };
    private readonly Label _validation = new() { AutoSize = true, ForeColor = Color.Firebrick };
    private readonly Button _btnNew = new() { Text = "Nouveau", AutoSize = true };
    private readonly Button _btnDup = new() { Text = "Dupliquer", AutoSize = true };
    private readonly Button _btnSave = new() { Text = "Enregistrer brouillon", AutoSize = true };
    private readonly Button _btnPublish = new() { Text = "Publier", AutoSize = true };
    private readonly Button _btnDelete = new() { Text = "Supprimer", AutoSize = true };
    private SystemCatalogSlot _activeSlot = SystemCatalogSlot.Switch;
    private bool _suppressList;
    private bool _suppressSlot;
    private bool _binding;

    public event Action<string>? StatusChanged;

    public bool IsDirty => _switches.IsDirty || _variables.IsDirty;

    internal long CurrentRevisionForTest => Active.CurrentRevision;

    internal long? PublishedRevisionForTest => Active.PublishedRevision;

    internal ContentPublishStatus CurrentStatusForTest => Active.CurrentStatus;

    internal GameDataPanelLifecycle LifecycleForTest => _lifecycle;

    internal Task<bool> DrainAsync(TimeSpan? timeout = null) => _lifecycle.DrainAsync(timeout ?? TimeSpan.FromSeconds(30));

    internal void BeginClosing() => _lifecycle.BeginClosing();

    internal void DisposeLifecycle() => _lifecycle.Dispose();

    internal Button BtnNewForTest => _btnNew;

    internal Button BtnDupForTest => _btnDup;

    internal Button BtnSaveForTest => _btnSave;

    internal Button BtnPublishForTest => _btnPublish;

    internal Button BtnDeleteForTest => _btnDelete;

    internal TextBox NameForTest => _label;

    internal TextBox KeyForTest => _key;

    internal TextBox NoteForTest => _note;

    internal TextBox SearchForTest => _search;

    internal ComboBox StatusFilterForTest => _statusFilter;

    internal ComboBox SlotForTest => _slot;

    internal Label TypeLabelForTest => _kind;

    internal ListBox ListForTest => _list;

    internal Label ValidationForTest => _validation;

    private SystemCatalogWorkspaceSession Active =>
        _activeSlot == SystemCatalogSlot.Variable ? _variables : _switches;

    public SystemEditorPanel(IPhase8ContentEditorRepository repository)
    {
        ArgumentNullException.ThrowIfNull(repository);
        _switches = new SystemCatalogWorkspaceSession(repository, SystemCatalogSlot.Switch);
        _variables = new SystemCatalogWorkspaceSession(repository, SystemCatalogSlot.Variable);
        _capabilities = repository.Capabilities;

        _slot.Items.AddRange(new object[] { "Interrupteurs", "Variables" });
        _slot.SelectedIndex = 0;
        _statusFilter.Items.AddRange(new object[] { "Tous", "Brouillon", "Publié" });
        _statusFilter.SelectedIndex = 0;

        var left = new Panel { Dock = DockStyle.Left, Width = 280, Padding = new Padding(4) };
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
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        void Row(string caption, Control control)
        {
            var row = form.RowCount++;
            form.Controls.Add(
                new Label { Text = caption, AutoSize = true, Anchor = AnchorStyles.Left },
                0,
                row);
            form.Controls.Add(control, 1, row);
        }

        Row("Catalogue", _slot);
        Row("Type", _kind);
        Row("Identifiant", _key);
        Row("Libellé", _label);
        Row("Note", _note);
        Row("État", _meta);
        Row("", _validation);
        Row(
            "",
            new Label
            {
                AutoSize = true,
                MaximumSize = new Size(420, 0),
                Text = "L’identifiant est celui des commandes d’événement (interrupteur ou variable).",
            });

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

        _slot.SelectedIndexChanged += (_, _) => _ = _lifecycle.RunAsync(ChangeSlotAsync, "refresh");
        _search.TextChanged += (_, _) => _ = _lifecycle.RunAsync(async ct =>
        {
            _switches.SearchFilter = _search.Text;
            _variables.SearchFilter = _search.Text;
            await RefreshListAsync(ct).ConfigureAwait(true);
        }, "refresh");
        _statusFilter.SelectedIndexChanged += (_, _) => _ = _lifecycle.RunAsync(async ct =>
        {
            var status = _statusFilter.SelectedIndex switch
            {
                1 => ContentPublishStatus.Draft,
                2 => ContentPublishStatus.Published,
                _ => (ContentPublishStatus?)null,
            };
            _switches.StatusFilter = status;
            _variables.StatusFilter = status;
            await RefreshListAsync(ct).ConfigureAwait(true);
        }, "refresh");
        _list.SelectedIndexChanged += (_, _) => _ = _lifecycle.RunAsync(async _ =>
        {
            if (_suppressList || _list.SelectedItem is not CatalogItem item)
            {
                return;
            }

            if (!GameDataListNavigation.ConfirmDiscardUnsavedChanges(this, "Système", Active.IsDirty))
            {
                GameDataListNavigation.RevertListSelection(
                    _list,
                    ref _suppressList,
                    Active.CurrentId,
                    listItem => ((CatalogItem)listItem).Id);
                return;
            }

            await Active.OpenAsync(item.Id).ConfigureAwait(true);
            BindForm();
        }, "refresh");

        void Mark()
        {
            if (_binding)
            {
                return;
            }

            ApplyFormToSession();
            Active.MarkDirty();
            LiveValidate();
            StatusChanged?.Invoke("Modifié (non enregistré)");
        }

        _key.TextChanged += (_, _) => Mark();
        _label.TextChanged += (_, _) => Mark();
        _note.TextChanged += (_, _) => Mark();

        _btnNew.Click += (_, _) =>
        {
            Active.AdoptNewDraft(SystemCatalogEntry.CreateNew(_activeSlot));
            BindForm();
            StatusChanged?.Invoke("Nouveau brouillon");
        };
        _btnDup.Click += (_, _) =>
        {
            if (Active.Current is null)
            {
                return;
            }

            Active.DuplicateCurrent();
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
    }

    public async Task InitializeAsync()
    {
        await _switches.RefreshCatalogAsync().ConfigureAwait(true);
        await _variables.RefreshCatalogAsync().ConfigureAwait(true);
        await RefreshListAsync().ConfigureAwait(true);
        StatusChanged?.Invoke($"Backend système : {_capabilities.DisplayLabel}");
    }

    internal void QueueRefreshList()
        => _ = _lifecycle.RunAsync(async ct => await RefreshListAsync(ct).ConfigureAwait(true), "refresh");

    private async Task ChangeSlotAsync(CancellationToken ct)
    {
        if (_suppressSlot)
        {
            return;
        }

        var requested = _slot.SelectedIndex == 1 ? SystemCatalogSlot.Variable : SystemCatalogSlot.Switch;
        if (requested == _activeSlot)
        {
            return;
        }

        if (!GameDataListNavigation.ConfirmDiscardUnsavedChanges(this, "Système", Active.IsDirty))
        {
            _suppressSlot = true;
            _slot.SelectedIndex = _activeSlot == SystemCatalogSlot.Variable ? 1 : 0;
            _suppressSlot = false;
            return;
        }

        if (Active.IsDirty)
        {
            await Active.DiscardEditsAsync(ct).ConfigureAwait(true);
        }

        _switches.SearchFilter = _search.Text;
        _variables.SearchFilter = _search.Text;
        _activeSlot = requested;
        _kind.Text = requested == SystemCatalogSlot.Variable ? "Variable" : "Interrupteur";
        await RefreshListAsync(ct).ConfigureAwait(true);
        BindForm();
    }

    private async Task RefreshListAsync(CancellationToken ct = default)
    {
        await Active.RefreshCatalogAsync(ct).ConfigureAwait(true);
        if (ct.IsCancellationRequested)
        {
            return;
        }

        _suppressList = true;
        _list.Items.Clear();
        var selected = -1;
        var index = 0;
        foreach (var entry in Active.Catalog)
        {
            _list.Items.Add(new CatalogItem(
                entry.Id,
                $"{entry.Label} ({entry.Key}) [{entry.Status}]"));
            if (Active.CurrentId == entry.Id)
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
        var definition = Active.Current;
        _binding = true;
        try
        {
            _kind.Text = _activeSlot == SystemCatalogSlot.Variable ? "Variable" : "Interrupteur";
            if (definition is null)
            {
                _key.Text = string.Empty;
                _label.Text = string.Empty;
                _note.Text = string.Empty;
                _meta.Text = string.Empty;
                _validation.Text = string.Empty;
                return;
            }

            _key.Text = definition.Key;
            _label.Text = definition.Label;
            _note.Text = definition.Note ?? string.Empty;
            _meta.Text =
                $"Id={definition.Id:N}  rev={Active.CurrentRevision}  statut={Active.CurrentStatus}  publié={Active.PublishedRevision?.ToString() ?? "—"}";
        }
        finally
        {
            _binding = false;
        }

        LiveValidate();
    }

    private void ApplyFormToSession()
    {
        if (Active.Current is null)
        {
            return;
        }

        Active.Current.Key = _key.Text.Trim();
        Active.Current.Label = _label.Text.Trim();
        Active.Current.Note = _note.Text.Trim();
    }

    private void LiveValidate()
    {
        if (Active.Current is null)
        {
            _validation.Text = string.Empty;
            return;
        }

        ApplyFormToSession();
        _validation.Text = Active.Current.Validate(_activeSlot, out var error) ? string.Empty : error;
    }

    private async Task SaveAsync(SaveContentIntent intent)
    {
        ApplyFormToSession();
        var result = await Active.SaveCurrentAsync(intent).ConfigureAwait(true);
        switch (result)
        {
            case Phase8SaveContentResult.Success success:
                StatusChanged?.Invoke(
                    intent == SaveContentIntent.Publish
                        ? $"Publié rev={success.PublishedRevision}"
                        : $"Brouillon enregistré rev={success.NewRevision}");
                await RefreshListAsync().ConfigureAwait(true);
                BindForm();
                break;
            case Phase8SaveContentResult.ValidationFailed validation:
                GameDataUiMessageBox.Show(
                    this,
                    validation.Error,
                    "Validation",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                break;
            case Phase8SaveContentResult.Conflict conflict:
                GameDataUiMessageBox.Show(
                    this,
                    $"Conflit de révision (courante={conflict.CurrentRevision}).",
                    "Conflit");
                break;
            case Phase8SaveContentResult.PersistenceFailed persistence:
                GameDataUiMessageBox.Show(this, persistence.Error, "Erreur");
                break;
        }
    }

    private async Task DeleteAsync()
    {
        var result = await Active.DeleteCurrentAsync().ConfigureAwait(true);
        switch (result)
        {
            case Phase8DeleteContentResult.Success:
                StatusChanged?.Invoke("Supprimé");
                await RefreshListAsync().ConfigureAwait(true);
                BindForm();
                break;
            case Phase8DeleteContentResult.NotFound:
                GameDataUiMessageBox.Show(this, "Entrée introuvable.");
                break;
            case Phase8DeleteContentResult.PersistenceFailed persistence:
                GameDataUiMessageBox.Show(this, persistence.Error, "Erreur");
                break;
        }
    }

    private sealed record CatalogItem(Guid Id, string Label)
    {
        public override string ToString() => Label;
    }
}
