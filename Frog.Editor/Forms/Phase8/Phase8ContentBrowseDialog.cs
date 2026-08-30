using Frog.Application.Content;
using Frog.Editor.Forms.GameData;
using Frog.Editor.Services;

namespace Frog.Editor.Forms.Phase8;

/// <summary>Parcours / enregistrement / publication du catalogue Phase 8 (PostgreSQL).</summary>
internal sealed class Phase8ContentBrowseDialog : Form
{
    private readonly Phase8ContentPostgreSqlService _service;
    private readonly GameDataPanelLifecycle _lifecycle = new();
    private readonly ComboBox _cbKind = new() { DropDownStyle = ComboBoxStyle.DropDownList, Width = 220 };
    private readonly TextBox _txtFilter = new() { Width = 200, PlaceholderText = "Filtrer…" };
    private readonly ListView _lvItems = new()
    {
        View = View.Details,
        FullRowSelect = true,
        Dock = DockStyle.Fill,
    };
    private readonly TextBox _txtName = new() { Width = 280 };
    private readonly NumericUpDown _numAlias = new() { Minimum = 0, Maximum = int.MaxValue, Width = 100 };
    private readonly Label _lblMeta = new() { AutoSize = true };
    private readonly Label _lblValidation = new() { AutoSize = true, ForeColor = Color.Firebrick };
    private readonly Panel _editorHost = new() { Dock = DockStyle.Fill, Padding = new Padding(4) };
    private readonly Button _btnReload = new() { Text = "Charger", AutoSize = true };
    private readonly Button _btnNew = new() { Text = "Nouveau", AutoSize = true };
    private readonly Button _btnDuplicate = new() { Text = "Dupliquer", AutoSize = true };
    private readonly Button _btnDelete = new() { Text = "Supprimer", AutoSize = true };
    private readonly Button _btnSave = new() { Text = "Enregistrer brouillon", AutoSize = true };
    private readonly Button _btnPublish = new() { Text = "Publier", AutoSize = true };

    private readonly Dictionary<Phase8ContentKind, Phase8EditorPanelBase> _editors = new();
    private readonly List<Phase8ContentListRow> _rows = new();

    private Guid _currentId = Guid.Empty;
    private long _currentRevision;
    private ContentPublishStatus _currentStatus = ContentPublishStatus.Draft;
    private long? _publishedRevision;
    private bool _dirty;
    private bool _suppressList;
    private Phase8EditorPanelBase? _activeEditor;
    private Phase8ContentKind _committedKind = Phase8ContentKind.Dialogue;
    private bool _suppressKindChange;
    private bool _allowCloseAfterCleanup;
    private bool _cleanupRunning;
    private bool _closeCleanupFailed;
    private Exception? _closeCleanupException;

    public Phase8ContentBrowseDialog(Phase8ContentPostgreSqlService service)
    {
        _service = service ?? throw new ArgumentNullException(nameof(service));
        Text = "Contenu Phase 8 (PostgreSQL)";
        FormBorderStyle = FormBorderStyle.Sizable;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;
        ShowInTaskbar = false;
        ClientSize = new Size(980, 640);

        _lvItems.Columns.Add("id", 220);
        _lvItems.Columns.Add("name", 180);
        _lvItems.Columns.Add("alias", 50);
        _lvItems.Columns.Add("revision", 60);
        _lvItems.Columns.Add("status", 80);

        foreach (Phase8ContentKind kind in Enum.GetValues<Phase8ContentKind>())
        {
            _cbKind.Items.Add(new KindChoice(kind, FormatKindLabel(kind)));
        }

        _cbKind.SelectedIndex = 0;
        RegisterEditors();

        var top = new FlowLayoutPanel
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            Padding = new Padding(8),
            WrapContents = true,
        };
        top.Controls.Add(new Label { Text = "Type", AutoSize = true, Margin = new Padding(0, 8, 4, 0) });
        top.Controls.Add(_cbKind);
        top.Controls.Add(new Label { Text = "Filtre", AutoSize = true, Margin = new Padding(12, 8, 4, 0) });
        top.Controls.Add(_txtFilter);
        top.Controls.Add(_btnReload);
        top.Controls.Add(_btnNew);
        top.Controls.Add(_btnDuplicate);
        top.Controls.Add(_btnDelete);

        var split = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Vertical,
            SplitterDistance = 320,
        };
        split.Panel1.Controls.Add(_lvItems);

        var right = new TableLayoutPanel { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 4 };
        right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        right.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        right.RowStyles.Add(new RowStyle(SizeType.Percent, 100));

        var metaRow = new FlowLayoutPanel { Dock = DockStyle.Fill, AutoSize = true, WrapContents = true, Padding = new Padding(4) };
        metaRow.Controls.Add(new Label { Text = "Nom", AutoSize = true, Margin = new Padding(0, 8, 4, 0) });
        metaRow.Controls.Add(_txtName);
        metaRow.Controls.Add(new Label { Text = "Alias éditeur", AutoSize = true, Margin = new Padding(12, 8, 4, 0) });
        metaRow.Controls.Add(_numAlias);
        right.Controls.Add(metaRow, 0, 0);
        right.Controls.Add(_lblMeta, 0, 1);
        right.Controls.Add(_lblValidation, 0, 2);
        right.Controls.Add(_editorHost, 0, 3);
        split.Panel2.Controls.Add(right);

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

        Controls.Add(split);
        Controls.Add(top);
        Controls.Add(bottom);
        CancelButton = btnClose;

        var canWrite = _service.Capabilities.AllowsSave;
        _btnSave.Enabled = canWrite;
        _btnPublish.Enabled = canWrite;
        _btnNew.Enabled = canWrite;
        _btnDuplicate.Enabled = canWrite;
        _btnDelete.Enabled = canWrite;

        _cbKind.SelectedIndexChanged += (_, _) => _ = _lifecycle.RunAsync(async ct =>
        {
            if (_suppressKindChange)
            {
                return;
            }

            if (!ConfirmDiscardIfDirty())
            {
                RevertKindSelection();
                return;
            }

            _committedKind = SelectedKind;
            SwapEditorPanel(_committedKind);
            await ReloadListAsync(ct).ConfigureAwait(true);
            ClearEditorSelection();
        }, "kind");

        _txtFilter.TextChanged += (_, _) => RefreshFilteredList();
        _lvItems.SelectedIndexChanged += (_, _) => _ = _lifecycle.RunAsync(async ct =>
        {
            if (_suppressList || _lvItems.SelectedItems.Count != 1)
            {
                return;
            }

            if (!Guid.TryParse(_lvItems.SelectedItems[0].Text, out var id))
            {
                return;
            }

            if (!ConfirmDiscardIfDirty())
            {
                RevertListSelection();
                return;
            }

            await LoadDraftAsync(id, ct).ConfigureAwait(true);
        }, "select");

        _txtName.TextChanged += (_, _) =>
        {
            if (_activeEditor is not null)
            {
                _activeEditor.CatalogName = _txtName.Text;
            }

            MarkDirty();
        };
        _numAlias.ValueChanged += (_, _) => MarkDirty();
        _btnReload.Click += (_, _) => _ = _lifecycle.RunAsync(ReloadListAsync, "reload");
        _btnNew.Click += (_, _) => _ = _lifecycle.RunAsync(NewDraftAsync, "new");
        _btnDuplicate.Click += (_, _) => _ = _lifecycle.RunAsync(DuplicateDraftAsync, "duplicate");
        _btnDelete.Click += (_, _) => _ = _lifecycle.TrackAsync(DeleteAsync, "delete");
        _btnSave.Click += (_, _) => _ = _lifecycle.TrackAsync(ct => SaveAsync(SaveContentIntent.SaveDraft, ct), "save");
        _btnPublish.Click += (_, _) => _ = _lifecycle.TrackAsync(ct => SaveAsync(SaveContentIntent.Publish, ct), "publish");

        FormClosing += Phase8ContentBrowseDialog_FormClosing;
        SwapEditorPanel(SelectedKind);
        Shown += (_, _) => _ = _lifecycle.RunAsync(async ct =>
        {
            if (_activeEditor is null)
            {
                SwapEditorPanel(SelectedKind);
            }

            await ReloadListAsync(ct).ConfigureAwait(true);
        }, "init");
    }

    internal GameDataPanelLifecycle LifecycleForTest => _lifecycle;

    internal bool IsDirtyForTest => _dirty;

    internal TextBox NameForTest => _txtName;

    internal ComboBox KindComboForTest => _cbKind;

    internal ListView ItemsForTest => _lvItems;

    internal Button BtnNewForTest => _btnNew;

    internal Button BtnSaveForTest => _btnSave;

    internal Button BtnPublishForTest => _btnPublish;

    internal Button BtnDeleteForTest => _btnDelete;

    internal Button BtnDuplicateForTest => _btnDuplicate;

    internal Button BtnReloadForTest => _btnReload;

    internal Phase8EditorPanelBase? ActiveEditorForTest => _activeEditor;

    internal Guid CurrentIdForTest => _currentId;

    internal long CurrentRevisionForTest => _currentRevision;

    internal ContentPublishStatus CurrentStatusForTest => _currentStatus;

    internal Exception? CloseCleanupExceptionForTest => _closeCleanupException;

    internal bool CloseCleanupFailedForTest => _closeCleanupFailed;

    internal bool AllowFinalCloseForTest => _allowCloseAfterCleanup;

    internal void RetryCloseCleanupForTest()
    {
        if (_allowCloseAfterCleanup || _cleanupRunning || IsDisposed)
        {
            return;
        }

        _cleanupRunning = true;
        SetClosingUiState(enabled: false);
        _ = RunAsyncCloseCleanupAndMaybeFinishAsync();
    }

    private Phase8ContentKind SelectedKind =>
        _cbKind.SelectedItem is KindChoice choice ? choice.Kind : Phase8ContentKind.Dialogue;

    private void RegisterEditors()
    {
        _editors[Phase8ContentKind.Dialogue] = new Phase8DialogueEditorPanel();
        _editors[Phase8ContentKind.Quest] = new Phase8QuestEditorPanel();
        _editors[Phase8ContentKind.Recipe] = new Phase8RecipeEditorPanel();
        _editors[Phase8ContentKind.Region] = new Phase8RegionEditorPanel();
        _editors[Phase8ContentKind.CommonEvent] = new Phase8CommonEventEditorPanel();
        _editors[Phase8ContentKind.Profession] = new Phase8ProfessionEditorPanel();
        _editors[Phase8ContentKind.WeatherProfile] = new Phase8WeatherEditorPanel();

        foreach (var editor in _editors.Values)
        {
            editor.ContentChanged += OnEditorContentChanged;
        }
    }

    private void SwapEditorPanel(Phase8ContentKind kind)
    {
        _editorHost.Controls.Clear();
        _activeEditor = _editors[kind];
        _activeEditor.Dock = DockStyle.Fill;
        _editorHost.Controls.Add(_activeEditor);
    }

    private void OnEditorContentChanged() => MarkDirty();

    private void MarkDirty()
    {
        _dirty = true;
        _lblValidation.Text = string.Empty;
        UpdateMetaLabel();
    }

    private void UpdateMetaLabel()
    {
        if (_currentId == Guid.Empty)
        {
            _lblMeta.Text = _dirty ? "Nouveau brouillon (non enregistré)" : "Aucune sélection";
            return;
        }

        var dirty = _dirty ? " · modifié" : string.Empty;
        _lblMeta.Text =
            $"Id {_currentId:D} · rév. {_currentRevision} · {_currentStatus}"
            + (_publishedRevision is long pub ? $" · publié rév. {pub}" : string.Empty)
            + dirty;
    }

    private async Task ReloadListAsync(CancellationToken ct)
    {
        _rows.Clear();
        var items = await _service.ListAsync(SelectedKind, ct).ConfigureAwait(true);
        if (ct.IsCancellationRequested)
        {
            return;
        }

        foreach (var row in items)
        {
            _rows.Add(row);
        }

        RefreshFilteredList();
    }

    private void RefreshFilteredList()
    {
        var filter = _txtFilter.Text.Trim();
        var comparison = StringComparison.OrdinalIgnoreCase;
        _lvItems.BeginUpdate();
        try
        {
            _lvItems.Items.Clear();
            foreach (var row in _rows)
            {
                if (filter.Length > 0)
                {
                    var blob = $"{row.Id} {row.Name} {row.EditorAliasId} {row.Status}";
                    if (!blob.Contains(filter, comparison))
                    {
                        continue;
                    }
                }

                var item = new ListViewItem(row.Id.ToString("D"));
                item.SubItems.Add(row.Name);
                item.SubItems.Add(row.EditorAliasId?.ToString() ?? string.Empty);
                item.SubItems.Add(row.Revision.ToString());
                item.SubItems.Add(row.Status.ToString());
                _lvItems.Items.Add(item);
            }
        }
        finally
        {
            _lvItems.EndUpdate();
        }
    }

    private async Task LoadDraftAsync(Guid id, CancellationToken ct)
    {
        var stored = await _service.LoadDraftAsync(id, ct).ConfigureAwait(true);
        if (ct.IsCancellationRequested || stored is null)
        {
            return;
        }

        BindStored(stored);
    }

    private void BindStored(Phase8StoredContent stored)
    {
        _currentId = stored.Id;
        _currentRevision = stored.Revision;
        _currentStatus = stored.Status;
        _publishedRevision = stored.PublishedRevision;
        _dirty = false;
        _txtName.Text = stored.Name;
        _numAlias.Value = Math.Max(0, stored.EditorAliasId ?? 0);
        _activeEditor?.LoadPayload(stored.PayloadJson);
        if (_activeEditor is not null)
        {
            _activeEditor.ContentId = stored.Id;
            _activeEditor.CatalogName = stored.Name;
        }

        _lblValidation.Text = string.Empty;
        UpdateMetaLabel();
    }

    private async Task NewDraftAsync(CancellationToken ct)
    {
        if (!ConfirmDiscardIfDirty())
        {
            return;
        }

        var newId = Guid.NewGuid();
        _currentId = newId;
        _currentRevision = 0;
        _currentStatus = ContentPublishStatus.Draft;
        _publishedRevision = null;
        _dirty = true;
        if (_activeEditor is null)
        {
            SwapEditorPanel(SelectedKind);
        }

        _txtName.Text = DefaultNameForKind(SelectedKind);
        _numAlias.Value = 0;
        _activeEditor?.ResetForNew(newId);
        if (_activeEditor is not null)
        {
            _activeEditor.CatalogName = _txtName.Text;
        }
        _suppressList = true;
        _lvItems.SelectedItems.Clear();
        _suppressList = false;
        _lblValidation.Text = string.Empty;
        UpdateMetaLabel();
        await Task.CompletedTask.ConfigureAwait(true);
    }

    private async Task DuplicateDraftAsync(CancellationToken ct)
    {
        if (_activeEditor is null || _currentId == Guid.Empty)
        {
            _lblValidation.Text = "Sélectionnez un contenu à dupliquer.";
            return;
        }

        if (!_activeEditor.TryBuildPayload(out var payloadJson, out var error))
        {
            _lblValidation.Text = error ?? "Payload invalide.";
            return;
        }

        var newId = Guid.NewGuid();
        var copyName = "Copie de " + (_txtName.Text.Trim().Length > 0 ? _txtName.Text.Trim() : DefaultNameForKind(SelectedKind));
        if (!Phase8ContentPostgreSqlService.TryRewritePayloadIdentity(
                SelectedKind,
                payloadJson,
                newId,
                copyName,
                out var rewritten,
                out error))
        {
            _lblValidation.Text = error ?? "Duplication impossible.";
            return;
        }

        _currentId = newId;
        _currentRevision = 0;
        _currentStatus = ContentPublishStatus.Draft;
        _publishedRevision = null;
        _dirty = true;
        _txtName.Text = copyName;
        _numAlias.Value = 0;
        _activeEditor.LoadPayload(rewritten);
        _activeEditor.ContentId = newId;
        _activeEditor.CatalogName = copyName;
        _suppressList = true;
        _lvItems.SelectedItems.Clear();
        _suppressList = false;
        _lblValidation.Text = string.Empty;
        UpdateMetaLabel();
        await Task.CompletedTask.ConfigureAwait(true);
    }

    private async Task DeleteAsync(CancellationToken ct)
    {
        if (_currentId == Guid.Empty || _currentRevision == 0)
        {
            _lblValidation.Text = "Rien à supprimer (brouillon non enregistré).";
            return;
        }

        if (GameDataUiMessageBox.Show(
                this,
                "Supprimer définitivement ce contenu ?",
                "Phase 8",
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
                ClearEditorSelection();
                await ReloadListAsync(ct).ConfigureAwait(true);
                GameDataUiMessageBox.Show(this, "Contenu supprimé.", "Phase 8");
                break;
            case Phase8DeleteContentResult.NotFound:
                _lblValidation.Text = "Contenu introuvable.";
                break;
            case Phase8DeleteContentResult.PersistenceFailed failed:
                _lblValidation.Text = failed.Error;
                break;
        }
    }

    private void ClearEditorSelection()
    {
        _currentId = Guid.Empty;
        _currentRevision = 0;
        _currentStatus = ContentPublishStatus.Draft;
        _publishedRevision = null;
        _dirty = false;
        _txtName.Clear();
        _numAlias.Value = 0;
        _lblValidation.Text = string.Empty;
        UpdateMetaLabel();
    }

    private async Task SaveAsync(SaveContentIntent intent, CancellationToken ct)
    {
        if (_activeEditor is null)
        {
            return;
        }

        var name = _txtName.Text.Trim();
        if (string.IsNullOrWhiteSpace(name))
        {
            _lblValidation.Text = "Nom requis.";
            return;
        }

        _activeEditor.CatalogName = name;
        if (!_activeEditor.TryBuildPayload(out var payloadJson, out var error))
        {
            _lblValidation.Text = error ?? "Payload invalide.";
            return;
        }

        if (!Phase8ContentPostgreSqlService.TryValidatePayload(SelectedKind, payloadJson, out error))
        {
            _lblValidation.Text = error ?? "Validation échouée.";
            return;
        }

        int? alias = _numAlias.Value > 0 ? (int)_numAlias.Value : null;
        var request = new Phase8SaveContentRequest
        {
            ContentId = _currentRevision > 0 ? _currentId : null,
            NewId = _currentRevision == 0 ? _currentId : null,
            Kind = SelectedKind,
            Name = name,
            EditorAliasId = alias,
            PayloadJson = payloadJson,
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
                _dirty = false;
                _lblValidation.Text = string.Empty;
                UpdateMetaLabel();
                await ReloadListAsync(ct).ConfigureAwait(true);
                SelectListItem(success.ContentId);
                GameDataUiMessageBox.Show(
                    this,
                    intent == SaveContentIntent.Publish ? "Contenu publié." : "Brouillon enregistré.",
                    "Phase 8");
                break;
            case Phase8SaveContentResult.Conflict conflict:
                _lblValidation.Text = $"Conflit de révision (courante={conflict.CurrentRevision}). Rechargez l'entrée.";
                break;
            case Phase8SaveContentResult.ValidationFailed failed:
                _lblValidation.Text = failed.Error;
                break;
            case Phase8SaveContentResult.PersistenceFailed failed:
                _lblValidation.Text = failed.Error;
                break;
        }
    }

    private void SelectListItem(Guid id)
    {
        _suppressList = true;
        foreach (ListViewItem item in _lvItems.Items)
        {
            if (item.Text == id.ToString("D"))
            {
                item.Selected = true;
                item.Focused = true;
                break;
            }
        }

        _suppressList = false;
    }

    private bool ConfirmDiscardIfDirty()
    {
        if (!_dirty)
        {
            return true;
        }

        return GameDataUiMessageBox.Show(
            this,
            "Modifications non enregistrées. Continuer ?",
            "Phase 8",
            MessageBoxButtons.YesNo,
            MessageBoxIcon.Question) == DialogResult.Yes;
    }

    private void RevertListSelection()
    {
        if (_currentId == Guid.Empty)
        {
            _suppressList = true;
            _lvItems.SelectedItems.Clear();
            _suppressList = false;
            return;
        }

        SelectListItem(_currentId);
    }

    private void RevertKindSelection()
    {
        _suppressKindChange = true;
        try
        {
            for (var i = 0; i < _cbKind.Items.Count; i++)
            {
                if (_cbKind.Items[i] is KindChoice choice && choice.Kind == _committedKind)
                {
                    _cbKind.SelectedIndex = i;
                    break;
                }
            }
        }
        finally
        {
            _suppressKindChange = false;
        }
    }

    private void Phase8ContentBrowseDialog_FormClosing(object? sender, FormClosingEventArgs e)
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
        SetClosingUiState(enabled: false);
        _ = RunAsyncCloseCleanupAndMaybeFinishAsync();
    }

    private async Task RunAsyncCloseCleanupAndMaybeFinishAsync()
    {
        var timeout = EditorTestHooks.GameDataCloseCleanupTimeoutForTest ?? TimeSpan.FromSeconds(30);
        try
        {
            var success = await RunCloseCleanupAsync(timeout).ConfigureAwait(true);
            if (!success)
            {
                _closeCleanupFailed = true;
                _cleanupRunning = false;
                SetClosingUiState(enabled: true);
                if (!IsDisposed)
                {
                    BeginInvoke(new Action(() =>
                    {
                        if (IsDisposed)
                        {
                            return;
                        }

                        GameDataUiMessageBox.Show(
                            this,
                            "La fermeture a expiré : une opération Phase 8 est encore en cours. "
                            + "Attendez la fin de l’opération puis réessayez de fermer.",
                            "Phase 8",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                    }));
                }

                return;
            }

            _lifecycle.Dispose();
            _closeCleanupFailed = false;
            _allowCloseAfterCleanup = true;
            _cleanupRunning = false;
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
        catch (Exception ex)
        {
            _closeCleanupException = ex;
            _closeCleanupFailed = true;
            _cleanupRunning = false;
            SetClosingUiState(enabled: true);
            if (!IsDisposed)
            {
                var message = $"Échec du nettoyage à la fermeture : {ex.Message}";
                BeginInvoke(new Action(() =>
                {
                    if (IsDisposed)
                    {
                        return;
                    }

                    GameDataUiMessageBox.Show(
                        this,
                        message,
                        "Phase 8",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }));
            }
        }
    }

    private async Task<bool> RunCloseCleanupAsync(TimeSpan timeout)
    {
        _lifecycle.BeginClosing();

        var drained = await _lifecycle.DrainAsync(timeout).ConfigureAwait(true);
        if (!drained || !_lifecycle.IsIdle)
        {
            return false;
        }

        return true;
    }

    private void SetClosingUiState(bool enabled)
    {
        _cbKind.Enabled = enabled;
        _txtFilter.Enabled = enabled;
        _lvItems.Enabled = enabled;
        _txtName.Enabled = enabled;
        _numAlias.Enabled = enabled;
        _btnReload.Enabled = enabled;
        _btnNew.Enabled = enabled;
        _btnDuplicate.Enabled = enabled;
        _btnDelete.Enabled = enabled;
        _btnSave.Enabled = enabled;
        _btnPublish.Enabled = enabled;
        if (_activeEditor is not null)
        {
            _activeEditor.Enabled = enabled;
        }
    }

    private static string FormatKindLabel(Phase8ContentKind kind) => kind switch
    {
        Phase8ContentKind.Dialogue => "Dialogue",
        Phase8ContentKind.Quest => "Quête",
        Phase8ContentKind.CommonEvent => "Événement commun",
        Phase8ContentKind.Profession => "Métier",
        Phase8ContentKind.Recipe => "Recette",
        Phase8ContentKind.Region => "Région",
        Phase8ContentKind.WeatherProfile => "Profil météo",
        _ => kind.ToString(),
    };

    private static string DefaultNameForKind(Phase8ContentKind kind) => kind switch
    {
        Phase8ContentKind.Dialogue => "Nouveau dialogue",
        Phase8ContentKind.Quest => "Nouvelle quête",
        Phase8ContentKind.CommonEvent => "Nouvel événement commun",
        Phase8ContentKind.Profession => "Nouveau métier",
        Phase8ContentKind.Recipe => "Nouvelle recette",
        Phase8ContentKind.Region => "Nouvelle région",
        Phase8ContentKind.WeatherProfile => "Nouveau profil météo",
        _ => "Nouveau contenu",
    };

    private sealed record KindChoice(Phase8ContentKind Kind, string Label)
    {
        public override string ToString() => Label;
    }
}
