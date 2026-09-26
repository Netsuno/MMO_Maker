using System.IO;
using Frog.Application.Content;
using Frog.Application.Maps;
using Frog.Core.Enums;
using Frog.Core.Models;
using Frog.Editor.Services;

namespace Frog.Editor.Forms.GameData;

/// <summary>
/// Fiche Système (base VX) : paramètres du projet (monnaie, groupe, carte, musiques, termes)
/// et catalogues nommés des interrupteurs et des variables.
/// </summary>
public sealed class SystemEditorPanel : UserControl
{
    private readonly GameDataPanelLifecycle _lifecycle = new();
    private readonly SystemFlagWorkspaceSession _session;
    private readonly SystemSettingsWorkspaceSession _settingsSession;
    private readonly IPublishedActorCatalog _actors;
    private readonly IMapRepository _maps;
    private readonly ContentRepositoryCapabilities _capabilities;
    private readonly ContentRepositoryCapabilities _settingsCapabilities;
    private readonly TabControl _tabs = new() { Dock = DockStyle.Fill };
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
    private readonly TextBox _currency = new() { Width = 160 };
    private readonly TextBox _termHp = new() { Width = 160 };
    private readonly TextBox _termMp = new() { Width = 160 };
    private readonly ComboBox _party1 = new() { Width = 280, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _party2 = new() { Width = 280, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _party3 = new() { Width = 280, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _party4 = new() { Width = 280, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly ComboBox _startMap = new() { Width = 280, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _titleAsset = new() { Width = 280 };
    private readonly NumericUpDown _titleVolume = new()
    {
        Width = 64,
        Minimum = MapAudioTrack.MinVolume,
        Maximum = MapAudioTrack.MaxVolume,
        Value = MapAudioTrack.DefaultVolume,
    };
    private readonly NumericUpDown _titleFade = new()
    {
        Width = 72,
        Minimum = MapAudioTrack.MinFadeMs,
        Maximum = MapAudioTrack.MaxFadeMs,
    };
    private readonly Button _btnTitleBrowse = new() { Text = "Parcourir…", AutoSize = true };
    private readonly Button _btnTitleClear = new() { Text = "Effacer", AutoSize = true };
    private readonly TextBox _startAsset = new() { Width = 280 };
    private readonly NumericUpDown _startVolume = new()
    {
        Width = 64,
        Minimum = MapAudioTrack.MinVolume,
        Maximum = MapAudioTrack.MaxVolume,
        Value = MapAudioTrack.DefaultVolume,
    };
    private readonly NumericUpDown _startFade = new()
    {
        Width = 72,
        Minimum = MapAudioTrack.MinFadeMs,
        Maximum = MapAudioTrack.MaxFadeMs,
    };
    private readonly Button _btnStartBrowse = new() { Text = "Parcourir…", AutoSize = true };
    private readonly Button _btnStartClear = new() { Text = "Effacer", AutoSize = true };
    private readonly Label _settingsMeta = new() { AutoSize = true };
    private readonly Label _settingsValidation = new() { AutoSize = true, ForeColor = Color.Firebrick };
    private readonly Button _btnSaveSettings = new() { Text = "Enregistrer brouillon", AutoSize = true };
    private readonly Button _btnPublishSettings = new() { Text = "Publier", AutoSize = true };
    private bool _suppressList;
    private bool _binding;
    private bool _settingsBound;

    public event Action<string>? StatusChanged;

    public bool IsDirty => _session.IsDirty || _settingsSession.IsDirty;

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

    internal TextBox CurrencyForTest => _currency;

    internal TextBox TermHpForTest => _termHp;

    internal TextBox TermMpForTest => _termMp;

    internal TextBox TitleBgmAssetForTest => _titleAsset;

    internal int TitleBgmVolumeForTest
    {
        get => (int)_titleVolume.Value;
        set => _titleVolume.Value = value;
    }

    internal int TitleBgmFadeForTest
    {
        get => (int)_titleFade.Value;
        set => _titleFade.Value = value;
    }

    internal TextBox StartBgmAssetForTest => _startAsset;

    internal int StartBgmVolumeForTest
    {
        get => (int)_startVolume.Value;
        set => _startVolume.Value = value;
    }

    internal int StartBgmFadeForTest
    {
        get => (int)_startFade.Value;
        set => _startFade.Value = value;
    }

    internal Button BtnSaveSettingsForTest => _btnSaveSettings;

    internal Button BtnPublishSettingsForTest => _btnPublishSettings;

    internal Label SettingsValidationForTest => _settingsValidation;

    internal long? SettingsPublishedRevisionForTest => _settingsSession.PublishedRevision;

    internal void SelectSettingsForTest() => _tabs.SelectedIndex = 0;

    internal void SelectCatalogForTest() => _tabs.SelectedIndex = 1;

    public SystemEditorPanel(
        SystemFlagWorkspaceSession session,
        SystemSettingsWorkspaceSession settings,
        IPublishedActorCatalog actors,
        IMapRepository maps,
        ContentRepositoryCapabilities capabilities,
        ContentRepositoryCapabilities settingsCapabilities)
    {
        _session = session;
        _settingsSession = settings ?? throw new ArgumentNullException(nameof(settings));
        _actors = actors ?? throw new ArgumentNullException(nameof(actors));
        _maps = maps ?? throw new ArgumentNullException(nameof(maps));
        _capabilities = capabilities;
        _settingsCapabilities = settingsCapabilities;
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

        var catalogPage = new TabPage("Interrupteurs / variables") { Padding = new Padding(4) };
        catalogPage.Controls.Add(form);
        catalogPage.Controls.Add(buttons);
        catalogPage.Controls.Add(left);
        var settingsPage = new TabPage("Paramètres") { Padding = new Padding(8), AutoScroll = true };
        BuildSettings(settingsPage);
        _tabs.TabPages.Add(settingsPage);
        _tabs.TabPages.Add(catalogPage);
        _tabs.SelectedIndex = 1;
        Controls.Add(_tabs);

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
        await _settingsSession.EnsureLoadedAsync().ConfigureAwait(true);
        await RefreshSettingsChoicesAsync().ConfigureAwait(true);
        StatusChanged?.Invoke($"Backend système : {_capabilities.DisplayLabel}");
    }

    internal void QueueRefreshList()
        => _ = _lifecycle.RunAsync(async ct =>
        {
            await RefreshListAsync(ct).ConfigureAwait(true);
            await RefreshSettingsChoicesAsync(ct).ConfigureAwait(true);
        }, "refresh");

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

    private void BuildSettings(TabPage page)
    {
        var form = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(12),
            AutoScroll = true,
        };
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 160));
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

        Row("Unité monétaire", _currency);
        Row("Terme HP", _termHp);
        Row("Terme MP", _termMp);
        Row("Groupe de départ", new Label { Text = "Jusqu’à 4 héros publiés", AutoSize = true });
        Row("Héros 1", _party1);
        Row("Héros 2", _party2);
        Row("Héros 3", _party3);
        Row("Héros 4", _party4);
        Row("Carte de départ", _startMap);
        Row("Musique du titre", TrackEditor(_titleAsset, _titleVolume, _titleFade, _btnTitleBrowse, _btnTitleClear));
        Row("Musique de départ", TrackEditor(_startAsset, _startVolume, _startFade, _btnStartBrowse, _btnStartClear));
        Row("État", _settingsMeta);
        Row("", _settingsValidation);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 40,
            FlowDirection = FlowDirection.LeftToRight,
        };
        buttons.Controls.AddRange(new Control[] { _btnSaveSettings, _btnPublishSettings });
        page.Controls.Add(form);
        page.Controls.Add(buttons);

        void Mark() => MarkSettings();
        _currency.TextChanged += (_, _) => Mark();
        _termHp.TextChanged += (_, _) => Mark();
        _termMp.TextChanged += (_, _) => Mark();
        _party1.SelectedIndexChanged += (_, _) => Mark();
        _party2.SelectedIndexChanged += (_, _) => Mark();
        _party3.SelectedIndexChanged += (_, _) => Mark();
        _party4.SelectedIndexChanged += (_, _) => Mark();
        _startMap.SelectedIndexChanged += (_, _) => Mark();
        _titleAsset.TextChanged += (_, _) => Mark();
        _titleVolume.ValueChanged += (_, _) => Mark();
        _titleFade.ValueChanged += (_, _) => Mark();
        _startAsset.TextChanged += (_, _) => Mark();
        _startVolume.ValueChanged += (_, _) => Mark();
        _startFade.ValueChanged += (_, _) => Mark();
        _btnTitleBrowse.Click += (_, _) => BrowseAudio("Choisir la musique du titre", _titleAsset);
        _btnTitleClear.Click += (_, _) => ClearTrack(_titleAsset, _titleVolume, _titleFade);
        _btnStartBrowse.Click += (_, _) => BrowseAudio("Choisir la musique de départ", _startAsset);
        _btnStartClear.Click += (_, _) => ClearTrack(_startAsset, _startVolume, _startFade);
        _btnSaveSettings.Click += (_, _) => _ = _lifecycle.TrackAsync(
            async _ => await SaveSettingsAsync(SaveContentIntent.SaveDraft).ConfigureAwait(true),
            "save");
        _btnPublishSettings.Click += (_, _) => _ = _lifecycle.TrackAsync(
            async _ => await SaveSettingsAsync(SaveContentIntent.Publish).ConfigureAwait(true),
            "publish");

        var canWrite = _settingsCapabilities.AllowsSave;
        _btnSaveSettings.Enabled = canWrite;
        _btnPublishSettings.Enabled = canWrite;
    }

    private static Control TrackEditor(
        TextBox path,
        NumericUpDown volume,
        NumericUpDown fade,
        Button browse,
        Button clear)
    {
        var host = new FlowLayoutPanel
        {
            AutoSize = true,
            WrapContents = true,
            FlowDirection = FlowDirection.LeftToRight,
        };
        host.Controls.Add(path);
        host.Controls.Add(browse);
        host.Controls.Add(clear);
        host.Controls.Add(new Label { Text = "Volume", AutoSize = true, Padding = new Padding(8, 6, 0, 0) });
        host.Controls.Add(volume);
        host.Controls.Add(new Label { Text = "Fondu (ms)", AutoSize = true, Padding = new Padding(8, 6, 0, 0) });
        host.Controls.Add(fade);
        return host;
    }

    private async Task RefreshSettingsChoicesAsync(CancellationToken ct = default)
    {
        if (_settingsBound)
        {
            ApplySettings();
        }

        var actors = await _actors.ListPublishedAsync(ct).ConfigureAwait(true);
        var maps = await _maps.ListSummariesAsync(ct).ConfigureAwait(true);
        if (ct.IsCancellationRequested)
        {
            return;
        }

        _binding = true;
        try
        {
            FillGuid(_party1, "(aucun)", actors.Select(actor => new GuidChoice(actor.Id, actor.Name)), _settingsSession.Current.PartyActor1);
            FillGuid(_party2, "(aucun)", actors.Select(actor => new GuidChoice(actor.Id, actor.Name)), _settingsSession.Current.PartyActor2);
            FillGuid(_party3, "(aucun)", actors.Select(actor => new GuidChoice(actor.Id, actor.Name)), _settingsSession.Current.PartyActor3);
            FillGuid(_party4, "(aucun)", actors.Select(actor => new GuidChoice(actor.Id, actor.Name)), _settingsSession.Current.PartyActor4);
            FillGuid(
                _startMap,
                "(aucune)",
                maps.Select(map => new GuidChoice(
                    map.MapId,
                    string.IsNullOrWhiteSpace(map.Name) ? map.MapId.ToString("N") : map.Name)),
                _settingsSession.Current.StartMapId);
        }
        finally
        {
            _binding = false;
        }

        BindSettings();
    }

    private void BindSettings()
    {
        var definition = _settingsSession.Current;
        _binding = true;
        try
        {
            _currency.Text = definition.CurrencyUnit;
            _termHp.Text = definition.TermHp;
            _termMp.Text = definition.TermMp;
            SelectGuid(_party1, definition.PartyActor1);
            SelectGuid(_party2, definition.PartyActor2);
            SelectGuid(_party3, definition.PartyActor3);
            SelectGuid(_party4, definition.PartyActor4);
            SelectGuid(_startMap, definition.StartMapId);
            BindTrack(_titleAsset, _titleVolume, _titleFade, definition.TitleBgm);
            BindTrack(_startAsset, _startVolume, _startFade, definition.StartBgm);
            _settingsMeta.Text =
                $"rev={_settingsSession.CurrentRevision}  statut={_settingsSession.CurrentStatus}  publié={_settingsSession.PublishedRevision?.ToString() ?? "—"}";
        }
        finally
        {
            _binding = false;
        }

        _settingsBound = true;
        LiveValidateSettings();
    }

    private void ApplySettings()
    {
        var current = _settingsSession.Current;
        current.CurrencyUnit = _currency.Text.Trim();
        current.TermHp = _termHp.Text.Trim();
        current.TermMp = _termMp.Text.Trim();
        current.PartyActor1 = ReadGuid(_party1);
        current.PartyActor2 = ReadGuid(_party2);
        current.PartyActor3 = ReadGuid(_party3);
        current.PartyActor4 = ReadGuid(_party4);
        current.StartMapId = ReadGuid(_startMap);
        current.TitleBgm = ReadTrack(_titleAsset, _titleVolume, _titleFade);
        current.StartBgm = ReadTrack(_startAsset, _startVolume, _startFade);
    }

    private void MarkSettings()
    {
        if (_binding || !_settingsBound)
        {
            return;
        }

        ApplySettings();
        _settingsSession.MarkDirty();
        LiveValidateSettings();
        StatusChanged?.Invoke("Modifié (non enregistré)");
    }

    private void LiveValidateSettings()
    {
        if (!_settingsBound)
        {
            _settingsValidation.Text = string.Empty;
            return;
        }

        ApplySettings();
        _settingsValidation.Text = _settingsSession.Current.Validate(out var error) ? string.Empty : error;
    }

    private async Task SaveSettingsAsync(SaveContentIntent intent)
    {
        ApplySettings();
        var result = await _settingsSession.SaveCurrentAsync(intent).ConfigureAwait(true);
        switch (result)
        {
            case SaveSystemSettingsResult.Success success:
                StatusChanged?.Invoke(
                    intent == SaveContentIntent.Publish
                        ? $"Paramètres publiés rev={success.PublishedRevision}"
                        : $"Paramètres enregistrés rev={success.NewRevision}");
                BindSettings();
                break;
            case SaveSystemSettingsResult.ValidationFailed validation:
                GameDataUiMessageBox.Show(
                    this,
                    validation.Error,
                    "Validation",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                break;
            case SaveSystemSettingsResult.Conflict conflict:
                GameDataUiMessageBox.Show(
                    this,
                    $"Conflit de révision (courante={conflict.CurrentRevision}).",
                    "Conflit");
                break;
            case SaveSystemSettingsResult.NotDurable notDurable:
                GameDataUiMessageBox.Show(this, notDurable.Message, "Persistance");
                break;
            case SaveSystemSettingsResult.PersistenceFailed persistence:
                GameDataUiMessageBox.Show(this, persistence.Error, "Erreur");
                break;
        }
    }

    private void BrowseAudio(string title, TextBox target)
    {
        var picked = EditorTestHooks.OverrideMapAudioPickPath;
        if (string.IsNullOrWhiteSpace(picked))
        {
            using var dialog = new OpenFileDialog
            {
                Filter = "Audio WAV (*.wav)|*.wav|Tous les fichiers (*.*)|*.*",
                Title = title,
                CheckFileExists = true,
                RestoreDirectory = true,
            };
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
                "Audio",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return;
        }

        target.Text = stored;
    }

    private static void ClearTrack(TextBox path, NumericUpDown volume, NumericUpDown fade)
    {
        path.Text = string.Empty;
        volume.Value = MapAudioTrack.DefaultVolume;
        fade.Value = MapAudioTrack.MinFadeMs;
    }

    private static MapAudioTrack ReadTrack(TextBox path, NumericUpDown volume, NumericUpDown fade)
        => new()
        {
            Asset = path.Text,
            Volume = (int)volume.Value,
            FadeMs = (int)fade.Value,
        };

    private static void BindTrack(TextBox path, NumericUpDown volume, NumericUpDown fade, MapAudioTrack track)
    {
        path.Text = track.Asset ?? string.Empty;
        volume.Value = Math.Clamp(track.Volume, MapAudioTrack.MinVolume, MapAudioTrack.MaxVolume);
        fade.Value = Math.Clamp(track.FadeMs, MapAudioTrack.MinFadeMs, MapAudioTrack.MaxFadeMs);
    }

    private static void FillGuid(
        ComboBox combo,
        string emptyLabel,
        IEnumerable<GuidChoice> choices,
        Guid? selectedId)
    {
        combo.Items.Clear();
        combo.Items.Add(new GuidChoice(null, emptyLabel));
        var list = choices.OrderBy(choice => choice.Label, StringComparer.OrdinalIgnoreCase).ToList();
        foreach (var choice in list)
        {
            combo.Items.Add(choice);
        }

        if (selectedId is Guid id && list.All(choice => choice.Id != id))
        {
            combo.Items.Add(new GuidChoice(id, id.ToString("N")));
        }

        SelectGuid(combo, selectedId);
    }

    private static Guid? ReadGuid(ComboBox combo)
        => combo.SelectedItem is GuidChoice choice ? choice.Id : null;

    private static void SelectGuid(ComboBox combo, Guid? id)
    {
        var match = combo.Items.Cast<GuidChoice>().FirstOrDefault(choice => choice.Id == id);
        combo.SelectedItem = match ?? combo.Items.Cast<GuidChoice>().FirstOrDefault();
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

    private sealed record CatalogItem(Guid Id, string Label)
    {
        public override string ToString() => Label;
    }

    private sealed record GuidChoice(Guid? Id, string Label)
    {
        public override string ToString() => Label;
    }
}
