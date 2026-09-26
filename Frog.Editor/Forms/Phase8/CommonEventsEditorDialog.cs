using Frog.Application.Content;
using Frog.Core.Events;
using Frog.Core.Models;
using Frog.Editor.Forms.GameData;
using Frog.Editor.Services;

namespace Frog.Editor.Forms.Phase8;

/// <summary>
/// Liste VX des événements communs et éditeur de pages (déclencheur, interrupteur, commandes).
/// Persistance : catalogue Phase 8 <see cref="Phase8ContentKind.CommonEvent"/> (brouillon / publication).
/// </summary>
internal sealed class CommonEventsEditorDialog : Form
{
    private readonly Phase8ContentPostgreSqlService _service;
    private readonly GameDataPanelLifecycle _lifecycle = new();
    private readonly TextBox _filter = new() { Width = 240, PlaceholderText = "Filtrer…" };
    private readonly ComboBox _statusFilter = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 140 };
    private readonly ListBox _list = new() { Dock = DockStyle.Fill, IntegralHeight = false };
    private readonly TextBox _name = new() { Width = 280 };
    private readonly NumericUpDown _alias = new() { Minimum = 0, Maximum = 999999, Width = 80 };
    private readonly ComboBox _trigger = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 180 };
    private readonly TextBox _switchId = new() { Width = 180 };
    private readonly CheckBox _switchActive = new() { Text = "Actif", AutoSize = true, Checked = true, Enabled = false };
    private readonly Label _meta = new() { AutoSize = true };
    private readonly Label _validation = new() { AutoSize = true, ForeColor = Color.Firebrick, MaximumSize = new Size(720, 0) };
    private readonly Phase8CommonEventEditorPanel _editor;
    private readonly Button _btnNew = new() { Text = "Nouveau", AutoSize = true };
    private readonly Button _btnDuplicate = new() { Text = "Dupliquer", AutoSize = true };
    private readonly Button _btnDelete = new() { Text = "Supprimer", AutoSize = true };
    private readonly Button _btnSave = new() { Text = "Enregistrer brouillon", AutoSize = true };
    private readonly Button _btnPublish = new() { Text = "Publier", AutoSize = true };

    private readonly List<Phase8ContentListRow> _rows = new();
    private Guid _currentId = Guid.Empty;
    private long _currentRevision;
    private ContentPublishStatus _currentStatus = ContentPublishStatus.Draft;
    private long? _publishedRevision;
    private bool _dirty;
    private bool _binding;
    private bool _suppressList;
    private bool _editorEnabled;
    private volatile bool _allowCloseAfterCleanup;
    private volatile bool _cleanupRunning;
    private volatile bool _closeCleanupFailed;

    public CommonEventsEditorDialog(Phase8ContentPostgreSqlService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        _editor = new Phase8CommonEventEditorPanel(showName: false) { Dock = DockStyle.Fill };

        Text = "Événements communs";
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(1100, 740);
        MinimumSize = new Size(880, 560);

        _statusFilter.Items.AddRange(new object[] { "Tous", "Brouillon", "Publié" });
        _statusFilter.SelectedIndex = 0;
        foreach (var kind in CommonEventEditorSheet.TriggerOrder)
        {
            _trigger.Items.Add(new TriggerChoice(kind, MapEventEditorLabels.Trigger(kind)));
        }

        if (_trigger.Items.Count > 0)
        {
            _trigger.SelectedIndex = 0;
        }

        var left = new Panel { Dock = DockStyle.Left, Width = 280, Padding = new Padding(8) };
        var leftTop = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            WrapContents = true,
            Padding = new Padding(0, 0, 0, 4),
        };
        leftTop.Controls.Add(_filter);
        leftTop.Controls.Add(_statusFilter);
        var leftButtons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            WrapContents = true,
            Padding = new Padding(0, 4, 0, 0),
        };
        leftButtons.Controls.Add(_btnNew);
        leftButtons.Controls.Add(_btnDuplicate);
        leftButtons.Controls.Add(_btnDelete);
        left.Controls.Add(_list);
        left.Controls.Add(leftButtons);
        left.Controls.Add(leftTop);

        var header = new FlowLayoutPanel
        {
            Dock = DockStyle.Fill,
            AutoSize = true,
            WrapContents = true,
            Padding = new Padding(4),
        };
        header.Controls.Add(Label("Nom"));
        header.Controls.Add(_name);
        header.Controls.Add(Label("N°"));
        header.Controls.Add(_alias);
        header.Controls.Add(Label("Déclencheur"));
        header.Controls.Add(_trigger);
        header.Controls.Add(Label("Interrupteur"));
        header.Controls.Add(_switchId);
        header.Controls.Add(_switchActive);

        var hint = new Label
        {
            AutoSize = true,
            MaximumSize = new Size(780, 0),
            ForeColor = Color.DimGray,
            Padding = new Padding(4, 0, 4, 4),
            Text = "Déclencheur et interrupteur de la page sélectionnée. Les commandes sont celles des événements de carte.",
        };

        var right = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
        right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
        right.Controls.Add(header, 0, 0);
        var metaHost = new FlowLayoutPanel { AutoSize = true, FlowDirection = FlowDirection.TopDown, WrapContents = false, Dock = DockStyle.Fill };
        metaHost.Controls.Add(_meta);
        metaHost.Controls.Add(hint);
        metaHost.Controls.Add(_validation);
        right.Controls.Add(metaHost, 0, 1);
        right.Controls.Add(_editor, 0, 2);

        var bottom = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            AutoSize = true,
            FlowDirection = FlowDirection.RightToLeft,
            Padding = new Padding(8),
        };
        var btnClose = new Button { Text = "Fermer", DialogResult = DialogResult.Cancel, AutoSize = true };
        bottom.Controls.Add(btnClose);
        bottom.Controls.Add(_btnPublish);
        bottom.Controls.Add(_btnSave);

        Controls.Add(right);
        Controls.Add(bottom);
        Controls.Add(left);
        CancelButton = btnClose;

        var canWrite = _service.Capabilities.AllowsSave;
        _btnNew.Enabled = canWrite;
        _btnDuplicate.Enabled = canWrite;
        _btnDelete.Enabled = canWrite;
        _btnSave.Enabled = canWrite;
        _btnPublish.Enabled = canWrite;

        _filter.TextChanged += (_, _) => RefreshFilteredList();
        _statusFilter.SelectedIndexChanged += (_, _) => RefreshFilteredList();
        _list.SelectedIndexChanged += (_, _) => _ = _lifecycle.RunAsync(OnListSelectedAsync, "select");
        _name.TextChanged += (_, _) => MarkDirtyFromHeader();
        _alias.ValueChanged += (_, _) => MarkDirtyFromHeader();
        _trigger.SelectedIndexChanged += (_, _) =>
        {
            if (_binding || !_editorEnabled)
            {
                return;
            }

            ApplyHeaderToSelectedPage();
        };
        _switchId.TextChanged += (_, _) =>
        {
            _switchActive.Enabled = _editorEnabled && !string.IsNullOrWhiteSpace(_switchId.Text);
            MarkDirtyFromHeader();
        };
        _switchId.Leave += (_, _) =>
        {
            if (!_binding && _editorEnabled)
            {
                ApplyHeaderToSelectedPage();
            }
        };
        _switchActive.CheckedChanged += (_, _) =>
        {
            if (!_binding && _editorEnabled)
            {
                ApplyHeaderToSelectedPage();
            }
        };
        _editor.ContentChanged += () =>
        {
            if (_binding)
            {
                return;
            }

            RefreshHeaderFromPages();
            MarkDirty();
        };

        _btnNew.Click += (_, _) => _ = _lifecycle.RunAsync(NewDraftAsync, "new");
        _btnDuplicate.Click += (_, _) => _ = _lifecycle.RunAsync(DuplicateAsync, "duplicate");
        _btnDelete.Click += (_, _) => _ = _lifecycle.RunAsync(DeleteAsync, "delete");
        _btnSave.Click += (_, _) => _ = _lifecycle.RunAsync(ct => SaveAsync(SaveContentIntent.SaveDraft, ct), "save");
        _btnPublish.Click += (_, _) => _ = _lifecycle.RunAsync(ct => SaveAsync(SaveContentIntent.Publish, ct), "publish");

        SetEditorEnabled(false);
        UpdateMeta();
        FormClosing += OnFormClosing;
        Shown += (_, _) =>
        {
            if (IsDisposed || _cleanupRunning || _allowCloseAfterCleanup)
            {
                return;
            }

            _ = _lifecycle.RunAsync(ReloadListAsync, "init");
        };
    }

    internal GameDataPanelLifecycle LifecycleForTest => _lifecycle;

    internal bool IsDirtyForTest => _dirty;

    internal TextBox NameForTest => _name;

    internal TextBox FilterForTest => _filter;

    internal TextBox SwitchForTest => _switchId;

    internal CheckBox SwitchActiveForTest => _switchActive;

    internal NumericUpDown AliasForTest => _alias;

    internal ComboBox TriggerForTest => _trigger;

    internal ListBox ListForTest => _list;

    internal Label ValidationForTest => _validation;

    internal Button BtnNewForTest => _btnNew;

    internal Button BtnDuplicateForTest => _btnDuplicate;

    internal Button BtnDeleteForTest => _btnDelete;

    internal Button BtnSaveForTest => _btnSave;

    internal Button BtnPublishForTest => _btnPublish;

    internal Phase8CommonEventEditorPanel EditorForTest => _editor;

    internal Guid CurrentIdForTest => _currentId;

    internal long CurrentRevisionForTest => _currentRevision;

    internal ContentPublishStatus CurrentStatusForTest => _currentStatus;

    internal bool CloseCleanupFailedForTest => _closeCleanupFailed;

    internal void SelectTriggerForTest(string kind)
    {
        for (var i = 0; i < _trigger.Items.Count; i++)
        {
            if (_trigger.Items[i] is TriggerChoice choice && choice.Kind == kind)
            {
                _trigger.SelectedIndex = i;
                return;
            }
        }

        throw new InvalidOperationException($"Déclencheur inconnu: {kind}.");
    }

    internal void FlushHeaderForTest() => ApplyHeaderToSelectedPage();

    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            _lifecycle.Dispose();
        }

        base.Dispose(disposing);
    }

    private async Task OnListSelectedAsync(CancellationToken ct)
    {
        if (_suppressList || _list.SelectedItem is not CommonEventRow row)
        {
            return;
        }

        if (row.Id == _currentId && _currentRevision > 0 && !_dirty)
        {
            return;
        }

        if (!ConfirmDiscardIfDirty())
        {
            ReselectCurrent();
            return;
        }

        await LoadDraftAsync(row.Id, ct).ConfigureAwait(true);
    }

    private async Task ReloadListAsync(CancellationToken ct)
    {
        await MapEventShopChoiceSource.RefreshAsync(ct).ConfigureAwait(true);
        var items = await _service.ListAsync(Phase8ContentKind.CommonEvent, ct).ConfigureAwait(true);
        if (ct.IsCancellationRequested)
        {
            return;
        }

        _rows.Clear();
        _rows.AddRange(items);
        RefreshFilteredList();
    }

    private void RefreshFilteredList()
    {
        var filter = _filter.Text.Trim();
        var status = SelectedStatusFilter();
        var comparison = StringComparison.CurrentCultureIgnoreCase;
        _suppressList = true;
        try
        {
            _list.BeginUpdate();
            _list.Items.Clear();
            foreach (var row in _rows
                         .OrderBy(r => r.EditorAliasId is > 0 ? r.EditorAliasId.Value : int.MaxValue)
                         .ThenBy(r => r.Name, StringComparer.CurrentCultureIgnoreCase))
            {
                if (status is not null && row.Status != status)
                {
                    continue;
                }

                var line = CommonEventEditorSheet.FormatListLine(row.EditorAliasId, row.Name, StatusLabel(row.Status));
                if (filter.Length > 0
                    && !line.Contains(filter, comparison)
                    && !row.Id.ToString("D").Contains(filter, comparison))
                {
                    continue;
                }

                var item = new CommonEventRow(row.Id, row.Name, row.EditorAliasId, row.Status, line);
                _list.Items.Add(item);
                if (row.Id == _currentId)
                {
                    _list.SelectedItem = item;
                }
            }
        }
        finally
        {
            _list.EndUpdate();
            _suppressList = false;
        }
    }

    private async Task LoadDraftAsync(Guid id, CancellationToken ct)
    {
        var stored = await _service.LoadDraftAsync(id, ct).ConfigureAwait(true);
        if (ct.IsCancellationRequested || stored is null || stored.Kind != Phase8ContentKind.CommonEvent)
        {
            _validation.Text = "Événement commun introuvable.";
            return;
        }

        BindStored(stored);
    }

    private void BindStored(Phase8StoredContent stored)
    {
        _binding = true;
        try
        {
            _currentId = stored.Id;
            _currentRevision = stored.Revision;
            _currentStatus = stored.Status;
            _publishedRevision = stored.PublishedRevision;
            _dirty = false;
            SetEditorEnabled(true);
            _name.Text = stored.Name;
            _alias.Value = Math.Clamp(stored.EditorAliasId ?? 0, (int)_alias.Minimum, (int)_alias.Maximum);
            _editor.LoadPayload(stored.PayloadJson);
            _editor.ContentId = stored.Id;
            _editor.CatalogName = stored.Name;
            RefreshHeaderFromPages();
            _validation.Text = string.Empty;
            UpdateMeta();
        }
        finally
        {
            _binding = false;
        }
    }

    private async Task NewDraftAsync(CancellationToken ct)
    {
        if (!ConfirmDiscardIfDirty())
        {
            return;
        }

        var newId = EditorTestHooks.OverrideNewContentIdFactory?.Invoke() ?? Guid.NewGuid();
        var alias = CommonEventEditorSheet.NextAlias(_rows.Select(r => r.EditorAliasId));
        _binding = true;
        try
        {
            _currentId = newId;
            _currentRevision = 0;
            _currentStatus = ContentPublishStatus.Draft;
            _publishedRevision = null;
            _dirty = true;
            SetEditorEnabled(true);
            _name.Text = "Nouvel événement commun";
            _alias.Value = alias;
            _editor.ResetForNew(newId);
            _editor.CatalogName = _name.Text;
            RefreshHeaderFromPages();
            _suppressList = true;
            _list.ClearSelected();
            _suppressList = false;
            _validation.Text = string.Empty;
            UpdateMeta();
        }
        finally
        {
            _binding = false;
        }

        await Task.CompletedTask.ConfigureAwait(true);
    }

    private async Task DuplicateAsync(CancellationToken ct)
    {
        if (_currentId == Guid.Empty)
        {
            _validation.Text = "Sélectionnez un événement à dupliquer.";
            return;
        }

        if (!TryComposeCurrent(out var definition, out var error) || definition is null)
        {
            _validation.Text = error ?? "Événement invalide.";
            return;
        }

        var newId = EditorTestHooks.OverrideNewContentIdFactory?.Invoke() ?? Guid.NewGuid();
        var copyName = "Copie de " + definition.Name;
        var aliases = _rows.Select(r => r.EditorAliasId).Append(definition.EditorAliasId);
        definition.Id = newId;
        definition.Name = copyName;
        definition.EditorAliasId = CommonEventEditorSheet.NextAlias(aliases);
        if (!definition.Validate(out error))
        {
            _validation.Text = error ?? "Duplication impossible.";
            return;
        }

        _binding = true;
        try
        {
            _currentId = newId;
            _currentRevision = 0;
            _currentStatus = ContentPublishStatus.Draft;
            _publishedRevision = null;
            _dirty = true;
            SetEditorEnabled(true);
            _name.Text = copyName;
            _alias.Value = definition.EditorAliasId ?? 0;
            var json = Phase8ContentPostgreSqlService.Serialize(definition);
            _editor.LoadPayload(json);
            _editor.ContentId = newId;
            _editor.CatalogName = copyName;
            RefreshHeaderFromPages();
            _suppressList = true;
            _list.ClearSelected();
            _suppressList = false;
            _validation.Text = string.Empty;
            UpdateMeta();
        }
        finally
        {
            _binding = false;
        }

        await Task.CompletedTask.ConfigureAwait(true);
    }

    private async Task DeleteAsync(CancellationToken ct)
    {
        if (_currentId == Guid.Empty || _currentRevision == 0)
        {
            _validation.Text = "Rien à supprimer (brouillon non enregistré).";
            return;
        }

        if (GameDataUiMessageBox.Show(
                this,
                "Supprimer définitivement cet événement commun ?",
                "Événements communs",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning) != DialogResult.Yes)
        {
            return;
        }

        var result = await _service.DeleteAsync(_currentId, ct).ConfigureAwait(true);
        if (ct.IsCancellationRequested)
        {
            return;
        }

        switch (result)
        {
            case Phase8DeleteContentResult.Success:
                ClearEditor();
                await ReloadListAsync(ct).ConfigureAwait(true);
                GameDataUiMessageBox.Show(this, "Événement commun supprimé.", "Événements communs");
                break;
            case Phase8DeleteContentResult.NotFound:
                _validation.Text = "Événement commun introuvable.";
                break;
            case Phase8DeleteContentResult.PersistenceFailed failed:
                _validation.Text = failed.Error;
                break;
        }
    }

    private async Task SaveAsync(SaveContentIntent intent, CancellationToken ct)
    {
        if (_currentId == Guid.Empty)
        {
            _validation.Text = "Aucune sélection.";
            return;
        }

        if (!TryComposeCurrent(out var definition, out var error) || definition is null)
        {
            _validation.Text = error ?? "Événement invalide.";
            return;
        }

        if (definition.EditorAliasId is int alias
            && CommonEventEditorSheet.IsAliasTaken(
                _rows.Select(r => (r.Id, r.EditorAliasId)),
                _currentId,
                alias))
        {
            _validation.Text = "Ce numéro est déjà utilisé.";
            return;
        }

        var request = new Phase8SaveContentRequest
        {
            ContentId = _currentRevision > 0 ? _currentId : null,
            NewId = _currentRevision == 0 ? _currentId : null,
            Kind = Phase8ContentKind.CommonEvent,
            Name = definition.Name,
            EditorAliasId = definition.EditorAliasId,
            PayloadJson = Phase8ContentPostgreSqlService.Serialize(definition),
            ExpectedRevision = _currentRevision,
            Intent = intent,
        };

        var result = await _service.SaveAsync(request, ct).ConfigureAwait(true);
        if (ct.IsCancellationRequested)
        {
            return;
        }

        switch (result)
        {
            case Phase8SaveContentResult.Success success:
                _currentId = success.ContentId;
                _currentRevision = success.NewRevision;
                _currentStatus = intent == SaveContentIntent.Publish
                    ? ContentPublishStatus.Published
                    : ContentPublishStatus.Draft;
                _publishedRevision = success.PublishedRevision ?? _publishedRevision;
                _binding = true;
                try
                {
                    definition.Id = success.ContentId;
                    _editor.LoadPayload(Phase8ContentPostgreSqlService.Serialize(definition));
                    _editor.ContentId = success.ContentId;
                    _editor.CatalogName = definition.Name;
                    RefreshHeaderFromPages();
                    _dirty = false;
                    _validation.Text = string.Empty;
                    UpdateMeta();
                }
                finally
                {
                    _binding = false;
                }

                await ReloadListAsync(ct).ConfigureAwait(true);
                GameDataUiMessageBox.Show(
                    this,
                    intent == SaveContentIntent.Publish ? "Événement commun publié." : "Brouillon enregistré.",
                    "Événements communs");
                break;
            case Phase8SaveContentResult.Conflict conflict:
                _validation.Text = $"Conflit de révision (courante={conflict.CurrentRevision}). Rechargez l'entrée.";
                break;
            case Phase8SaveContentResult.ValidationFailed failed:
                _validation.Text = failed.Error;
                break;
            case Phase8SaveContentResult.PersistenceFailed failed:
                _validation.Text = failed.Error;
                break;
        }
    }

    private bool TryComposeCurrent(out CommonEventDefinition? definition, out string? error)
    {
        definition = null;
        _editor.CatalogName = _name.Text.Trim();
        if (!_editor.PagesPanelForTest.TryBuildPages(out var pages, out error))
        {
            return false;
        }

        var pageIndex = _editor.PagesPanelForTest.PagesForTest.SelectedIndex;
        return CommonEventEditorSheet.TryCompose(
            _currentId,
            _name.Text,
            CurrentAlias(),
            pages,
            pageIndex,
            SelectedTriggerKind(),
            CurrentSwitchId(),
            _switchActive.Checked,
            out definition,
            out error);
    }

    private void ApplyHeaderToSelectedPage()
    {
        if (_binding || !_editorEnabled || _currentId == Guid.Empty)
        {
            return;
        }

        if (!_editor.PagesPanelForTest.TryBuildPages(out var pages, out var error))
        {
            _validation.Text = error ?? "Pages invalides.";
            MarkDirty(clearValidation: false);
            return;
        }

        var pageIndex = _editor.PagesPanelForTest.PagesForTest.SelectedIndex;
        var nextSwitch = CurrentSwitchId();
        if (CommonEventEditorSheet.TryReadPage(pages, pageIndex, out var currentTrigger, out var currentSwitch, out var currentValue)
            && string.Equals(currentTrigger, SelectedTriggerKind(), StringComparison.Ordinal)
            && string.Equals(currentSwitch ?? string.Empty, nextSwitch ?? string.Empty, StringComparison.Ordinal)
            && (nextSwitch is null || currentValue == _switchActive.Checked))
        {
            return;
        }

        if (!CommonEventEditorSheet.TryApplyToPage(
                pages,
                pageIndex,
                SelectedTriggerKind(),
                nextSwitch,
                _switchActive.Checked,
                out var updated,
                out error))
        {
            _validation.Text = error ?? "Déclencheur ou interrupteur invalide.";
            MarkDirty(clearValidation: false);
            return;
        }

        var restore = pageIndex < 0 ? 0 : pageIndex;
        _binding = true;
        try
        {
            _editor.PagesPanelForTest.LoadPages(updated);
            if (restore >= 0 && restore < _editor.PagesPanelForTest.PagesForTest.Items.Count)
            {
                _editor.PagesPanelForTest.PagesForTest.SelectedIndex = restore;
            }

            _validation.Text = string.Empty;
        }
        finally
        {
            _binding = false;
        }

        MarkDirty();
    }

    private void RefreshHeaderFromPages()
    {
        if (!_editor.PagesPanelForTest.TryBuildPages(out var pages, out _))
        {
            return;
        }

        var pageIndex = _editor.PagesPanelForTest.PagesForTest.SelectedIndex;
        if (!CommonEventEditorSheet.TryReadPage(pages, pageIndex, out var trigger, out var switchId, out var switchValue))
        {
            trigger = Phase8MapEventTriggerKinds.Action;
            switchId = null;
            switchValue = true;
        }

        var previous = _binding;
        _binding = true;
        try
        {
            SelectTrigger(trigger);
            _switchId.Text = switchId ?? string.Empty;
            _switchActive.Checked = switchId is null || switchValue;
            _switchActive.Enabled = _editorEnabled && !string.IsNullOrWhiteSpace(switchId);
        }
        finally
        {
            _binding = previous;
        }
    }

    private void SelectTrigger(string kind)
    {
        for (var i = 0; i < _trigger.Items.Count; i++)
        {
            if (_trigger.Items[i] is TriggerChoice choice && choice.Kind == kind)
            {
                _trigger.SelectedIndex = i;
                return;
            }
        }

        if (_trigger.Items.Count > 0)
        {
            _trigger.SelectedIndex = 0;
        }
    }

    private string SelectedTriggerKind() =>
        _trigger.SelectedItem is TriggerChoice choice
            ? choice.Kind
            : Phase8MapEventTriggerKinds.Action;

    private int? CurrentAlias() => _alias.Value > 0 ? (int)_alias.Value : null;

    private string? CurrentSwitchId() =>
        string.IsNullOrWhiteSpace(_switchId.Text) ? null : _switchId.Text.Trim();

    private ContentPublishStatus? SelectedStatusFilter() => _statusFilter.SelectedIndex switch
    {
        1 => ContentPublishStatus.Draft,
        2 => ContentPublishStatus.Published,
        _ => null,
    };

    private void MarkDirtyFromHeader()
    {
        if (_binding || !_editorEnabled)
        {
            return;
        }

        MarkDirty();
    }

    private void MarkDirty(bool clearValidation = true)
    {
        if (_currentId == Guid.Empty)
        {
            return;
        }

        if (clearValidation)
        {
            _validation.Text = string.Empty;
        }

        _dirty = true;
        UpdateMeta();
    }

    private void UpdateMeta()
    {
        if (_currentId == Guid.Empty)
        {
            _meta.Text = "Aucune sélection";
            return;
        }

        if (_currentRevision == 0)
        {
            _meta.Text = _dirty ? "Nouveau brouillon (non enregistré)" : "Nouveau brouillon";
            return;
        }

        var dirty = _dirty ? " · modifié" : string.Empty;
        _meta.Text =
            $"Id {_currentId:D} · rév. {_currentRevision} · {StatusLabel(_currentStatus)}"
            + (_publishedRevision is long published ? $" · publié rév. {published}" : string.Empty)
            + dirty;
    }

    private void ClearEditor()
    {
        _binding = true;
        try
        {
            _currentId = Guid.Empty;
            _currentRevision = 0;
            _currentStatus = ContentPublishStatus.Draft;
            _publishedRevision = null;
            _dirty = false;
            _name.Clear();
            _alias.Value = 0;
            _switchId.Clear();
            _switchActive.Checked = true;
            SelectTrigger(Phase8MapEventTriggerKinds.Action);
            _editor.LoadPayload(Phase8ContentPostgreSqlService.Serialize(new CommonEventDefinition()));
            _validation.Text = string.Empty;
            SetEditorEnabled(false);
            UpdateMeta();
        }
        finally
        {
            _binding = false;
        }
    }

    private void SetEditorEnabled(bool enabled)
    {
        _editorEnabled = enabled;
        _name.Enabled = enabled;
        _alias.Enabled = enabled;
        _trigger.Enabled = enabled;
        _switchId.Enabled = enabled;
        _switchActive.Enabled = enabled && !string.IsNullOrWhiteSpace(_switchId.Text);
        _editor.Enabled = enabled;
    }

    private void ReselectCurrent()
    {
        _suppressList = true;
        try
        {
            _list.ClearSelected();
            foreach (CommonEventRow row in _list.Items)
            {
                if (row.Id == _currentId)
                {
                    _list.SelectedItem = row;
                    break;
                }
            }
        }
        finally
        {
            _suppressList = false;
        }
    }

    private bool ConfirmDiscardIfDirty() =>
        GameDataListNavigation.ConfirmDiscardUnsavedChanges(this, "Événements communs", _dirty);

    private void OnFormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_allowCloseAfterCleanup)
        {
            return;
        }

        if (_dirty && !ConfirmDiscardIfDirty())
        {
            e.Cancel = true;
            return;
        }

        e.Cancel = true;
        if (_cleanupRunning)
        {
            return;
        }

        _cleanupRunning = true;
        SetWriteEnabled(false);
        _ = FinishCloseAsync();
    }

    private async Task FinishCloseAsync()
    {
        _lifecycle.BeginClosing();
        var timeout = EditorTestHooks.GameDataCloseCleanupTimeoutForTest ?? TimeSpan.FromSeconds(30);
        var drained = await _lifecycle.DrainAsync(timeout).ConfigureAwait(true);
        if (!drained)
        {
            _closeCleanupFailed = true;
            _cleanupRunning = false;
            SetWriteEnabled(_service.Capabilities.AllowsSave);
            return;
        }

        _allowCloseAfterCleanup = true;
        if (!IsDisposed)
        {
            BeginInvoke(new Action(() =>
            {
                if (!IsDisposed)
                {
                    Close();
                }
            }));
        }
    }

    private void SetWriteEnabled(bool enabled)
    {
        _btnNew.Enabled = enabled;
        _btnDuplicate.Enabled = enabled;
        _btnDelete.Enabled = enabled;
        _btnSave.Enabled = enabled;
        _btnPublish.Enabled = enabled;
        _list.Enabled = enabled;
        _filter.Enabled = enabled;
    }

    private static string StatusLabel(ContentPublishStatus status) => status switch
    {
        ContentPublishStatus.Published => "Publié",
        _ => "Brouillon",
    };

    private static Label Label(string text) => new()
    {
        Text = text,
        AutoSize = true,
        Margin = new Padding(8, 8, 4, 0),
    };

    private sealed record TriggerChoice(string Kind, string Label)
    {
        public override string ToString() => Label;
    }

    private sealed class CommonEventRow
    {
        public CommonEventRow(Guid id, string name, int? alias, ContentPublishStatus status, string line)
        {
            Id = id;
            Name = name;
            Alias = alias;
            Status = status;
            Line = line;
        }

        public Guid Id { get; }

        public string Name { get; }

        public int? Alias { get; }

        public ContentPublishStatus Status { get; }

        public string Line { get; }

        public override string ToString() => Line;
    }
}
