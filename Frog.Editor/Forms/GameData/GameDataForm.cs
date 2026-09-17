using Frog.Application.Content;
using Frog.Core.Enums;
using Frog.Core.Models;
using Frog.Editor.Assets;
using Frog.Editor.Services;

namespace Frog.Editor.Forms.GameData;

/// <summary>
/// Shell « Données de jeu » : navigation catégories + éditeurs Phase 6.
/// Accès DB uniquement via ports Application.
/// </summary>
public sealed class GameDataForm : Form
{
    private readonly ListBox _categoryList = new() { Dock = DockStyle.Fill };
    private readonly Panel _host = new() { Dock = DockStyle.Fill };
    private readonly Panel _loading = new() { Dock = DockStyle.Fill, BackColor = Color.FromArgb(245, 245, 245) };
    private readonly Label _loadingLabel = new()
    {
        Dock = DockStyle.Fill,
        TextAlign = ContentAlignment.MiddleCenter,
        Text = "Initialisation des données de jeu…",
    };
    private readonly Label _status = new() { Dock = DockStyle.Bottom, Height = 22, TextAlign = ContentAlignment.MiddleLeft };

    private TilesetEditorPanel? _tilesets;
    private NpcEditorPanel? _npcs;
    private ItemEditorPanel? _items;
    private SpellEditorPanel? _spells;
    private ClassEditorPanel? _classes;
    private ShopEditorPanel? _shops;
    private ResourceAndSpawnEditorPanel? _resourcesAndSpawns;
    private GameDataRepositorySet? _repositorySet;
    private CancellationTokenSource? _initCts;
    private Task? _initializationTask;
    private bool _initialized;
    private bool _allowCloseAfterCleanup;
    private bool _cleanupRunning;
    private bool _closeCleanupFailed;
    private Exception? _closeCleanupException;

    internal Task InitializationTask => _initializationTask ?? Task.CompletedTask;

    internal bool IsInitializedForTest => _initialized;

    internal GameDataRepositorySet? RepositorySetForTest => _repositorySet;

    internal TilesetEditorPanel TilesetsForTest => _tilesets ?? throw new InvalidOperationException("Game Data not initialized.");

    internal NpcEditorPanel NpcsForTest => _npcs ?? throw new InvalidOperationException("Game Data not initialized.");

    internal ItemEditorPanel ItemsForTest => _items ?? throw new InvalidOperationException("Game Data not initialized.");

    internal SpellEditorPanel SpellsForTest => _spells ?? throw new InvalidOperationException("Game Data not initialized.");

    internal ClassEditorPanel ClassesForTest => _classes ?? throw new InvalidOperationException("Game Data not initialized.");

    internal ShopEditorPanel ShopsForTest => _shops ?? throw new InvalidOperationException("Game Data not initialized.");

    internal ResourceAndSpawnEditorPanel ResourcesForTest =>
        _resourcesAndSpawns ?? throw new InvalidOperationException("Game Data not initialized.");

    public GameDataForm()
    {
        Text = "Données de jeu";
        Width = 960;
        Height = 640;
        StartPosition = FormStartPosition.CenterParent;
        MinimizeBox = false;

        _loading.Controls.Add(_loadingLabel);
        _host.Controls.Add(_loading);

        _categoryList.Items.AddRange(new object[]
        {
            "Tilesets",
            "NPCs / monstres",
            "Objets",
            "Sorts / compétences",
            "Classes",
            "Boutiques",
            "Ressources / spawns",
        });
        _categoryList.SelectedIndex = 0;
        _categoryList.Enabled = false;
        _categoryList.SelectedIndexChanged += (_, _) => ShowCategory();

        var left = new Panel { Dock = DockStyle.Left, Width = 200, Padding = new Padding(4) };
        left.Controls.Add(_categoryList);
        Controls.Add(_host);
        Controls.Add(_status);
        Controls.Add(left);

        FormClosing += GameDataForm_FormClosing;
        Shown += GameDataForm_Shown;
        Load += GameDataForm_LoadAsync;
    }

    private void GameDataForm_Shown(object? sender, EventArgs e)
    {
        if (EditorTestHooks.UseSynchronousGameDataInitForTest)
        {
            EnsureInitializedSynchronouslyForTest();
        }
    }

    internal void SelectCategoryForTest(int index)
    {
        _categoryList.SelectedIndex = index;
        ShowCategory();
    }

    private async void GameDataForm_LoadAsync(object? sender, EventArgs e)
    {
        if (EditorTestHooks.UseSynchronousGameDataInitForTest)
        {
            return;
        }

        _initCts = new CancellationTokenSource();
        _initializationTask = InitializeCoreAsync(_initCts.Token);
        try
        {
            await _initializationTask.ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            if (!_cleanupRunning && !_allowCloseAfterCleanup && !IsDisposed)
            {
                Close();
            }
        }
        catch (Exception ex)
        {
            GameDataUiMessageBox.Show(this, ex.Message, "Données de jeu", MessageBoxButtons.OK, MessageBoxIcon.Error);
            if (!_cleanupRunning && !_allowCloseAfterCleanup && !IsDisposed)
            {
                Close();
            }
        }
    }

    private async Task InitializeCoreAsync(CancellationToken cancellationToken)
    {
        var progress = new Progress<string>(message =>
        {
            if (!IsDisposed)
            {
                _loadingLabel.Text = message;
                _status.Text = message;
            }
        });

        if (EditorTestHooks.GameDataInitBarrierForTest is { } initBarrier)
        {
            await initBarrier(cancellationToken).ConfigureAwait(true);
        }

        _repositorySet = await GameDataInitializationService
            .InitializeAsync(progress, cancellationToken)
            .ConfigureAwait(true);

        cancellationToken.ThrowIfCancellationRequested();

        BuildPanels(_repositorySet);

        await _tilesets!.InitializeAsync().ConfigureAwait(true);
        await _npcs!.InitializeAsync().ConfigureAwait(true);
        await _items!.InitializeAsync().ConfigureAwait(true);
        await _spells!.InitializeAsync().ConfigureAwait(true);
        await _classes!.InitializeAsync().ConfigureAwait(true);
        await _shops!.InitializeAsync().ConfigureAwait(true);
        await _resourcesAndSpawns!.InitializeAsync().ConfigureAwait(true);

        _initialized = true;
        ShowInitialCategory();
        _status.Text = $"Prêt — {_tilesets!.CapabilitiesLabelForTest}";
    }

    internal bool IsTilesetPanelVisibleForTest =>
        _initialized && _host.Controls.Contains(_tilesets!);

    internal int CategorySelectedIndexForTest => _categoryList.SelectedIndex;

    internal Panel HostPanelForTest => _host;

    internal void EnsureInitializedSynchronouslyForTest()
    {
        if (_initialized)
        {
            return;
        }

        _repositorySet = GameDataInitializationService.CreateInjectedSet();
        BuildPanels(_repositorySet);
        _tilesets!.InitializeAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        _npcs!.InitializeAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        _items!.InitializeAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        _spells!.InitializeAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        _classes!.InitializeAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        _shops!.InitializeAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        _resourcesAndSpawns!.InitializeAsync().ConfigureAwait(false).GetAwaiter().GetResult();
        _initialized = true;
        _initializationTask = Task.CompletedTask;
        ShowInitialCategory();
        _status.Text = $"Prêt — {_tilesets.CapabilitiesLabelForTest}";
    }

    private void BuildPanels(GameDataRepositorySet set)
    {
        _tilesets = new TilesetEditorPanel(
            new TilesetWorkspaceSession(set.Tileset.Repository),
            set.Tileset.Capabilities);
        _tilesets.StatusChanged += msg => _status.Text = msg;
        _npcs = new NpcEditorPanel(
            new NpcWorkspaceSession(set.Npc.Repository),
            set.Npc.Capabilities);
        _npcs.StatusChanged += msg => _status.Text = msg;
        _items = new ItemEditorPanel(
            new ItemWorkspaceSession(set.Item.Repository),
            set.Item.Capabilities);
        _items.StatusChanged += msg => _status.Text = msg;
        _spells = new SpellEditorPanel(
            new SpellWorkspaceSession(set.Spell.Repository),
            set.Spell.Capabilities);
        _spells.StatusChanged += msg => _status.Text = msg;
        _classes = new ClassEditorPanel(
            new ClassWorkspaceSession(set.Class.Repository),
            set.Spell.PublishedCatalog,
            set.Class.Capabilities);
        _classes.StatusChanged += msg => _status.Text = msg;
        _shops = new ShopEditorPanel(
            new ShopWorkspaceSession(set.Shop.Repository),
            set.Item.PublishedCatalog,
            set.Shop.Capabilities);
        _shops.StatusChanged += msg => _status.Text = msg;
        _resourcesAndSpawns = new ResourceAndSpawnEditorPanel(
            new ResourceWorkspaceSession(set.Resource.Repository),
            new ResourceSpawnWorkspaceSession(set.ResourceSpawn.Repository),
            set.Item.PublishedCatalog,
            set.Resource.PublishedCatalog,
            set.Map.Repository,
            set.Resource.Capabilities,
            set.ResourceSpawn.Capabilities);
        _resourcesAndSpawns.StatusChanged += msg => _status.Text = msg;

        _host.Controls.Remove(_loading);
        _categoryList.Enabled = true;
    }

    private void ShowInitialCategory()
    {
        ShowCategory();
    }

    private void GameDataForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_allowCloseAfterCleanup)
        {
            return;
        }

        if (_initialized
            && (_tilesets!.IsDirty
                || _npcs!.IsDirty
                || _items!.IsDirty
                || _spells!.IsDirty
                || _classes!.IsDirty
                || _shops!.IsDirty
                || _resourcesAndSpawns!.IsDirty))
        {
            var r = GameDataUiMessageBox.Show(
                this,
                "Modifications non enregistrées. Fermer quand même ?",
                "Données de jeu",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);
            if (r != DialogResult.Yes)
            {
                e.Cancel = true;
                return;
            }
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
                    // BeginInvoke so a modal MessageBox cannot deadlock the close state machine
                    // or STA test pumps waiting on CloseCleanupFailedForTest.
                    BeginInvoke(new Action(() =>
                    {
                        if (IsDisposed)
                        {
                            return;
                        }

                        GameDataUiMessageBox.Show(
                            this,
                            "La fermeture a expiré : une opération Données de jeu est encore en cours. "
                            + "Attendez la fin de l’opération puis réessayez de fermer.",
                            "Données de jeu",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Warning);
                    }));
                }

                return;
            }

            DisposeRepositorySetSafely();
            DisposePanelLifecycles();
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
                        "Données de jeu",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }));
            }
        }
    }

    private async Task<bool> RunCloseCleanupAsync(TimeSpan timeout)
    {
        _initCts?.Cancel();
        BeginClosePanels();

        var sw = System.Diagnostics.Stopwatch.StartNew();
        using var overall = new CancellationTokenSource(timeout);
        var token = overall.Token;

        TimeSpan Remaining()
        {
            var left = timeout - sw.Elapsed;
            return left <= TimeSpan.Zero ? TimeSpan.Zero : left;
        }

        if (_initializationTask is { IsCompleted: false })
        {
            try
            {
                await _initializationTask.WaitAsync(token).ConfigureAwait(true);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested)
            {
                return false;
            }
            catch (OperationCanceledException)
            {
                // initialization cancelled cooperatively
            }
            catch (Exception ex)
            {
                _closeCleanupException = ex;
            }
        }

        if (_initialized)
        {
            async Task<bool> DrainOne(Func<TimeSpan, Task<bool>> drain)
            {
                var left = Remaining();
                if (left <= TimeSpan.Zero)
                {
                    return false;
                }

                return await drain(left).ConfigureAwait(true);
            }

            if (!await DrainOne(t => _tilesets!.DrainAsync(t)).ConfigureAwait(true)
                || !await DrainOne(t => _npcs!.DrainAsync(t)).ConfigureAwait(true)
                || !await DrainOne(t => _items!.DrainAsync(t)).ConfigureAwait(true)
                || !await DrainOne(t => _spells!.DrainAsync(t)).ConfigureAwait(true)
                || !await DrainOne(t => _classes!.DrainAsync(t)).ConfigureAwait(true)
                || !await DrainOne(t => _shops!.DrainAsync(t)).ConfigureAwait(true)
                || !await DrainOne(t => _resourcesAndSpawns!.DrainAsync(t)).ConfigureAwait(true))
            {
                return false;
            }
        }

        if (_repositorySet?.DatabaseScope is { } scope)
        {
            try
            {
                var left = Remaining();
                if (left <= TimeSpan.Zero)
                {
                    return false;
                }

                using var scopeCts = CancellationTokenSource.CreateLinkedTokenSource(token);
                scopeCts.CancelAfter(left);
                await scope.DrainAsync(scopeCts.Token).ConfigureAwait(true);
            }
            catch (OperationCanceledException) when (token.IsCancellationRequested || Remaining() <= TimeSpan.Zero)
            {
                return false;
            }
            catch (Exception ex)
            {
                _closeCleanupException ??= ex;
                return false;
            }
        }

        // Never treat unfinished tracked work as successful cleanup.
        if (_initialized
            && (!(_tilesets?.LifecycleForTest.IsIdle ?? true)
                || !(_npcs?.LifecycleForTest.IsIdle ?? true)
                || !(_items?.LifecycleForTest.IsIdle ?? true)
                || !(_spells?.LifecycleForTest.IsIdle ?? true)
                || !(_classes?.LifecycleForTest.IsIdle ?? true)
                || !(_shops?.LifecycleForTest.IsIdle ?? true)
                || !(_resourcesAndSpawns?.IsIdleForTest ?? true)))
        {
            return false;
        }

        return true;
    }

    private void SetClosingUiState(bool enabled)
    {
        _categoryList.Enabled = enabled && _initialized;
        if (_initialized)
        {
            _tilesets!.Enabled = enabled;
            _npcs!.Enabled = enabled;
            _items!.Enabled = enabled;
            _spells!.Enabled = enabled;
            _classes!.Enabled = enabled;
            _shops!.Enabled = enabled;
            _resourcesAndSpawns!.Enabled = enabled;
        }

        _status.Text = enabled
            ? _status.Text
            : "Fermeture en cours — attente des opérations…";
    }

    private void BeginClosePanels()
    {
        _tilesets?.BeginClosing();
        _npcs?.BeginClosing();
        _items?.BeginClosing();
        _spells?.BeginClosing();
        _classes?.BeginClosing();
        _shops?.BeginClosing();
        _resourcesAndSpawns?.BeginClosing();
    }

    private void DisposePanelLifecycles()
    {
        _tilesets?.DisposeLifecycle();
        _npcs?.DisposeLifecycle();
        _items?.DisposeLifecycle();
        _spells?.DisposeLifecycle();
        _classes?.DisposeLifecycle();
        _shops?.DisposeLifecycle();
        _resourcesAndSpawns?.DisposeLifecycle();
    }

    private void DisposeRepositorySetSafely()
    {
        try
        {
            _repositorySet?.Dispose();
        }
        catch (Exception ex)
        {
            _closeCleanupException ??= ex;
        }
        finally
        {
            _repositorySet = null;
        }
    }

    internal Exception? CloseCleanupExceptionForTest => _closeCleanupException;

    internal bool CloseCleanupFailedForTest => _closeCleanupFailed;

    internal bool AllowFinalCloseForTest => _allowCloseAfterCleanup;

    internal string StatusTextForTest => _status.Text;

    /// <summary>Relance le nettoyage de fermeture après un échec (tests / utilisateur).</summary>
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

    private void ShowCategory()
    {
        if (!_initialized || _tilesets is null)
        {
            return;
        }

        _host.Controls.Clear();
        if (_categoryList.SelectedIndex == 0)
        {
            _tilesets.Dock = DockStyle.Fill;
            _host.Controls.Add(_tilesets);
        }
        else if (_categoryList.SelectedIndex == 1)
        {
            _npcs!.Dock = DockStyle.Fill;
            _host.Controls.Add(_npcs);
        }
        else if (_categoryList.SelectedIndex == 2)
        {
            _items!.Dock = DockStyle.Fill;
            _host.Controls.Add(_items);
        }
        else if (_categoryList.SelectedIndex == 3)
        {
            _spells!.Dock = DockStyle.Fill;
            _host.Controls.Add(_spells);
        }
        else if (_categoryList.SelectedIndex == 4)
        {
            _classes!.Dock = DockStyle.Fill;
            _host.Controls.Add(_classes);
        }
        else if (_categoryList.SelectedIndex == 5)
        {
            _shops!.Dock = DockStyle.Fill;
            _host.Controls.Add(_shops);
        }
        else
        {
            _resourcesAndSpawns!.Dock = DockStyle.Fill;
            _host.Controls.Add(_resourcesAndSpawns);
        }
    }
}

public sealed class TilesetEditorPanel : UserControl
{
    private readonly GameDataPanelLifecycle _lifecycle = new();
    private readonly TilesetWorkspaceSession _session;
    private readonly ContentRepositoryCapabilities _capabilities;
    private readonly ListBox _list = new() { Dock = DockStyle.Fill };
    private readonly TextBox _search = new() { Dock = DockStyle.Top, PlaceholderText = "Rechercher…" };
    private readonly ComboBox _statusFilter = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _name = new() { Width = 280 };
    private readonly TextBox _path = new() { Width = 280 };
    private readonly NumericUpDown _tileSize = new() { Minimum = 8, Maximum = 256, Value = 32, Width = 80 };
    private readonly NumericUpDown _width = new() { Minimum = 8, Maximum = 8192, Value = 32, Width = 80 };
    private readonly NumericUpDown _height = new() { Minimum = 8, Maximum = 8192, Value = 32, Width = 80 };
    private readonly TextBox _sha = new() { Width = 280 };
    private readonly NumericUpDown _palette = new() { Minimum = 0, Maximum = 99999, Value = 0, Width = 80 };
    private readonly Label _meta = new() { AutoSize = true };
    private readonly Label _validation = new() { AutoSize = true, ForeColor = Color.Firebrick };
    private readonly Button _btnNew = new() { Text = "Nouveau", AutoSize = true };
    private readonly Button _btnDup = new() { Text = "Dupliquer", AutoSize = true };
    private readonly Button _btnSave = new() { Text = "Enregistrer brouillon", AutoSize = true };
    private readonly Button _btnPublish = new() { Text = "Publier", AutoSize = true };
    private readonly Button _btnDelete = new() { Text = "Supprimer", AutoSize = true };
    private readonly AssetPreviewControl _preview = new() { Width = 128, Height = 128 };
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

    internal string CapabilitiesLabelForTest => _capabilities.DisplayLabel;

    internal Button BtnNewForTest => _btnNew;

    internal Button BtnDupForTest => _btnDup;

    internal Button BtnSaveForTest => _btnSave;

    internal Button BtnPublishForTest => _btnPublish;

    internal Button BtnDeleteForTest => _btnDelete;

    internal TextBox NameForTest => _name;

    internal TextBox PathForTest => _path;

    internal TextBox SearchForTest => _search;

    internal ComboBox StatusFilterForTest => _statusFilter;

    internal ListBox ListForTest => _list;

    internal Label ValidationForTest => _validation;

    internal AssetPreviewControl PreviewForTest => _preview;

    public TilesetEditorPanel(TilesetWorkspaceSession session, ContentRepositoryCapabilities capabilities)
    {
        _session = session;
        _capabilities = capabilities;
        _preview.AssetRoot = EditorTestHooks.OverrideProjectAssetRoot ?? ProjectAssetRoot.Resolve();

        _statusFilter.Items.AddRange(new object[] { "Tous", "Brouillon", "Publié" });
        _statusFilter.SelectedIndex = 0;

        var left = new Panel { Dock = DockStyle.Left, Width = 260, Padding = new Padding(4) };
        left.Controls.Add(_list);
        left.Controls.Add(_search);
        left.Controls.Add(_statusFilter);

        var form = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(12),
            AutoSize = true,
        };
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        void Row(string label, Control c)
        {
            var r = form.RowCount++;
            form.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, r);
            form.Controls.Add(c, 1, r);
        }

        Row("Nom", _name);
        Row("Chemin logique", _path);
        Row("Aperçu", _preview);
        Row("Taille tuile", _tileSize);
        Row("Largeur px", _width);
        Row("Hauteur px", _height);
        Row("SHA-256", _sha);
        Row("Palette éditeur", _palette);
        Row("État", _meta);
        Row("", _validation);

        var buttons = new FlowLayoutPanel { Dock = DockStyle.Bottom, Height = 40, FlowDirection = FlowDirection.LeftToRight };
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

            if (!GameDataListNavigation.ConfirmDiscardUnsavedChanges(this, "Tilesets", _session.IsDirty))
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
        _path.TextChanged += (_, _) =>
        {
            Mark();
            _preview.LogicalPath = _path.Text.Trim();
        };
        _tileSize.ValueChanged += (_, _) => Mark();
        _width.ValueChanged += (_, _) => Mark();
        _height.ValueChanged += (_, _) => Mark();
        _sha.TextChanged += (_, _) => Mark();
        _palette.ValueChanged += (_, _) => Mark();

        _btnNew.Click += (_, _) =>
        {
            var def = new TilesetDefinition
            {
                Id = Guid.NewGuid(),
                Name = "Nouveau tileset",
                LogicalPath = $"tiles/new_{Guid.NewGuid():N}.png",
                TileSizePixels = 32,
                WidthPixels = 32,
                HeightPixels = 32,
                Sha256Hex = new string('0', 64),
            };
            _session.AdoptNewDraft(def);
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
        StatusChanged?.Invoke($"Backend : {_capabilities.DisplayLabel}");
    }

    private async Task RefreshListAsync(CancellationToken ct = default)
    {
        try
        {
            await _session.RefreshCatalogAsync(ct).ConfigureAwait(true);
            if (ct.IsCancellationRequested)
            {
                return;
            }

            _suppressList = true;
            _list.Items.Clear();
            foreach (var e in _session.Catalog)
            {
                _list.Items.Add(new CatalogItem(e.TilesetId, $"{e.Name} [{e.Status}]"));
            }

            _suppressList = false;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
    }

    private void BindForm()
    {
        var d = _session.Current;
        if (d is null)
        {
            return;
        }

        _binding = true;
        try
        {
            _name.Text = d.Name;
            _path.Text = d.LogicalPath;
            _tileSize.Value = Math.Clamp(d.TileSizePixels, 8, 256);
            _width.Value = Math.Clamp(d.WidthPixels, 8, 8192);
            _height.Value = Math.Clamp(d.HeightPixels, 8, 8192);
            _sha.Text = d.Sha256Hex;
            _palette.Value = d.EditorPaletteId ?? 0;
            _preview.SetLogicalPathSilently(d.LogicalPath);
            _meta.Text =
                $"Id={d.Id:N}  rev={_session.CurrentRevision}  statut={_session.CurrentStatus}  publié={_session.PublishedRevision?.ToString() ?? "—"}";
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
        _session.Current.LogicalPath = _path.Text.Trim().Replace('\\', '/');
        _session.Current.TileSizePixels = (int)_tileSize.Value;
        _session.Current.WidthPixels = (int)_width.Value;
        _session.Current.HeightPixels = (int)_height.Value;
        _session.Current.Sha256Hex = _sha.Text.Trim();
        _session.Current.EditorPaletteId = _palette.Value <= 0 ? null : (int)_palette.Value;
    }

    private void LiveValidate()
    {
        if (_session.Current is null)
        {
            _validation.Text = string.Empty;
            return;
        }

        ApplyFormToSession();
        _validation.Text = _session.Current.Validate(out var err) ? string.Empty : err;
    }

    private async Task SaveAsync(SaveContentIntent intent)
    {
        ApplyFormToSession();
        var result = await _session.SaveCurrentAsync(intent).ConfigureAwait(true);
        switch (result)
        {
            case SaveTilesetResult.Success s:
                StatusChanged?.Invoke(
                    intent == SaveContentIntent.Publish
                        ? $"Publié rev={s.PublishedRevision}"
                        : $"Brouillon enregistré rev={s.NewRevision}");
                await RefreshListAsync().ConfigureAwait(true);
                BindForm();
                break;
            case SaveTilesetResult.ValidationFailed v:
                GameDataUiMessageBox.Show(this, v.Error, "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                break;
            case SaveTilesetResult.Conflict c:
                GameDataUiMessageBox.Show(this, $"Conflit de révision (courante={c.CurrentRevision}).", "Conflit");
                break;
            case SaveTilesetResult.NotDurable n:
                GameDataUiMessageBox.Show(this, n.Message, "Persistance");
                break;
            case SaveTilesetResult.PersistenceFailed p:
                GameDataUiMessageBox.Show(this, p.Error, "Erreur");
                break;
        }
    }

    private async Task DeleteAsync()
    {
        var result = await _session.DeleteCurrentAsync().ConfigureAwait(true);
        switch (result)
        {
            case DeleteTilesetResult.Success:
                StatusChanged?.Invoke("Supprimé");
                await RefreshListAsync().ConfigureAwait(true);
                break;
            case DeleteTilesetResult.Referenced r:
                GameDataUiMessageBox.Show(this, r.Error, "Référence", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                break;
            case DeleteTilesetResult.NotFound:
                GameDataUiMessageBox.Show(this, "Tileset introuvable.");
                break;
            case DeleteTilesetResult.PersistenceFailed p:
                GameDataUiMessageBox.Show(this, p.Error, "Erreur");
                break;
        }
    }

    private sealed record CatalogItem(Guid Id, string Label)
    {
        public override string ToString() => Label;
    }
}

/// <summary>Liste + formulaire NPC/monstre (brouillon / publication).</summary>
public sealed class NpcEditorPanel : UserControl
{
    private readonly GameDataPanelLifecycle _lifecycle = new();
    private readonly NpcWorkspaceSession _session;
    private readonly ContentRepositoryCapabilities _capabilities;
    private readonly ListBox _list = new() { Dock = DockStyle.Fill };
    private readonly TextBox _search = new() { Dock = DockStyle.Top, PlaceholderText = "Rechercher…" };
    private readonly ComboBox _statusFilter = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _name = new() { Width = 280 };
    private readonly ComboBox _kind = new() { Width = 160, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _spritePath = new() { Width = 280 };
    private readonly NumericUpDown _level = new() { Minimum = 1, Maximum = 99, Value = 1, Width = 80 };
    private readonly TextBox _notes = new() { Width = 360, Height = 90, Multiline = true, ScrollBars = ScrollBars.Vertical };
    private readonly NumericUpDown _alias = new() { Minimum = 0, Maximum = 99999, Value = 0, Width = 80 };
    private readonly Label _meta = new() { AutoSize = true };
    private readonly Label _validation = new() { AutoSize = true, ForeColor = Color.Firebrick };
    private readonly Button _btnNew = new() { Text = "Nouveau", AutoSize = true };
    private readonly Button _btnDup = new() { Text = "Dupliquer", AutoSize = true };
    private readonly Button _btnSave = new() { Text = "Enregistrer brouillon", AutoSize = true };
    private readonly Button _btnPublish = new() { Text = "Publier", AutoSize = true };
    private readonly Button _btnDelete = new() { Text = "Supprimer", AutoSize = true };
    private readonly AssetPreviewControl _preview = new() { Width = 128, Height = 128 };
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

    internal TextBox NameForTest => _name;

    internal TextBox SpritePathForTest => _spritePath;

    internal TextBox SearchForTest => _search;

    internal ComboBox StatusFilterForTest => _statusFilter;

    internal ListBox ListForTest => _list;

    internal Label ValidationForTest => _validation;

    internal AssetPreviewControl PreviewForTest => _preview;

    public NpcEditorPanel(NpcWorkspaceSession session, ContentRepositoryCapabilities capabilities)
    {
        _session = session;
        _capabilities = capabilities;
        _preview.AssetRoot = EditorTestHooks.OverrideProjectAssetRoot ?? ProjectAssetRoot.Resolve();

        _statusFilter.Items.AddRange(new object[] { "Tous", "Brouillon", "Publié" });
        _statusFilter.SelectedIndex = 0;
        _kind.Items.AddRange(new object[] { NpcKind.Npc, NpcKind.Monster });
        _kind.SelectedIndex = 0;

        var left = new Panel { Dock = DockStyle.Left, Width = 260, Padding = new Padding(4) };
        left.Controls.Add(_list);
        left.Controls.Add(_search);
        left.Controls.Add(_statusFilter);

        var form = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(12),
            AutoSize = true,
        };
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        void Row(string label, Control control)
        {
            var row = form.RowCount++;
            form.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
            form.Controls.Add(control, 1, row);
        }

        Row("Nom", _name);
        Row("Type", _kind);
        Row("Chemin sprite", _spritePath);
        Row("Aperçu", _preview);
        Row("Niveau", _level);
        Row("Notes", _notes);
        Row("Alias éditeur", _alias);
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

            if (!GameDataListNavigation.ConfirmDiscardUnsavedChanges(this, "NPCs / monstres", _session.IsDirty))
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
        _kind.SelectedIndexChanged += (_, _) => Mark();
        _spritePath.TextChanged += (_, _) =>
        {
            Mark();
            _preview.LogicalPath = _spritePath.Text.Trim();
        };
        _level.ValueChanged += (_, _) => Mark();
        _notes.TextChanged += (_, _) => Mark();
        _alias.ValueChanged += (_, _) => Mark();

        _btnNew.Click += (_, _) =>
        {
            _session.AdoptNewDraft(new NpcDefinition
            {
                Id = Guid.NewGuid(),
                Name = "Nouveau NPC",
                Kind = NpcKind.Npc,
                SpriteLogicalPath = $"sprites/npcs/new_{Guid.NewGuid():N}.png",
                Level = 1,
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
        StatusChanged?.Invoke($"Backend NPC : {_capabilities.DisplayLabel}");
    }

    private async Task RefreshListAsync(CancellationToken ct = default)
    {
        try
        {
            await _session.RefreshCatalogAsync(ct).ConfigureAwait(true);
            if (ct.IsCancellationRequested)
            {
                return;
            }

            _suppressList = true;
            _list.Items.Clear();
            foreach (var entry in _session.Catalog)
            {
                _list.Items.Add(new CatalogItem(
                    entry.NpcId,
                    $"{entry.Name} ({entry.Kind}, niv. {entry.Level}) [{entry.Status}]"));
            }

            _suppressList = false;
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
        }
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
            _kind.SelectedItem = definition.Kind;
            _spritePath.Text = definition.SpriteLogicalPath;
            _level.Value = Math.Clamp(definition.Level, 1, 99);
            _notes.Text = definition.Notes ?? string.Empty;
            _alias.Value = definition.EditorAliasId ?? 0;
            _preview.SetLogicalPathSilently(definition.SpriteLogicalPath);
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
        _session.Current.Kind = _kind.SelectedItem is NpcKind kind ? kind : NpcKind.Npc;
        _session.Current.SpriteLogicalPath = _spritePath.Text.Trim().Replace('\\', '/');
        _session.Current.Level = (int)_level.Value;
        _session.Current.Notes = string.IsNullOrWhiteSpace(_notes.Text) ? null : _notes.Text.Trim();
        _session.Current.EditorAliasId = _alias.Value <= 0 ? null : (int)_alias.Value;
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
            case SaveNpcResult.Success success:
                StatusChanged?.Invoke(
                    intent == SaveContentIntent.Publish
                        ? $"Publié rev={success.PublishedRevision}"
                        : $"Brouillon enregistré rev={success.NewRevision}");
                await RefreshListAsync().ConfigureAwait(true);
                BindForm();
                break;
            case SaveNpcResult.ValidationFailed validation:
                GameDataUiMessageBox.Show(this, validation.Error, "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                break;
            case SaveNpcResult.Conflict conflict:
                GameDataUiMessageBox.Show(
                    this,
                    $"Conflit de révision (courante={conflict.CurrentRevision}).",
                    "Conflit");
                break;
            case SaveNpcResult.NotDurable notDurable:
                GameDataUiMessageBox.Show(this, notDurable.Message, "Persistance");
                break;
            case SaveNpcResult.PersistenceFailed persistence:
                GameDataUiMessageBox.Show(this, persistence.Error, "Erreur");
                break;
        }
    }

    private async Task DeleteAsync()
    {
        var result = await _session.DeleteCurrentAsync().ConfigureAwait(true);
        switch (result)
        {
            case DeleteNpcResult.Success:
                StatusChanged?.Invoke("Supprimé");
                await RefreshListAsync().ConfigureAwait(true);
                break;
            case DeleteNpcResult.Referenced referenced:
                GameDataUiMessageBox.Show(
                    this,
                    referenced.Error,
                    "Référence",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                break;
            case DeleteNpcResult.NotFound:
                GameDataUiMessageBox.Show(this, "NPC introuvable.");
                break;
            case DeleteNpcResult.PersistenceFailed persistence:
                GameDataUiMessageBox.Show(this, persistence.Error, "Erreur");
                break;
        }
    }

    private sealed record CatalogItem(Guid Id, string Label)
    {
        public override string ToString() => Label;
    }
}

/// <summary>Liste + formulaire objet (brouillon / publication).</summary>
public sealed class ItemEditorPanel : UserControl
{
    private readonly GameDataPanelLifecycle _lifecycle = new();
    private readonly ItemWorkspaceSession _session;
    private readonly ContentRepositoryCapabilities _capabilities;
    private readonly ListBox _list = new() { Dock = DockStyle.Fill };
    private readonly TextBox _search = new() { Dock = DockStyle.Top, PlaceholderText = "Rechercher…" };
    private readonly ComboBox _statusFilter = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _name = new() { Width = 280 };
    private readonly ComboBox _kind = new() { Width = 160, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _iconPath = new() { Width = 280 };
    private readonly NumericUpDown _maxStack = new() { Minimum = 1, Maximum = 999, Value = 1, Width = 80 };
    private readonly NumericUpDown _buyPrice = new() { Minimum = 0, Maximum = 999999999, Width = 120 };
    private readonly NumericUpDown _sellPrice = new() { Minimum = 0, Maximum = 999999999, Width = 120 };
    private readonly TextBox _description = new()
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
    private readonly AssetPreviewControl _preview = new() { Width = 128, Height = 128 };
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

    internal TextBox NameForTest => _name;

    internal TextBox IconPathForTest => _iconPath;

    internal TextBox SearchForTest => _search;

    internal ComboBox StatusFilterForTest => _statusFilter;

    internal ListBox ListForTest => _list;

    internal Label ValidationForTest => _validation;

    internal AssetPreviewControl PreviewForTest => _preview;

    public ItemEditorPanel(ItemWorkspaceSession session, ContentRepositoryCapabilities capabilities)
    {
        _session = session;
        _capabilities = capabilities;
        _preview.AssetRoot = EditorTestHooks.OverrideProjectAssetRoot ?? ProjectAssetRoot.Resolve();

        _statusFilter.Items.AddRange(new object[] { "Tous", "Brouillon", "Publié" });
        _statusFilter.SelectedIndex = 0;
        _kind.Items.AddRange(
            Enum.GetValues<ItemType>()
                .Where(value => value != ItemType.Unknown)
                .Cast<object>()
                .ToArray());
        _kind.SelectedItem = ItemType.Consumable;

        var left = new Panel { Dock = DockStyle.Left, Width = 260, Padding = new Padding(4) };
        left.Controls.Add(_list);
        left.Controls.Add(_search);
        left.Controls.Add(_statusFilter);

        var form = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(12),
            AutoSize = true,
        };
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 140));
        form.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100));
        void Row(string label, Control control)
        {
            var row = form.RowCount++;
            form.Controls.Add(new Label { Text = label, AutoSize = true, Anchor = AnchorStyles.Left }, 0, row);
            form.Controls.Add(control, 1, row);
        }

        Row("Nom", _name);
        Row("Type", _kind);
        Row("Chemin icône", _iconPath);
        Row("Aperçu", _preview);
        Row("Pile maximum", _maxStack);
        Row("Prix d’achat", _buyPrice);
        Row("Prix de vente", _sellPrice);
        Row("Description", _description);
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

            if (!GameDataListNavigation.ConfirmDiscardUnsavedChanges(this, "Objets", _session.IsDirty))
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
        _kind.SelectedIndexChanged += (_, _) => Mark();
        _iconPath.TextChanged += (_, _) =>
        {
            Mark();
            _preview.LogicalPath = _iconPath.Text.Trim();
        };
        _maxStack.ValueChanged += (_, _) => Mark();
        _buyPrice.ValueChanged += (_, _) => Mark();
        _sellPrice.ValueChanged += (_, _) => Mark();
        _description.TextChanged += (_, _) => Mark();

        _btnNew.Click += (_, _) =>
        {
            _session.AdoptNewDraft(new ItemDefinition
            {
                Id = Guid.NewGuid(),
                Name = "Nouvel objet",
                Kind = ItemType.Consumable,
                IconLogicalPath = $"icons/items/new_{Guid.NewGuid():N}.png",
                MaxStack = 1,
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
        StatusChanged?.Invoke($"Backend objets : {_capabilities.DisplayLabel}");
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
        foreach (var entry in _session.Catalog)
        {
            _list.Items.Add(new CatalogItem(
                entry.ItemId,
                $"{entry.Name} ({entry.Kind}, pile {entry.MaxStack}) [{entry.Status}]"));
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
            _kind.SelectedItem = definition.Kind;
            _iconPath.Text = definition.IconLogicalPath;
            _preview.SetLogicalPathSilently(definition.IconLogicalPath);
            _maxStack.Value = Math.Clamp(definition.MaxStack, 1, 999);
            _buyPrice.Value = Math.Clamp(definition.BuyPrice, 0, 999999999);
            _sellPrice.Value = Math.Clamp(definition.SellPrice, 0, 999999999);
            _description.Text = definition.Description ?? string.Empty;
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
        _session.Current.Kind = _kind.SelectedItem is ItemType kind ? kind : ItemType.Unknown;
        _session.Current.IconLogicalPath = _iconPath.Text.Trim().Replace('\\', '/');
        _session.Current.MaxStack = (int)_maxStack.Value;
        _session.Current.BuyPrice = (int)_buyPrice.Value;
        _session.Current.SellPrice = (int)_sellPrice.Value;
        _session.Current.Description = string.IsNullOrWhiteSpace(_description.Text)
            ? null
            : _description.Text.Trim();
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
            case SaveItemResult.Success success:
                StatusChanged?.Invoke(
                    intent == SaveContentIntent.Publish
                        ? $"Publié rev={success.PublishedRevision}"
                        : $"Brouillon enregistré rev={success.NewRevision}");
                await RefreshListAsync().ConfigureAwait(true);
                BindForm();
                break;
            case SaveItemResult.ValidationFailed validation:
                GameDataUiMessageBox.Show(this, validation.Error, "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                break;
            case SaveItemResult.Conflict conflict:
                GameDataUiMessageBox.Show(
                    this,
                    $"Conflit de révision (courante={conflict.CurrentRevision}).",
                    "Conflit");
                break;
            case SaveItemResult.NotDurable notDurable:
                GameDataUiMessageBox.Show(this, notDurable.Message, "Persistance");
                break;
            case SaveItemResult.PersistenceFailed persistence:
                GameDataUiMessageBox.Show(this, persistence.Error, "Erreur");
                break;
        }
    }

    private async Task DeleteAsync()
    {
        var result = await _session.DeleteCurrentAsync().ConfigureAwait(true);
        switch (result)
        {
            case DeleteItemResult.Success:
                StatusChanged?.Invoke("Supprimé");
                await RefreshListAsync().ConfigureAwait(true);
                break;
            case DeleteItemResult.NotFound:
                GameDataUiMessageBox.Show(this, "Objet introuvable.");
                break;
            case DeleteItemResult.Referenced referenced:
                GameDataUiMessageBox.Show(
                    this,
                    referenced.Error,
                    "Référence",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                break;
            case DeleteItemResult.PersistenceFailed persistence:
                GameDataUiMessageBox.Show(this, persistence.Error, "Erreur");
                break;
        }
    }

    private sealed record CatalogItem(Guid Id, string Label)
    {
        public override string ToString() => Label;
    }
}

/// <summary>Liste + formulaire sort/compétence (brouillon / publication).</summary>
public sealed class SpellEditorPanel : UserControl
{
    private readonly GameDataPanelLifecycle _lifecycle = new();
    private readonly SpellWorkspaceSession _session;
    private readonly ContentRepositoryCapabilities _capabilities;
    private readonly ListBox _list = new() { Dock = DockStyle.Fill };
    private readonly TextBox _search = new() { Dock = DockStyle.Top, PlaceholderText = "Rechercher…" };
    private readonly ComboBox _statusFilter = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _name = new() { Width = 280 };
    private readonly ComboBox _kind = new() { Width = 160, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly NumericUpDown _manaCost = new() { Minimum = 0, Maximum = int.MaxValue, Width = 120 };
    private readonly NumericUpDown _cooldown = new() { Minimum = 0, Maximum = int.MaxValue, Width = 120 };
    private readonly ComboBox _targetType = new() { Width = 160, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _iconPath = new() { Width = 280 };
    private readonly TextBox _description = new()
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
    private readonly AssetPreviewControl _preview = new() { Width = 128, Height = 128 };
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

    internal TextBox NameForTest => _name;

    internal TextBox IconPathForTest => _iconPath;

    internal TextBox SearchForTest => _search;

    internal ComboBox StatusFilterForTest => _statusFilter;

    internal ListBox ListForTest => _list;

    internal Label ValidationForTest => _validation;

    internal AssetPreviewControl PreviewForTest => _preview;

    public SpellEditorPanel(
        SpellWorkspaceSession session,
        ContentRepositoryCapabilities capabilities)
    {
        _session = session;
        _capabilities = capabilities;
        _preview.AssetRoot = EditorTestHooks.OverrideProjectAssetRoot ?? ProjectAssetRoot.Resolve();

        _statusFilter.Items.AddRange(new object[] { "Tous", "Brouillon", "Publié" });
        _statusFilter.SelectedIndex = 0;
        _kind.Items.AddRange(Enum.GetValues<SpellKind>().Cast<object>().ToArray());
        _kind.SelectedItem = SpellKind.Spell;
        _targetType.Items.AddRange(Enum.GetValues<TargetType>().Cast<object>().ToArray());
        _targetType.SelectedItem = TargetType.SingleEnemy;

        var left = new Panel { Dock = DockStyle.Left, Width = 260, Padding = new Padding(4) };
        left.Controls.Add(_list);
        left.Controls.Add(_search);
        left.Controls.Add(_statusFilter);

        var form = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 2,
            Padding = new Padding(12),
            AutoSize = true,
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

        Row("Nom", _name);
        Row("Type", _kind);
        Row("Coût en mana", _manaCost);
        Row("Recharge (ms)", _cooldown);
        Row("Cible", _targetType);
        Row("Chemin icône", _iconPath);
        Row("Aperçu", _preview);
        Row("Description", _description);
        Row("État", _meta);
        Row("", _validation);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 40,
            FlowDirection = FlowDirection.LeftToRight,
        };
        buttons.Controls.AddRange(
            new Control[] { _btnNew, _btnDup, _btnSave, _btnPublish, _btnDelete });

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

            if (!GameDataListNavigation.ConfirmDiscardUnsavedChanges(this, "Sorts / compétences", _session.IsDirty))
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
        _kind.SelectedIndexChanged += (_, _) => Mark();
        _manaCost.ValueChanged += (_, _) => Mark();
        _cooldown.ValueChanged += (_, _) => Mark();
        _targetType.SelectedIndexChanged += (_, _) => Mark();
        _iconPath.TextChanged += (_, _) =>
        {
            Mark();
            _preview.LogicalPath = _iconPath.Text.Trim();
        };
        _description.TextChanged += (_, _) => Mark();

        _btnNew.Click += (_, _) =>
        {
            _session.AdoptNewDraft(new SpellDefinition
            {
                Id = Guid.NewGuid(),
                Name = "Nouveau sort",
                Kind = SpellKind.Spell,
                TargetType = TargetType.SingleEnemy,
                IconLogicalPath = $"icons/spells/new_{Guid.NewGuid():N}.png",
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
        StatusChanged?.Invoke($"Backend sorts/compétences : {_capabilities.DisplayLabel}");
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
        foreach (var entry in _session.Catalog)
        {
            _list.Items.Add(new CatalogItem(
                entry.SpellId,
                $"{entry.Name} ({entry.Kind}, {entry.TargetType}) [{entry.Status}]"));
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
            _kind.SelectedItem = definition.Kind;
            _manaCost.Value = Math.Clamp(definition.ManaCost, 0, int.MaxValue);
            _cooldown.Value = Math.Clamp(definition.CooldownMs, 0, int.MaxValue);
            _targetType.SelectedItem = definition.TargetType;
            _iconPath.Text = definition.IconLogicalPath;
            _preview.SetLogicalPathSilently(definition.IconLogicalPath);
            _description.Text = definition.Description ?? string.Empty;
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
        _session.Current.Kind = _kind.SelectedItem is SpellKind kind ? kind : SpellKind.Spell;
        _session.Current.ManaCost = (int)_manaCost.Value;
        _session.Current.CooldownMs = (int)_cooldown.Value;
        _session.Current.TargetType = _targetType.SelectedItem is TargetType target
            ? target
            : TargetType.Self;
        _session.Current.IconLogicalPath = _iconPath.Text.Trim().Replace('\\', '/');
        _session.Current.Description = string.IsNullOrWhiteSpace(_description.Text)
            ? null
            : _description.Text.Trim();
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
            case SaveSpellResult.Success success:
                StatusChanged?.Invoke(
                    intent == SaveContentIntent.Publish
                        ? $"Publié rev={success.PublishedRevision}"
                        : $"Brouillon enregistré rev={success.NewRevision}");
                await RefreshListAsync().ConfigureAwait(true);
                BindForm();
                break;
            case SaveSpellResult.ValidationFailed validation:
                GameDataUiMessageBox.Show(
                    this,
                    validation.Error,
                    "Validation",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                break;
            case SaveSpellResult.Conflict conflict:
                GameDataUiMessageBox.Show(
                    this,
                    $"Conflit de révision (courante={conflict.CurrentRevision}).",
                    "Conflit");
                break;
            case SaveSpellResult.NotDurable notDurable:
                GameDataUiMessageBox.Show(this, notDurable.Message, "Persistance");
                break;
            case SaveSpellResult.PersistenceFailed persistence:
                GameDataUiMessageBox.Show(this, persistence.Error, "Erreur");
                break;
        }
    }

    private async Task DeleteAsync()
    {
        var result = await _session.DeleteCurrentAsync().ConfigureAwait(true);
        switch (result)
        {
            case DeleteSpellResult.Success:
                StatusChanged?.Invoke("Supprimé");
                await RefreshListAsync().ConfigureAwait(true);
                break;
            case DeleteSpellResult.NotFound:
                GameDataUiMessageBox.Show(this, "Sort ou compétence introuvable.");
                break;
            case DeleteSpellResult.Referenced referenced:
                GameDataUiMessageBox.Show(
                    this,
                    referenced.Error,
                    "Référence",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                break;
            case DeleteSpellResult.PersistenceFailed persistence:
                GameDataUiMessageBox.Show(this, persistence.Error, "Erreur");
                break;
        }
    }

    private sealed record CatalogItem(Guid Id, string Label)
    {
        public override string ToString() => Label;
    }
}

/// <summary>Liste + formulaire classe (brouillon / publication).</summary>
public sealed class ClassEditorPanel : UserControl
{
    private readonly GameDataPanelLifecycle _lifecycle = new();
    private readonly ClassWorkspaceSession _session;
    private readonly IPublishedSpellCatalog _spellCatalog;
    private readonly ContentRepositoryCapabilities _capabilities;
    private readonly ListBox _list = new() { Dock = DockStyle.Fill };
    private readonly TextBox _search = new() { Dock = DockStyle.Top, PlaceholderText = "Rechercher…" };
    private readonly ComboBox _statusFilter = new() { Dock = DockStyle.Top, DropDownStyle = ComboBoxStyle.DropDownList };
    private readonly TextBox _name = new() { Width = 280 };
    private readonly TextBox _description = new()
    {
        Width = 360,
        Height = 70,
        Multiline = true,
        ScrollBars = ScrollBars.Vertical,
    };
    private readonly NumericUpDown _baseHp = new() { Minimum = 1, Maximum = int.MaxValue, Value = 100, Width = 120 };
    private readonly NumericUpDown _baseMp = new() { Minimum = 1, Maximum = int.MaxValue, Value = 50, Width = 120 };
    private readonly NumericUpDown _str = StatControl();
    private readonly NumericUpDown _agi = StatControl();
    private readonly NumericUpDown _vit = StatControl();
    private readonly NumericUpDown _int = StatControl();
    private readonly NumericUpDown _dex = StatControl();
    private readonly NumericUpDown _luck = StatControl();
    private readonly ComboBox _startingSpell = new()
    {
        Width = 280,
        DropDownStyle = ComboBoxStyle.DropDownList,
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

    internal TextBox NameForTest => _name;

    internal TextBox SearchForTest => _search;

    internal ComboBox StatusFilterForTest => _statusFilter;

    internal ComboBox StartingSpellForTest => _startingSpell;

    internal ListBox ListForTest => _list;

    internal Label ValidationForTest => _validation;

    public ClassEditorPanel(
        ClassWorkspaceSession session,
        IPublishedSpellCatalog spellCatalog,
        ContentRepositoryCapabilities capabilities)
    {
        _session = session;
        _spellCatalog = spellCatalog;
        _capabilities = capabilities;

        _statusFilter.Items.AddRange(new object[] { "Tous", "Brouillon", "Publié" });
        _statusFilter.SelectedIndex = 0;
        _startingSpell.Items.Add(new SpellChoice(null, "Aucun"));
        _startingSpell.SelectedIndex = 0;

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

        Row("Nom", _name);
        Row("Description", _description);
        Row("PV de base", _baseHp);
        Row("PM de base", _baseMp);
        Row("FOR", _str);
        Row("AGI", _agi);
        Row("VIT", _vit);
        Row("INT", _int);
        Row("DEX", _dex);
        Row("CHANCE", _luck);
        Row("Sort de départ", _startingSpell);
        Row("État", _meta);
        Row("", _validation);

        var buttons = new FlowLayoutPanel
        {
            Dock = DockStyle.Bottom,
            Height = 40,
            FlowDirection = FlowDirection.LeftToRight,
        };
        buttons.Controls.AddRange(
            new Control[] { _btnNew, _btnDup, _btnSave, _btnPublish, _btnDelete });

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

            if (!GameDataListNavigation.ConfirmDiscardUnsavedChanges(this, "Classes", _session.IsDirty))
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
        _description.TextChanged += (_, _) => Mark();
        _baseHp.ValueChanged += (_, _) => Mark();
        _baseMp.ValueChanged += (_, _) => Mark();
        _str.ValueChanged += (_, _) => Mark();
        _agi.ValueChanged += (_, _) => Mark();
        _vit.ValueChanged += (_, _) => Mark();
        _int.ValueChanged += (_, _) => Mark();
        _dex.ValueChanged += (_, _) => Mark();
        _luck.ValueChanged += (_, _) => Mark();
        _startingSpell.SelectedIndexChanged += (_, _) => Mark();

        _btnNew.Click += (_, _) =>
        {
            _session.AdoptNewDraft(new ClassDefinition
            {
                Id = Guid.NewGuid(),
                Name = "Nouvelle classe",
                BaseHp = 100,
                BaseMp = 50,
                Str = 10,
                Agi = 10,
                Vit = 10,
                Int = 10,
                Dex = 10,
                Luck = 10,
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
        await RefreshStartingSpellsAsync().ConfigureAwait(true);
        await RefreshListAsync().ConfigureAwait(true);
        StatusChanged?.Invoke($"Backend classes : {_capabilities.DisplayLabel}");
    }

    private async Task RefreshStartingSpellsAsync()
    {
        var selectedId = (_startingSpell.SelectedItem as SpellChoice)?.Id;
        var spells = await _spellCatalog.ListPublishedAsync().ConfigureAwait(true);
        _binding = true;
        try
        {
            _startingSpell.Items.Clear();
            _startingSpell.Items.Add(new SpellChoice(null, "Aucun"));
            foreach (var spell in spells.OrderBy(s => s.Name, StringComparer.OrdinalIgnoreCase))
            {
                _startingSpell.Items.Add(new SpellChoice(spell.Id, spell.Name));
            }

            _startingSpell.SelectedItem = _startingSpell.Items
                .Cast<SpellChoice>()
                .FirstOrDefault(choice => choice.Id == selectedId)
                ?? _startingSpell.Items.Cast<SpellChoice>().First();
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
        foreach (var entry in _session.Catalog)
        {
            _list.Items.Add(new CatalogItem(
                entry.ClassId,
                $"{entry.Name} (PV {entry.BaseHp}, PM {entry.BaseMp}) [{entry.Status}]"));
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
            _description.Text = definition.Description ?? string.Empty;
            _baseHp.Value = Math.Clamp(definition.BaseHp, 1, int.MaxValue);
            _baseMp.Value = Math.Clamp(definition.BaseMp, 1, int.MaxValue);
            _str.Value = Math.Clamp(definition.Str, ClassDefinition.MinStat, ClassDefinition.MaxStat);
            _agi.Value = Math.Clamp(definition.Agi, ClassDefinition.MinStat, ClassDefinition.MaxStat);
            _vit.Value = Math.Clamp(definition.Vit, ClassDefinition.MinStat, ClassDefinition.MaxStat);
            _int.Value = Math.Clamp(definition.Int, ClassDefinition.MinStat, ClassDefinition.MaxStat);
            _dex.Value = Math.Clamp(definition.Dex, ClassDefinition.MinStat, ClassDefinition.MaxStat);
            _luck.Value = Math.Clamp(definition.Luck, ClassDefinition.MinStat, ClassDefinition.MaxStat);
            _startingSpell.SelectedItem = _startingSpell.Items
                .Cast<SpellChoice>()
                .FirstOrDefault(choice => choice.Id == definition.StartingSpellId)
                ?? _startingSpell.Items.Cast<SpellChoice>().First();
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
        _session.Current.Description = string.IsNullOrWhiteSpace(_description.Text)
            ? null
            : _description.Text.Trim();
        _session.Current.BaseHp = (int)_baseHp.Value;
        _session.Current.BaseMp = (int)_baseMp.Value;
        _session.Current.Str = (int)_str.Value;
        _session.Current.Agi = (int)_agi.Value;
        _session.Current.Vit = (int)_vit.Value;
        _session.Current.Int = (int)_int.Value;
        _session.Current.Dex = (int)_dex.Value;
        _session.Current.Luck = (int)_luck.Value;
        _session.Current.StartingSpellId = (_startingSpell.SelectedItem as SpellChoice)?.Id;
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
            case SaveClassResult.Success success:
                StatusChanged?.Invoke(
                    intent == SaveContentIntent.Publish
                        ? $"Publié rev={success.PublishedRevision}"
                        : $"Brouillon enregistré rev={success.NewRevision}");
                await RefreshListAsync().ConfigureAwait(true);
                BindForm();
                break;
            case SaveClassResult.ValidationFailed validation:
                GameDataUiMessageBox.Show(
                    this,
                    validation.Error,
                    "Validation",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Warning);
                break;
            case SaveClassResult.Conflict conflict:
                GameDataUiMessageBox.Show(
                    this,
                    $"Conflit de révision (courante={conflict.CurrentRevision}).",
                    "Conflit");
                break;
            case SaveClassResult.NotDurable notDurable:
                GameDataUiMessageBox.Show(this, notDurable.Message, "Persistance");
                break;
            case SaveClassResult.PersistenceFailed persistence:
                GameDataUiMessageBox.Show(this, persistence.Error, "Erreur");
                break;
        }
    }

    private async Task DeleteAsync()
    {
        var result = await _session.DeleteCurrentAsync().ConfigureAwait(true);
        switch (result)
        {
            case DeleteClassResult.Success:
                StatusChanged?.Invoke("Supprimée");
                await RefreshListAsync().ConfigureAwait(true);
                break;
            case DeleteClassResult.NotFound:
                GameDataUiMessageBox.Show(this, "Classe introuvable.");
                break;
            case DeleteClassResult.PersistenceFailed persistence:
                GameDataUiMessageBox.Show(this, persistence.Error, "Erreur");
                break;
        }
    }

    private static NumericUpDown StatControl() => new()
    {
        Minimum = ClassDefinition.MinStat,
        Maximum = ClassDefinition.MaxStat,
        Value = 10,
        Width = 80,
    };

    private sealed record CatalogItem(Guid Id, string Label)
    {
        public override string ToString() => Label;
    }

    private sealed record SpellChoice(Guid? Id, string Label)
    {
        public override string ToString() => Label;
    }
}
