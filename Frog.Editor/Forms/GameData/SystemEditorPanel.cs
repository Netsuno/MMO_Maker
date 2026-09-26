using Frog.Application.Content;
using Frog.Core.Enums;
using Frog.Core.Models;
using Frog.Editor.Services;

namespace Frog.Editor.Forms.GameData;

/// <summary>
/// Fiche Système (base VX) : catalogues nommés des interrupteurs et des variables
/// utilisés comme identifiants de commandes d’événement.
/// </summary>
public sealed class SystemEditorPanel : UserControl
{
    private readonly GameDataPanelLifecycle _lifecycle = new();
    private readonly SystemFlagWorkspaceSession _session;
    private readonly ContentRepositoryCapabilities _capabilities;
    private readonly ListBox _list = new() { Dock = DockStyle.Fill };
    private readonly TextBox _search = new() { Dock = DockStyle.Top, PlaceholderText = "Rechercher…" };
    private readonly ComboBox _statusFilter = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _kindFilter = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _label = new() { Width = 280 };
    private readonly TextBox _key = new() { Width = 280 };
    private readonly Label _kind = new() { AutoSize = true, Text = "Interrupteur" };
    private readonly TextBox _note = new()
    {
        Width = 360,
        Height = 90,
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

    internal TextBox NameForTest => _label;

    internal TextBox KeyForTest => _key;

    internal TextBox NoteForTest => _note;

    internal TextBox SearchForTest => _search;

    internal ComboBox StatusFilterForTest => _statusFilter;

    internal ComboBox KindFilterForTest => _kindFilter;

    internal Label TypeLabelForTest => _kind;

    internal ListBox ListForTest => _list;

    internal Label ValidationForTest => _validation;

    public SystemEditorPanel(
        SystemFlagWorkspaceSession session,
        ContentRepositoryCapabilities capabilities)
    {
        _session = session;
        _capabilities = capabilities;
        _session.KindFilter = SystemFlagKind.Switch;

        _statusFilter.Items.AddRange(new object[] { "Tous", "Brouillon", "Publié" });
        _statusFilter.SelectedIndex = 0;
        _kindFilter.Items.Add(SystemFlagKindLabels.FrenchList(SystemFlagKind.Switch));
        _kindFilter.Items.Add(SystemFlagKindLabels.FrenchList(SystemFlagKind.Variable));
        _kindFilter.SelectedIndex = 0;

        var left = new Panel { Dock = DockStyle.Left, Width = 280, Padding = new Padding(4) };
        left.Controls.Add(_list);
        left.Controls.Add(_search);
        left.Controls.Add(_statusFilter);
        left.Controls.Add(_kindFilter);

        var form = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(12),
            AutoScroll = true,
        };
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
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

        Row("Libellé", _label);
        Row("Identifiant", _key);
        Row("Type", _kind);
        Row("Note", _note);
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
        _kindFilter.SelectedIndexChanged += (_, _) => _ = _lifecycle.RunAsync(async ct =>
        {
            if (_binding)
            {
                return;
            }

            var next = SelectedKind();
            if (next == _session.KindFilter)
            {
                return;
            }

            if (!GameDataListNavigation.ConfirmDiscardUnsavedChanges(this, "Système", _session.IsDirty))
            {
                _binding = true;
                _kindFilter.SelectedIndex = _session.KindFilter == SystemFlagKind.Variable ? 1 : 0;
                _binding = false;
                return;
            }

            _session.KindFilter = next;
            _session.ClearCurrent();
            await RefreshListAsync(ct).ConfigureAwait(true);
            BindForm();
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

        _label.TextChanged += (_, _) => Mark();
        _key.TextChanged += (_, _) => Mark();
        _note.TextChanged += (_, _) => Mark();

        _btnNew.Click += (_, _) =>
        {
            var kind = _session.KindFilter;
            _session.AdoptNewDraft(new SystemFlagDefinition
            {
                Id = Guid.NewGuid(),
                Kind = kind,
                Key = NewCommandKey(kind),
                Label = kind == SystemFlagKind.Variable ? "Nouvelle variable" : "Nouvel interrupteur",
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
    }

    public async Task InitializeAsync()
    {
        await RefreshListAsync().ConfigureAwait(true);
        StatusChanged?.Invoke($"Backend système : {_capabilities.DisplayLabel}");
    }

    internal void QueueRefreshList()
        => _ = _lifecycle.RunAsync(async ct => await RefreshListAsync(ct).ConfigureAwait(true), "refresh");

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
                entry.FlagId,
                $"{entry.Label} ({entry.Key}) [{entry.Status}]"));
            if (_session.CurrentId == entry.FlagId)
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
        _binding = true;
        try
        {
            if (definition is null)
            {
                _label.Text = string.Empty;
                _key.Text = string.Empty;
                _note.Text = string.Empty;
                _kind.Text = SystemFlagKindLabels.French(_session.KindFilter);
                _meta.Text = string.Empty;
                _validation.Text = string.Empty;
                return;
            }

            _label.Text = definition.Label;
            _key.Text = definition.Key;
            _kind.Text = SystemFlagKindLabels.French(definition.Kind);
            _note.Text = definition.Note ?? string.Empty;
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

        _session.Current.Label = _label.Text.Trim();
        _session.Current.Key = _key.Text.Trim();
        _session.Current.Kind = _session.KindFilter;
        _session.Current.Note = string.IsNullOrWhiteSpace(_note.Text) ? null : _note.Text.Trim();
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
            case SaveSystemFlagResult.Success success:
                StatusChanged?.Invoke(
                    intent == SaveContentIntent.Publish
                        ? $"Publié rev={success.PublishedRevision}"
                        : $"Brouillon enregistré rev={success.NewRevision}");
                await RefreshListAsync().ConfigureAwait(true);
                BindForm();
                break;
            case SaveSystemFlagResult.ValidationFailed validation:
                GameDataUiMessageBox.Show(
                    this,
                    validation.Error,
                    "Validation",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                break;
            case SaveSystemFlagResult.Conflict conflict:
                GameDataUiMessageBox.Show(
                    this,
                    $"Conflit de révision (courante={conflict.CurrentRevision}).",
                    "Conflit");
                break;
            case SaveSystemFlagResult.NotDurable notDurable:
                GameDataUiMessageBox.Show(this, notDurable.Message, "Persistance");
                break;
            case SaveSystemFlagResult.PersistenceFailed persistence:
                GameDataUiMessageBox.Show(this, persistence.Error, "Erreur");
                break;
        }
    }

    private async Task DeleteAsync()
    {
        var result = await _session.DeleteCurrentAsync().ConfigureAwait(true);
        switch (result)
        {
            case DeleteSystemFlagResult.Success:
                StatusChanged?.Invoke("Supprimé");
                await RefreshListAsync().ConfigureAwait(true);
                BindForm();
                break;
            case DeleteSystemFlagResult.NotFound:
                GameDataUiMessageBox.Show(this, "Entrée introuvable.");
                break;
            case DeleteSystemFlagResult.PersistenceFailed persistence:
                GameDataUiMessageBox.Show(this, persistence.Error, "Erreur");
                break;
        }
    }

    private SystemFlagKind SelectedKind()
        => _kindFilter.SelectedIndex == 1 ? SystemFlagKind.Variable : SystemFlagKind.Switch;

    private static string NewCommandKey(SystemFlagKind kind)
    {
        var prefix = kind == SystemFlagKind.Variable ? "var_" : "sw_";
        return prefix + Guid.NewGuid().ToString("N")[..12];
    }

    private sealed record CatalogItem(Guid Id, string Label)
    {
        public override string ToString() => Label;
    }
}
