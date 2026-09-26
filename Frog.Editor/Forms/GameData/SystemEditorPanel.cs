using System.IO;
using Frog.Application.Content;
using Frog.Core.Enums;
using Frog.Core.Models;
using Frog.Editor.Services;

namespace Frog.Editor.Forms.GameData;

/// <summary>
/// Fiche Système (base VX) : options du projet, interrupteurs globaux et variables globales.
/// </summary>
public sealed class SystemEditorPanel : UserControl
{
    private readonly GameDataPanelLifecycle _lifecycle = new();
    private readonly GameSystemWorkspaceSession _flags;
    private readonly GameSystemWorkspaceSession _options;
    private readonly ContentRepositoryCapabilities _capabilities;
    private readonly Dictionary<GameSystemEntryKind, HashSet<string>> _knownKeys = new()
    {
        [GameSystemEntryKind.Switch] = new HashSet<string>(StringComparer.Ordinal),
        [GameSystemEntryKind.Variable] = new HashSet<string>(StringComparer.Ordinal),
    };
    private readonly ListBox _list = new() { Dock = DockStyle.Fill };
    private readonly TextBox _search = new() { Dock = DockStyle.Top, PlaceholderText = "Rechercher…" };
    private readonly ComboBox _statusFilter = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _kindFilter = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _gameTitle = new() { Width = 280 };
    private readonly TextBox _bgmPath = new() { Width = 280 };
    private readonly NumericUpDown _bgmVolume = new()
    {
        Minimum = MapAudioTrack.MinVolume,
        Maximum = MapAudioTrack.MaxVolume,
        Value = MapAudioTrack.DefaultVolume,
        Width = 80,
    };
    private readonly NumericUpDown _bgmFade = new()
    {
        Minimum = MapAudioTrack.MinFadeMs,
        Maximum = MapAudioTrack.MaxFadeMs,
        Width = 80,
    };
    private readonly Button _btnBrowseBgm = new() { Text = "Parcourir…", AutoSize = true };
    private readonly Button _btnClearBgm = new() { Text = "Effacer", AutoSize = true };
    private readonly Button _btnSaveOptions = new() { Text = "Enregistrer les options", AutoSize = true };
    private readonly Button _btnPublishOptions = new() { Text = "Publier les options", AutoSize = true };
    private readonly Label _optionsMeta = new() { AutoSize = true };
    private readonly Label _optionsValidation = new() { AutoSize = true, ForeColor = Color.Firebrick };
    private readonly TextBox _key = new() { Width = 280 };
    private readonly TextBox _label = new() { Width = 280 };
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
    private bool _suppressList;
    private bool _suppressKind;
    private bool _binding;

    public event Action<string>? StatusChanged;

    public bool IsDirty => _flags.IsDirty || _options.IsDirty;

    internal long CurrentRevisionForTest => _flags.CurrentRevision;

    internal long? PublishedRevisionForTest => _flags.PublishedRevision;

    internal ContentPublishStatus CurrentStatusForTest => _flags.CurrentStatus;

    internal GameDataPanelLifecycle LifecycleForTest => _lifecycle;

    internal Task<bool> DrainAsync(TimeSpan? timeout = null) => _lifecycle.DrainAsync(timeout ?? TimeSpan.FromSeconds(30));

    internal void BeginClosing() => _lifecycle.BeginClosing();

    internal void DisposeLifecycle() => _lifecycle.Dispose();

    internal Button BtnNewForTest => _btnNew;

    internal Button BtnDupForTest => _btnDup;

    internal Button BtnSaveForTest => _btnSave;

    internal Button BtnPublishForTest => _btnPublish;

    internal Button BtnDeleteForTest => _btnDelete;

    internal Button BtnSaveOptionsForTest => _btnSaveOptions;

    internal Button BtnPublishOptionsForTest => _btnPublishOptions;

    internal Button BtnBrowseBgmForTest => _btnBrowseBgm;

    internal Button BtnClearBgmForTest => _btnClearBgm;

    internal TextBox NameForTest => _label;

    internal TextBox KeyForTest => _key;

    internal TextBox NoteForTest => _note;

    internal TextBox GameTitleForTest => _gameTitle;

    internal TextBox StartingBgmForTest => _bgmPath;

    internal NumericUpDown BgmVolumeForTest => _bgmVolume;

    internal NumericUpDown BgmFadeForTest => _bgmFade;

    internal TextBox SearchForTest => _search;

    internal ComboBox StatusFilterForTest => _statusFilter;

    internal ComboBox KindFilterForTest => _kindFilter;

    internal ListBox ListForTest => _list;

    internal Label ValidationForTest => _validation;

    internal Label OptionsValidationForTest => _optionsValidation;

    internal bool OptionsDirtyForTest => _options.IsDirty;

    internal long? OptionsPublishedRevisionForTest => _options.PublishedRevision;

    public SystemEditorPanel(
        GameSystemWorkspaceSession flags,
        GameSystemWorkspaceSession options,
        ContentRepositoryCapabilities capabilities)
    {
        _flags = flags;
        _options = options;
        _capabilities = capabilities;
        _flags.KindFilter = GameSystemEntryKind.Switch;
        _options.KindFilter = GameSystemEntryKind.Options;

        _statusFilter.Items.AddRange(new object[] { "Tous", "Brouillon", "Publié" });
        _statusFilter.SelectedIndex = 0;
        _kindFilter.Items.AddRange(new object[] { "Interrupteurs", "Variables" });
        _kindFilter.SelectedIndex = 0;

        var left = new Panel { Dock = DockStyle.Left, Width = 260, Padding = new Padding(4) };
        left.Controls.Add(_list);
        left.Controls.Add(_search);
        left.Controls.Add(_statusFilter);
        left.Controls.Add(_kindFilter);

        var optionsBox = new GroupBox
        {
            Text = "Options du projet",
            Dock = DockStyle.Top,
            Height = 176,
            Padding = new Padding(8),
        };
        var optionsForm = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            AutoScroll = true,
        };
        optionsForm.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        optionsForm.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        void OptionRow(string caption, Control control)
        {
            var row = optionsForm.RowCount++;
            optionsForm.Controls.Add(
                new Label { Text = caption, AutoSize = true, Anchor = AnchorStyles.Left },
                0,
                row);
            optionsForm.Controls.Add(control, 1, row);
        }

        var bgmLine = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
        };
        bgmLine.Controls.Add(_bgmPath);
        bgmLine.Controls.Add(_btnBrowseBgm);
        bgmLine.Controls.Add(_btnClearBgm);
        var audioLine = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
        };
        audioLine.Controls.Add(_bgmVolume);
        audioLine.Controls.Add(new Label { Text = "Fondu (ms)", AutoSize = true, Padding = new Padding(8, 6, 4, 0) });
        audioLine.Controls.Add(_bgmFade);
        var optionButtons = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = false,
            FlowDirection = FlowDirection.LeftToRight,
        };
        optionButtons.Controls.AddRange(new Control[] { _btnSaveOptions, _btnPublishOptions });

        OptionRow("Nom du jeu", _gameTitle);
        OptionRow("Musique de départ", bgmLine);
        OptionRow("Volume", audioLine);
        OptionRow("", optionButtons);
        OptionRow("État", _optionsMeta);
        OptionRow("", _optionsValidation);
        optionsBox.Controls.Add(optionsForm);

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

        Row("Identifiant", _key);
        Row("Libellé", _label);
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
        Controls.Add(optionsBox);
        Controls.Add(left);

        _search.TextChanged += (_, _) => _ = _lifecycle.RunAsync(async ct =>
        {
            _flags.SearchFilter = _search.Text;
            await RefreshListAsync(ct).ConfigureAwait(true);
        }, "refresh");
        _statusFilter.SelectedIndexChanged += (_, _) => _ = _lifecycle.RunAsync(async ct =>
        {
            _flags.StatusFilter = _statusFilter.SelectedIndex switch
            {
                1 => ContentPublishStatus.Draft,
                2 => ContentPublishStatus.Published,
                _ => null,
            };
            await RefreshListAsync(ct).ConfigureAwait(true);
        }, "refresh");
        _kindFilter.SelectedIndexChanged += (_, _) =>
        {
            if (_suppressKind)
            {
                return;
            }

            if (!GameDataListNavigation.ConfirmDiscardUnsavedChanges(this, "Système", _flags.IsDirty))
            {
                _suppressKind = true;
                _kindFilter.SelectedIndex = _flags.KindFilter == GameSystemEntryKind.Variable ? 1 : 0;
                _suppressKind = false;
                return;
            }

            _flags.ClearCurrent();
            _flags.KindFilter = SelectedKind();
            BindFlagForm();
            _ = _lifecycle.RunAsync(async ct => await RefreshListAsync(ct).ConfigureAwait(true), "refresh");
        };
        _list.SelectedIndexChanged += (_, _) => _ = _lifecycle.RunAsync(async _ =>
        {
            if (_suppressList || _list.SelectedItem is not CatalogItem item)
            {
                return;
            }

            if (!GameDataListNavigation.ConfirmDiscardUnsavedChanges(this, "Système", _flags.IsDirty))
            {
                GameDataListNavigation.RevertListSelection(
                    _list,
                    ref _suppressList,
                    _flags.CurrentId,
                    listItem => ((CatalogItem)listItem).Id);
                return;
            }

            await _flags.OpenAsync(item.Id).ConfigureAwait(true);
            BindFlagForm();
        }, "refresh");

        void MarkFlag()
        {
            if (_binding)
            {
                return;
            }

            ApplyFlagToSession();
            _flags.MarkDirty();
            LiveValidateFlag();
            StatusChanged?.Invoke("Modifié (non enregistré)");
        }

        void MarkOptions()
        {
            if (_binding)
            {
                return;
            }

            ApplyOptionsToSession();
            _options.MarkDirty();
            LiveValidateOptions();
            StatusChanged?.Invoke("Options modifiées (non enregistrées)");
        }

        _key.TextChanged += (_, _) => MarkFlag();
        _label.TextChanged += (_, _) => MarkFlag();
        _note.TextChanged += (_, _) => MarkFlag();
        _gameTitle.TextChanged += (_, _) => MarkOptions();
        _bgmPath.TextChanged += (_, _) => MarkOptions();
        _bgmVolume.ValueChanged += (_, _) => MarkOptions();
        _bgmFade.ValueChanged += (_, _) => MarkOptions();

        _btnNew.Click += (_, _) =>
        {
            var kind = SelectedKind();
            var key = GameSystemEntryDefinition.AllocateKey(kind, KnownKeys(kind));
            RememberKey(kind, key);
            _flags.AdoptNewDraft(new GameSystemEntryDefinition
            {
                Id = Guid.NewGuid(),
                Kind = kind,
                Key = key,
                Label = kind == GameSystemEntryKind.Variable ? "Nouvelle variable" : "Nouvel interrupteur",
            });
            BindFlagForm();
            StatusChanged?.Invoke("Nouveau brouillon");
        };
        _btnDup.Click += (_, _) =>
        {
            if (_flags.Current is null || _flags.Current.Kind == GameSystemEntryKind.Options)
            {
                return;
            }

            var kind = SelectedKind();
            var copy = _flags.DuplicateCurrent(KnownKeys(kind));
            RememberKey(kind, copy.Key);
            BindFlagForm();
            StatusChanged?.Invoke("Copie créée");
        };
        _btnSave.Click += (_, _) => _ = _lifecycle.TrackAsync(async _ => await SaveFlagAsync(SaveContentIntent.SaveDraft).ConfigureAwait(true), "save");
        _btnPublish.Click += (_, _) => _ = _lifecycle.TrackAsync(async _ => await SaveFlagAsync(SaveContentIntent.Publish).ConfigureAwait(true), "publish");
        _btnDelete.Click += (_, _) => _ = _lifecycle.RunAsync(async _ => await DeleteFlagAsync().ConfigureAwait(true), "delete");
        _btnSaveOptions.Click += (_, _) => _ = _lifecycle.TrackAsync(async _ => await SaveOptionsAsync(SaveContentIntent.SaveDraft).ConfigureAwait(true), "save");
        _btnPublishOptions.Click += (_, _) => _ = _lifecycle.TrackAsync(async _ => await SaveOptionsAsync(SaveContentIntent.Publish).ConfigureAwait(true), "publish");
        _btnBrowseBgm.Click += (_, _) => BrowseBgm();
        _btnClearBgm.Click += (_, _) =>
        {
            _bgmPath.Text = string.Empty;
            _bgmVolume.Value = MapAudioTrack.DefaultVolume;
            _bgmFade.Value = MapAudioTrack.MinFadeMs;
        };

        var canWrite = _capabilities.AllowsSave;
        _btnSave.Enabled = canWrite;
        _btnPublish.Enabled = canWrite;
        _btnDelete.Enabled = canWrite;
        _btnSaveOptions.Enabled = canWrite;
        _btnPublishOptions.Enabled = canWrite;
    }

    public async Task InitializeAsync()
    {
        await RefreshListAsync().ConfigureAwait(true);
        await EnsureOptionsAsync().ConfigureAwait(true);
        StatusChanged?.Invoke($"Backend système : {_capabilities.DisplayLabel}");
    }

    internal void QueueRefreshList()
        => _ = _lifecycle.RunAsync(async ct =>
        {
            await RefreshListAsync(ct).ConfigureAwait(true);
            if (!_options.IsDirty)
            {
                await EnsureOptionsAsync(ct).ConfigureAwait(true);
            }
        }, "refresh");

    private GameSystemEntryKind SelectedKind()
        => _kindFilter.SelectedIndex == 1 ? GameSystemEntryKind.Variable : GameSystemEntryKind.Switch;

    private IEnumerable<string> KnownKeys(GameSystemEntryKind kind)
    {
        if (_flags.Current?.Kind == kind && !string.IsNullOrEmpty(_flags.Current.Key))
        {
            return _knownKeys[kind].Append(_flags.Current.Key);
        }

        return _knownKeys[kind];
    }

    private void RememberKey(GameSystemEntryKind kind, string key)
    {
        if (kind is GameSystemEntryKind.Switch or GameSystemEntryKind.Variable && key.Length > 0)
        {
            _knownKeys[kind].Add(key);
        }
    }

    private void RememberCatalogKeys()
    {
        var kind = SelectedKind();
        var filtered = !string.IsNullOrWhiteSpace(_flags.SearchFilter) || _flags.StatusFilter is not null;
        if (!filtered)
        {
            _knownKeys[kind].Clear();
        }

        foreach (var entry in _flags.Catalog)
        {
            if (entry.Key.Length > 0)
            {
                _knownKeys[kind].Add(entry.Key);
            }
        }
    }

    private async Task RefreshListAsync(CancellationToken ct = default)
    {
        await _flags.RefreshCatalogAsync(ct).ConfigureAwait(true);
        if (ct.IsCancellationRequested)
        {
            return;
        }

        RememberCatalogKeys();
        _suppressList = true;
        _list.Items.Clear();
        var selected = -1;
        var index = 0;
        foreach (var entry in _flags.Catalog)
        {
            var key = string.IsNullOrEmpty(entry.Key) ? "—" : entry.Key;
            _list.Items.Add(new CatalogItem(
                entry.EntryId,
                $"{entry.Label} ({key}) [{entry.Status}]"));
            if (_flags.CurrentId == entry.EntryId)
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

    private async Task EnsureOptionsAsync(CancellationToken ct = default)
    {
        await _options.RefreshCatalogAsync(ct).ConfigureAwait(true);
        if (ct.IsCancellationRequested)
        {
            return;
        }

        if (_options.Catalog.Count > 0)
        {
            await _options.OpenAsync(_options.Catalog[0].EntryId, ct).ConfigureAwait(true);
        }
        else if (_options.CurrentId is null && !_options.IsDirty)
        {
            _options.AdoptNewDraft(new GameSystemEntryDefinition
            {
                Id = Guid.NewGuid(),
                Kind = GameSystemEntryKind.Options,
                Label = string.Empty,
                StartingBgmVolume = MapAudioTrack.DefaultVolume,
            });
            _options.ClearDirty();
        }

        BindOptionsForm();
    }

    private void BindFlagForm()
    {
        var definition = _flags.Current;
        _binding = true;
        try
        {
            _key.Text = definition?.Key ?? string.Empty;
            _label.Text = definition?.Label ?? string.Empty;
            _note.Text = definition?.Note ?? string.Empty;
            _meta.Text = definition is null
                ? "Aucune entrée sélectionnée."
                : $"Id={definition.Id:N}  rev={_flags.CurrentRevision}  statut={_flags.CurrentStatus}  publié={_flags.PublishedRevision?.ToString() ?? "—"}";
        }
        finally
        {
            _binding = false;
        }

        LiveValidateFlag();
    }

    private void BindOptionsForm()
    {
        var definition = _options.Current;
        _binding = true;
        try
        {
            _gameTitle.Text = definition?.Label ?? string.Empty;
            _bgmPath.Text = definition?.StartingBgmAsset ?? string.Empty;
            _bgmVolume.Value = Math.Clamp(
                definition?.StartingBgmVolume ?? MapAudioTrack.DefaultVolume,
                MapAudioTrack.MinVolume,
                MapAudioTrack.MaxVolume);
            _bgmFade.Value = Math.Clamp(
                definition?.StartingBgmFadeMs ?? 0,
                MapAudioTrack.MinFadeMs,
                MapAudioTrack.MaxFadeMs);
            _optionsMeta.Text = definition is null
                ? "Options non chargées."
                : $"rev={_options.CurrentRevision}  statut={_options.CurrentStatus}  publié={_options.PublishedRevision?.ToString() ?? "—"}";
        }
        finally
        {
            _binding = false;
        }

        LiveValidateOptions();
    }

    private void ApplyFlagToSession()
    {
        if (_flags.Current is null)
        {
            return;
        }

        _flags.Current.Kind = SelectedKind();
        _flags.Current.Key = _key.Text.Trim();
        _flags.Current.Label = _label.Text.Trim();
        _flags.Current.Note = string.IsNullOrWhiteSpace(_note.Text) ? null : _note.Text.Trim();
        _flags.Current.StartingBgmAsset = string.Empty;
        _flags.Current.StartingBgmVolume = MapAudioTrack.DefaultVolume;
        _flags.Current.StartingBgmFadeMs = 0;
    }

    private void ApplyOptionsToSession()
    {
        if (_options.Current is null)
        {
            return;
        }

        _options.Current.Kind = GameSystemEntryKind.Options;
        _options.Current.Key = string.Empty;
        _options.Current.Label = _gameTitle.Text.Trim();
        if (!MapAudioTrack.TryCreate(
                _bgmPath.Text,
                (int)_bgmVolume.Value,
                (int)_bgmFade.Value,
                "Musique de départ",
                out var track,
                out _))
        {
            _options.Current.StartingBgmAsset = _bgmPath.Text.Trim().Replace('\\', '/');
            _options.Current.StartingBgmVolume = (int)_bgmVolume.Value;
            _options.Current.StartingBgmFadeMs = (int)_bgmFade.Value;
            return;
        }

        _options.Current.StartingBgmAsset = track.Asset;
        _options.Current.StartingBgmVolume = track.Volume;
        _options.Current.StartingBgmFadeMs = track.FadeMs;
    }

    private void LiveValidateFlag()
    {
        if (_flags.Current is null)
        {
            _validation.Text = string.Empty;
            return;
        }

        ApplyFlagToSession();
        _validation.Text = _flags.Current.Validate(out var error) ? string.Empty : error ?? string.Empty;
    }

    private void LiveValidateOptions()
    {
        if (_options.Current is null)
        {
            _optionsValidation.Text = string.Empty;
            return;
        }

        ApplyOptionsToSession();
        if (!_options.IsDirty
            && _options.CurrentId is null
            && string.IsNullOrWhiteSpace(_options.Current.Label)
            && string.IsNullOrWhiteSpace(_options.Current.StartingBgmAsset))
        {
            _optionsValidation.Text = string.Empty;
            return;
        }

        _optionsValidation.Text = _options.Current.Validate(out var error) ? string.Empty : error ?? string.Empty;
    }

    private async Task SaveFlagAsync(SaveContentIntent intent)
    {
        ApplyFlagToSession();
        var result = await _flags.SaveCurrentAsync(intent).ConfigureAwait(true);
        switch (result)
        {
            case SaveGameSystemResult.Success success:
                StatusChanged?.Invoke(
                    intent == SaveContentIntent.Publish
                        ? $"Publié rev={success.PublishedRevision}"
                        : $"Brouillon enregistré rev={success.NewRevision}");
                await RefreshListAsync().ConfigureAwait(true);
                BindFlagForm();
                break;
            case SaveGameSystemResult.ValidationFailed validation:
                GameDataUiMessageBox.Show(this, validation.Error, "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                break;
            case SaveGameSystemResult.Conflict conflict:
                GameDataUiMessageBox.Show(this, $"Conflit de révision (courante={conflict.CurrentRevision}).", "Conflit");
                break;
            case SaveGameSystemResult.NotDurable notDurable:
                GameDataUiMessageBox.Show(this, notDurable.Message, "Persistance");
                break;
            case SaveGameSystemResult.PersistenceFailed persistence:
                GameDataUiMessageBox.Show(this, persistence.Error, "Erreur");
                break;
        }
    }

    private async Task SaveOptionsAsync(SaveContentIntent intent)
    {
        ApplyOptionsToSession();
        var result = await _options.SaveCurrentAsync(intent).ConfigureAwait(true);
        switch (result)
        {
            case SaveGameSystemResult.Success success:
                StatusChanged?.Invoke(
                    intent == SaveContentIntent.Publish
                        ? $"Options publiées rev={success.PublishedRevision}"
                        : $"Options enregistrées rev={success.NewRevision}");
                BindOptionsForm();
                break;
            case SaveGameSystemResult.ValidationFailed validation:
                GameDataUiMessageBox.Show(this, validation.Error, "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                break;
            case SaveGameSystemResult.Conflict conflict:
                GameDataUiMessageBox.Show(this, $"Conflit de révision (courante={conflict.CurrentRevision}).", "Conflit");
                break;
            case SaveGameSystemResult.NotDurable notDurable:
                GameDataUiMessageBox.Show(this, notDurable.Message, "Persistance");
                break;
            case SaveGameSystemResult.PersistenceFailed persistence:
                GameDataUiMessageBox.Show(this, persistence.Error, "Erreur");
                break;
        }
    }

    private async Task DeleteFlagAsync()
    {
        var result = await _flags.DeleteCurrentAsync().ConfigureAwait(true);
        switch (result)
        {
            case DeleteGameSystemResult.Success:
                StatusChanged?.Invoke("Supprimé");
                await RefreshListAsync().ConfigureAwait(true);
                BindFlagForm();
                break;
            case DeleteGameSystemResult.NotFound:
                GameDataUiMessageBox.Show(this, "Entrée introuvable.");
                break;
            case DeleteGameSystemResult.PersistenceFailed persistence:
                GameDataUiMessageBox.Show(this, persistence.Error, "Erreur");
                break;
        }
    }

    private void BrowseBgm()
    {
        var picked = EditorTestHooks.OverrideMapAudioPickPath;
        if (string.IsNullOrWhiteSpace(picked))
        {
            using var dialog = new OpenFileDialog
            {
                Filter = "Audio WAV (*.wav)|*.wav|Tous les fichiers (*.*)|*.*",
                Title = "Choisir la musique de départ",
                CheckFileExists = true,
                RestoreDirectory = true,
            };
            var initial = FindAudioFolder();
            if (initial is not null)
            {
                dialog.InitialDirectory = initial;
            }

            if (dialog.ShowDialog(this) != DialogResult.OK)
            {
                return;
            }

            picked = dialog.FileName;
        }

        if (!MapAudioTrack.TryFromPickedFile(picked, FindRepositoryRoot(), out var stored, out var error))
        {
            GameDataUiMessageBox.Show(
                this,
                error ?? "Fichier audio refusé.",
                "Musique de départ",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        _bgmPath.Text = stored;
    }

    private static string? FindRepositoryRoot()
    {
        foreach (var start in new[] { Environment.CurrentDirectory, AppContext.BaseDirectory })
        {
            if (string.IsNullOrWhiteSpace(start))
            {
                continue;
            }

            var dir = new DirectoryInfo(start);
            while (dir is not null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "Frog.Creator.sln")))
                {
                    return dir.FullName;
                }

                dir = dir.Parent;
            }
        }

        return null;
    }

    private static string? FindAudioFolder()
    {
        var root = FindRepositoryRoot();
        if (root is null)
        {
            return null;
        }

        var folder = Path.Combine(root, "Frog.Client", "Assets", "Audio");
        return Directory.Exists(folder) ? folder : null;
    }

    private sealed record CatalogItem(Guid Id, string Label)
    {
        public override string ToString() => Label;
    }
}
