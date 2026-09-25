using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using System.Windows.Interop;
using Frog.Application.Assets;
using Frog.Application.Content;
using Frog.Application.Maps;
using Frog.Application.Playtest;
using Frog.Application.Prefabs;
using Frog.Core.Enums;
using Frog.Core.IO;
using Frog.Core.Maps;
using Frog.Core.Models;
using Frog.Editor.Assets;
using Frog.Editor.Controls;
using Frog.Editor.Dialogs;
using Frog.Editor.Enums;
using Frog.Editor.Panels;
using Frog.Editor.Ui;

using Frog.Editor.Config;
using Frog.Editor.Interop;
using Frog.Editor.Services;
using Frog.Persistence.PostgreSql;

namespace Frog.Editor.Forms;

public sealed class MainForm : Form
{
    private readonly MenuStrip? _menuStrip;
    private readonly ToolStripMenuItem? _mnuUndo;
    private readonly ToolStripMenuItem? _mnuRedo;
    private readonly ToolStripMenuItem? _mnuShowEventMarkers;
    private readonly ToolStripMenuItem? _mnuShowEventNames;
    private readonly StatusStrip? _status;
    private readonly ToolStripStatusLabel? _lblPos;
    private readonly bool _embedAsWpfChild;
    private readonly SplitContainer? _splitLeft;
    private readonly SplitContainer? _splitRight;
    private readonly ElementHost _leftToolsElementHost;
    private readonly EditorLeftToolsWpf _leftToolsWpf;
    private readonly ElementHost _tilesetPickerElementHost;
    private readonly TilesetPickerPanelWpf _tilesetPickerWpf;
    private readonly LayersProjectPanel _layersProjectPanel;
    private readonly ElementHost _layersElementHost;
    private bool _suspendLayerListEvents;
    private readonly PropertyGrid _propGrid;
    private readonly MapPropertiesBar _mapPropertiesBar;
    private readonly MapPlacedEntityPropertiesPanel _placedEntityPanel;
    private readonly TransferIssuesPanel _transferIssuesPanel;
    private readonly MapCanvas _canvas;
    private readonly MapMinimapControl _minimap;
    private Point _lastHoverTile;
    private readonly TableLayoutPanel _leftLayout;
    /// <summary>Horizontal : panneau haut = couches, bas = PropertyGrid.</summary>
    private readonly SplitContainer _splitLayersProps;
    private readonly SplitContainer _splitRightTileset;
    private readonly MapsProjectPanel _mapsProjectPanel;
    private readonly ElementHost _mapsElementHost;
    private readonly Panel _wfMapDockPanel;
    private readonly Panel _leftColumnPanel;
    private readonly Panel _mapHeader;
    private readonly Label _lblMapWorkspaceTitle;
    private System.Windows.Window? _wpfOwnerWindow;
    private MapWorkspaceSession? _workspace;
    private IMapRepository? _mapRepository;
    private MapEventsPostgreSqlService? _mapEventService;
    private MapEventsBrowseDialog? _mapEventsDialog;
    private bool _syncingMapEventOverlay;
    private Phase8ContentPostgreSqlService? _phase8ContentService;
    private PendingQuickNpc? _pendingQuickNpc;
    private string? _statusNotice;
    private List<MapTransferIssue> _transferIssues = new();
    private IReadOnlyList<MapTransferLink> _eventTransferLinks = Array.Empty<MapTransferLink>();
    private readonly Dictionary<Guid, CachedTransferMap> _transferMapCache = new();
    private EditorPostgreSqlScope? _mapDatabaseScope;
    private EditorPostgreSqlScope? _mapEventDatabaseScope;
    private EditorPostgreSqlScope? _phase8DatabaseScope;
    private bool _catalogOpenInProgress;
    private bool _suppressDirtyTracking;
    private readonly IEditorDialogService _dialogService;
    private MapRepositoryCapabilities _persistenceCapabilities = MapRepositoryCapabilities.InMemoryDemo;
    private bool _saveInProgress;
    private bool _closeConfirmed;
    private bool _propGridUndoCaptured;
    private Task? _pendingSaveOperation;
    private ToolStripMenuItem? _mnuSave;
    private ToolStripMenuItem? _mnuPublish;
    private ToolStripMenuItem? _mnuPlaytest;
    private ToolStripMenuItem? _mnuStopPlaytest;
    private EditorPlaytestProcessLauncher? _playtestLauncher;
    private PlaytestOrchestrator? _playtestOrchestrator;
    private string? _playtestReuseClientExe;
    private string? _playtestReuseServerExe;
    private CancellationTokenSource? _playtestCts;
    private bool _playtestBusy;
    private EditorMainFormCloseCoordinator? _closeCoordinator;
    private readonly TileAssetCatalogue _tileAssetCatalogue;
    private readonly TileAssetWorkbench _tileAssetWorkbench;
    private readonly Panel _sheetTilesHost;
    private readonly Button _btnPaletteSheet;
    private readonly Button _btnPaletteAsset;
    private string? _tileAssetMapPath;

    /// <summary>Colonne gauche (outils, cartes) pour hébergement dans un <c>WindowsFormsHost</c> WPF.</summary>
    internal Control LeftShellForWpf => _leftColumnPanel;

    /// <summary>Zone carte (bandeau + canevas + mini-carte).</summary>
    internal Control CenterShellForWpf => _wfMapDockPanel;

    /// <summary>Tuiles + couches + grille de propriétés.</summary>
    internal Control RightShellForWpf => _splitRightTileset;

    internal Map? GetCanvasMapForTest() => _canvas.Map;

    internal MapCanvas GetCanvasForTest() => _canvas;

    internal EditorTool GetActiveToolForTest() => _canvas.ActiveTool;

    internal Point? GetPlaytestSpawnForTest() => _canvas.PlaytestSpawnTile;

    internal IReadOnlyList<MapPlacedEntity> GetPlacedEntitiesForTest() => _canvas.PlacedEntities;

    internal MapPlacedEntity? SelectedPlacedEntityForTest => _canvas.SelectedPlacedEntity;

    internal string PlacedEntitySelectionTextForTest => _placedEntityPanel.SelectionTextForTest;

    internal void SetPlaceKindForTest(MapPlacedKind kind) => _placedEntityPanel.SetKindToPlaceForTest(kind);

    internal bool TrySetPlaytestSpawnForTest(int tileX, int tileY) =>
        PersistPlaytestSpawnFromUser(tileX, tileY);

    internal void SelectEditorToolForTest(EditorTool tool) => SelectEditorTool(tool);

    internal IReadOnlyList<PrefabPlacement> GetPrefabPlacementsForTest() => _canvas.PrefabPlacements;

    internal bool TryPlacePrefabForTest(int tileX, int tileY) => _canvas.TryApplyPrefabToolAtTileForTest(tileX, tileY);

    internal bool TryProcessCmdKeyForTest(Keys keyData)
    {
        Message msg = default;
        return ProcessCmdKey(ref msg, keyData);
    }

    internal string PrefabStatusForTest() => _leftToolsWpf.StatusTextForTest;

    internal string PrefabSelectedNameForTest() => _leftToolsWpf.SelectedNameForTest;

    internal void RestorePlaytestSpawnFromWorkstateForTest() => RestorePlaytestSpawnFromWorkstate();

    internal MapWorkspaceSession? GetWorkspaceSessionForTest() => _workspace;

    internal MapRepositoryCapabilities GetPersistenceCapabilitiesForTest() => _persistenceCapabilities;

    internal bool IsSaveInProgressForTest() => _saveInProgress;

    /// <summary>True when save/publish/init/close-cleanup must finish before WPF shell teardown.</summary>
    internal bool HasPendingEditorOperations()
        => _saveInProgress
           || _workspace?.IsSaveInProgress == true
           || _pendingSaveOperation is { IsCompleted: false }
           || IsWorkspaceInitializationPendingForTest
           || IsEditorClosingForTest();

    internal Task? PendingSaveOperationForTest => _pendingSaveOperation;

    internal bool HasUnsavedChangesForTest() => _workspace?.IsDirty == true;

    internal bool IsCloseConfirmedForTest() => _closeConfirmed;

    internal EditorMainFormCloseCoordinator? CloseCoordinatorForTest => _closeCoordinator;

    internal EditorPostgreSqlScope? MapDatabaseScopeForTest => _mapDatabaseScope;

    internal EditorPostgreSqlScope? MapEventDatabaseScopeForTest => _mapEventDatabaseScope;

    internal EditorPostgreSqlScope? Phase8DatabaseScopeForTest => _phase8DatabaseScope;

    internal MapEventsPostgreSqlService? MapEventServiceForTest => _mapEventService;

    internal Phase8ContentPostgreSqlService? Phase8ContentServiceForTest => _phase8ContentService;

    /// <summary>Attache des scopes/services pour vérifier dispose (FORCE_IN_MEMORY laisse ces champs null).</summary>
    internal void AttachScopesAndServicesForDisposeTest(
        EditorPostgreSqlScope mapScope,
        EditorPostgreSqlScope mapEventScope,
        EditorPostgreSqlScope phase8Scope,
        MapEventsPostgreSqlService mapEventService,
        Phase8ContentPostgreSqlService phase8Service)
    {
        _mapDatabaseScope = mapScope ?? throw new ArgumentNullException(nameof(mapScope));
        _mapEventDatabaseScope = mapEventScope ?? throw new ArgumentNullException(nameof(mapEventScope));
        _phase8DatabaseScope = phase8Scope ?? throw new ArgumentNullException(nameof(phase8Scope));
        _mapEventService = mapEventService ?? throw new ArgumentNullException(nameof(mapEventService));
        _phase8ContentService = phase8Service ?? throw new ArgumentNullException(nameof(phase8Service));
    }

    internal void BeginCloseCleanupViaCoordinatorForTest()
    {
        var args = new FormClosingEventArgs(CloseReason.UserClosing, cancel: false);
        _ = _closeCoordinator?.TryHandleFormClosing(args, ConfirmCloseForShutdownAsync);
    }

    internal bool IsEditorClosingForTest() => _closeCoordinator?.IsEditorClosingForTest == true;

    internal void SaveMap()
    {
        if (_saveInProgress)
        {
            return;
        }

        _pendingSaveOperation = RunSaveOperationAsync(SaveMapCoreAsync);
    }

    internal void PublishMap()
    {
        if (_saveInProgress)
        {
            return;
        }

        _pendingSaveOperation = RunSaveOperationAsync(PublishMapCoreAsync);
    }

    internal Task SaveMapCoreForTestAsync() => SaveMapCoreAsync();

    internal Task PublishMapCoreForTestAsync() => PublishMapCoreAsync();

    internal async Task<bool> TryRequestCloseAsync() => await ConfirmCloseAsync().ConfigureAwait(true);

    public bool CanExecuteSaveOrPublish()
        => _workspace is not null
           && _persistenceCapabilities.AllowsSave
           && !_saveInProgress
           && _workspace.IsSaveInProgress != true;

    internal async Task<bool> ConfirmCloseAsync()
    {
        if (_closeConfirmed || _workspace?.IsDirty != true)
        {
            return true;
        }

        var proceed = await TryDiscardOrSaveBeforeSwitchAsync().ConfigureAwait(true);
        if (proceed)
        {
            _closeConfirmed = true;
        }

        return proceed;
    }

    internal bool AreShellHostsReadyForTest() =>
        _leftColumnPanel.IsHandleCreated
        && _wfMapDockPanel.IsHandleCreated
        && _splitRightTileset.IsHandleCreated;

    internal void SetWpfOwnerWindow(System.Windows.Window window) => _wpfOwnerWindow = window;

    /// <summary>Réapplique les splits internes après redimensionnement de la coque WPF.</summary>
    internal void NotifyWpfShellLayout()
    {
        ApplyLayersPropertySplitDistance();
        ApplyRightTilesetSplitDistance();
        PositionMinimap();
    }

    /// <summary>Coordonnées tuile sous le curseur (pour barre d’état WPF).</summary>
    public event Action<string>? TileHoverStatusChanged;

    /// <summary>État annuler / rétablir (pour menu WPF).</summary>
    public event Action<bool, bool>? UndoRedoStateChanged;

    /// <summary>Session playtest démarrée ou arrêtée (boutons Tester / Arrêter).</summary>
    public event Action? PlaytestStateChanged;

    public MapUndoController UndoHistory => _canvas.History;

    /// <param name="embedAsWpfChild">Si vrai, la fenêtre est hébergée dans un <c>WindowsFormsHost</c> WPF (pas de chrome fenêtre).</param>
    public MainForm(bool embedAsWpfChild = false)
    {
        _embedAsWpfChild = embedAsWpfChild;
        Text = "MMO Maker — Éditeur";
        MinimumSize = new Size(1100, 720);
        if (embedAsWpfChild)
        {
            StartPosition = FormStartPosition.Manual;
            WindowState = FormWindowState.Normal;
            FormBorderStyle = FormBorderStyle.None;
            TopLevel = false;
        }
        else
        {
            StartPosition = FormStartPosition.CenterScreen;
            WindowState = FormWindowState.Maximized;
        }

        KeyPreview = true;
        EditorChrome.ApplyFormChrome(this);
        _dialogService = EditorTestHooks.OverrideDialogService
                         ?? new WinFormsEditorDialogService(GetDialogOwner);

        _closeCoordinator = new EditorMainFormCloseCoordinator(
            () => StopPlaytestAsync(),
            () => _workspaceInitTask,
            () => new EditorPostgreSqlScope?[] { _mapDatabaseScope, _mapEventDatabaseScope, _phase8DatabaseScope },
            DisposeWorkspaceServicesAndScopes,
            SetClosingUiState,
            () => _saveInProgress || _workspace?.IsSaveInProgress == true || (_workspaceInitTask is { IsCompleted: false }),
            () =>
            {
                if (!IsDisposed && _closeCoordinator?.AllowFinalCloseForTest == true)
                {
                    BeginInvoke(new Action(() =>
                    {
                        if (!IsDisposed)
                        {
                            Close();
                        }
                    }));
                }
            });

        FormClosing += MainForm_FormClosing;

        FormClosed += (_, _) =>
        {
            TilesetCache.Clear();
            TileAssetThumbnails.Clear();
            if (_mapEventsDialog is { IsDisposed: false } eventsDialog)
            {
                _mapEventsDialog = null;
                eventsDialog.Close();
            }
        };

        if (!embedAsWpfChild)
        {
            var menuStrip = new MenuStrip();
            EditorChrome.StyleMainMenu(menuStrip);

            var mFile = new ToolStripMenuItem("Fichier");
            mFile.DropDownItems.Add(new ToolStripMenuItem("Nouvelle carte…", null, (_, _) => CreateNewMap())
            {
                ShortcutKeys = Keys.Control | Keys.N,
                ShowShortcutKeys = true,
            });
            mFile.DropDownItems.Add(new ToolStripMenuItem("Ouvrir…", null, (_, _) => LoadMap())
            {
                ShortcutKeys = Keys.Control | Keys.O,
                ShowShortcutKeys = true,
            });
            mFile.DropDownItems.Add(new ToolStripSeparator());
            var mnuSave = new ToolStripMenuItem("Enregistrer (PostgreSQL)", null, (_, _) => SaveMap())
            {
                ShortcutKeys = Keys.Control | Keys.S,
                ShowShortcutKeys = true,
            };
            var mnuPublish = new ToolStripMenuItem("Publier (PostgreSQL)…", null, (_, _) => PublishMap());
            mFile.DropDownItems.Add(mnuSave);
            mFile.DropDownItems.Add(mnuPublish);
            _mnuSave = mnuSave;
            _mnuPublish = mnuPublish;
            mFile.DropDownItems.Add(new ToolStripMenuItem("Exporter fichier .fmap…", null, (_, _) => ExportMapToFile()));
            mFile.DropDownItems.Add(new ToolStripMenuItem("Lancer le client Frog…", null, (_, _) => LaunchFrogGameClient()));
            _mnuPlaytest = new ToolStripMenuItem("Tester (playtest)…", null, async (_, _) => await StartPlaytestAsync())
            {
                ShortcutKeys = Keys.F5 | Keys.Control,
                ToolTipText = "Enregistre la carte si besoin, puis lance le client sur la carte courante (dossiers frères, sans republier).",
            };
            _mnuStopPlaytest = new ToolStripMenuItem("Arrêter le test", null, async (_, _) => await StopPlaytestAsync())
            {
                Enabled = false,
            };
            mFile.DropDownItems.Add(_mnuPlaytest);
            mFile.DropDownItems.Add(_mnuStopPlaytest);
            mFile.DropDownItems.Add(new ToolStripSeparator());
            mFile.DropDownItems.Add("Quitter", null, (_, _) =>
            {
                if (TopLevel)
                {
                    Close();
                }
                else
                {
                    System.Windows.Application.Current.Shutdown();
                }
            });

            var mnuUndo = new ToolStripMenuItem("Annuler", null, (_, _) => DoUndo())
            {
                Enabled = false,
                ShortcutKeys = Keys.Control | Keys.Z,
                ShowShortcutKeys = true,
            };
            var mnuRedo = new ToolStripMenuItem("Rétablir", null, (_, _) => DoRedo())
            {
                Enabled = false,
                ShortcutKeys = Keys.Control | Keys.Y,
                ShowShortcutKeys = true,
            };
            var mEdit = new ToolStripMenuItem("Édition");
            mEdit.DropDownItems.Add(mnuUndo);
            mEdit.DropDownItems.Add(mnuRedo);
            mEdit.DropDownItems.Add(new ToolStripSeparator());
            mEdit.DropDownItems.Add("Copier la sélection — toutes les couches (Ctrl+C)", null, (_, _) => CopyTileSelection(false));
            mEdit.DropDownItems.Add("Couper la sélection — toutes les couches (Ctrl+X)", null, (_, _) => CutTileSelection(false));
            mEdit.DropDownItems.Add("Coller — toutes les couches (Ctrl+V)", null, (_, _) => PasteTileSelection(false));
            mEdit.DropDownItems.Add("Copier la couche active (Ctrl+Maj+C)", null, (_, _) => CopyTileSelection(true));
            mEdit.DropDownItems.Add("Couper la couche active (Ctrl+Maj+X)", null, (_, _) => CutTileSelection(true));
            mEdit.DropDownItems.Add("Coller sur la couche active (Ctrl+Maj+V)", null, (_, _) => PasteTileSelection(true));
            mEdit.DropDownItems.Add(new ToolStripSeparator());
            mEdit.DropDownItems.Add("Rotation 90° (Q)", null, (_, _) => TryRotateSelection90());
            mEdit.DropDownItems.Add("Miroir horizontal (H)", null, (_, _) => TryMirrorSelectionHorizontal());
            mEdit.DropDownItems.Add("Miroir vertical (V)", null, (_, _) => TryMirrorSelectionVertical());
            mEdit.DropDownItems.Add("Pipette tuile (I)", null, (_, _) => TryPipetteAtHover());
            mEdit.DropDownItems.Add(new ToolStripSeparator());
            mEdit.DropDownItems.Add("Enregistrer la sélection comme modèle…", null, (_, _) => SaveSelectionAsMapTemplate());
            mEdit.DropDownItems.Add("Enregistrer la carte comme modèle…", null, (_, _) => SaveCurrentMapAsTemplate());
            mEdit.DropDownItems.Add("Poser un modèle…", null, (_, _) => PromptStampMapTemplate());

            var mResources = new ToolStripMenuItem("Ressources");
            mResources.DropDownItems.Add("Charger une image tuiles…", null, (_, _) => OpenTileset());
            mResources.DropDownItems.Add("Importer une feuille TileAsset…", null, (_, _) => ImportTileAssetSheet());
            mResources.DropDownItems.Add("Importer un asset projet…", null, (_, _) => ImportProjectAsset());
            mResources.DropDownItems.Add("Animer la sélection de tuiles", null, (_, _) => MarkSelectedTilesAnimated());
            mResources.DropDownItems.Add("Retirer l’animation de la sélection", null, (_, _) => ClearSelectedTilesAnimated());

            var mMap = new ToolStripMenuItem("Carte");
            mMap.DropDownItems.Add("Valider la carte…", null, (_, _) => ValidateMap());
            mMap.DropDownItems.Add("Propriétés de la carte…", null, (_, _) => ShowMapProperties());
            mMap.DropDownItems.Add("Passer cette carte en TileAsset (v6)…", null, (_, _) => ConvertCurrentMapToTileAsset());
            mMap.DropDownItems.Add("Vérifier les transferts…", null, (_, _) => ShowTransferIssues());
            mMap.DropDownItems.Add("Outil gomme (E)", null, (_, _) => SelectEditorTool(EditorTool.Eraser));
            mMap.DropDownItems.Add("Outil remplissage (F)", null, (_, _) => SelectEditorTool(EditorTool.Fill));
            mMap.DropDownItems.Add("Outil rectangle (R)", null, (_, _) => SelectEditorTool(EditorTool.Rectangle));
            mMap.DropDownItems.Add("Outil ligne (L)", null, (_, _) => SelectEditorTool(EditorTool.Line));
            mMap.DropDownItems.Add("Outil point de départ (D)", null, (_, _) => SelectEditorTool(EditorTool.Spawn));
            mMap.DropDownItems.Add("Outil prefab / objet (P)", null, (_, _) => SelectEditorTool(EditorTool.Prefab));
            mMap.DropDownItems.Add("Outil entités (N)", null, (_, _) => SelectEditorTool(EditorTool.Place));
            mMap.DropDownItems.Add("Pipette tuile (I)", null, (_, _) => TryPipetteAtHover());
            mMap.DropDownItems.Add("Configurer warp sélectionné…", null, (_, _) => EditSelectedWarpDestination());
            mMap.DropDownItems.Add("PNJ rapide…", null, (_, _) => OpenQuickTalkingNpc());
            mMap.DropDownItems.Add("Événements carte…", null, (_, _) => BrowseMapEvents());
            mMap.DropDownItems.Add("Contenu Phase 8…", null, (_, _) => BrowsePhase8Content());
            mMap.DropDownItems.Add("Actualiser marqueurs événements", null, (_, _) => RefreshMapEventMarkers());
            mMap.DropDownItems.Add(
                new ToolStripMenuItem("Astuce : Ctrl+clic droit sur la carte = menu événements (tuile sous curseur)")
                {
                    Enabled = false,
                });

            var mView = new ToolStripMenuItem("Affichage");
            mView.DropDownItems.Add("Zoom avant", null, (_, _) => _canvas!.ZoomInTowardCenter());
            mView.DropDownItems.Add("Zoom arrière", null, (_, _) => _canvas!.ZoomOutTowardCenter());
            mView.DropDownItems.Add(new ToolStripSeparator());
            mView.DropDownItems.Add("Réinitialiser la vue (zoom 100 %)", null, (_, _) => ResetMapView());
            mView.DropDownItems.Add(new ToolStripSeparator());
            var mnuShowEventMarkers = new ToolStripMenuItem("Marqueurs événements")
            {
                CheckOnClick = true,
                Checked = true,
            };
            mView.DropDownItems.Add(mnuShowEventMarkers);
            var mnuShowEventNames = new ToolStripMenuItem("Afficher noms des événements")
            {
                CheckOnClick = true,
                Checked = true,
            };
            mView.DropDownItems.Add(mnuShowEventNames);
            var mnuAnimPreview = new ToolStripMenuItem("Aperçu des tuiles animées")
            {
                CheckOnClick = true,
                Checked = true,
            };
            mnuAnimPreview.CheckedChanged += (_, _) => AnimatedTilePreviewVisible = mnuAnimPreview.Checked;
            mView.DropDownItems.Add(mnuAnimPreview);

            menuStrip.Items.AddRange(new ToolStripItem[] { mFile, mEdit, mResources, mMap, mView });
            MainMenuStrip = menuStrip;
            _menuStrip = menuStrip;
            _mnuUndo = mnuUndo;
            _mnuRedo = mnuRedo;
            _mnuShowEventMarkers = mnuShowEventMarkers;
            _mnuShowEventNames = mnuShowEventNames;

            var status = new StatusStrip { SizingGrip = false, GripStyle = ToolStripGripStyle.Hidden, Dock = DockStyle.Bottom };
            status.BackColor = EditorChrome.RibbonBg;
            status.Font = EditorChrome.BodyFont;
            status.Padding = new Padding(8, 4, 8, 4);
            var lblPos = new ToolStripStatusLabel("Tuile · x = 0, y = 0")
            {
                BorderSides = ToolStripStatusLabelBorderSides.None,
                Spring = true,
                AutoToolTip = true,
                TextAlign = ContentAlignment.MiddleLeft,
                Font = EditorChrome.BodyFont,
            };
            lblPos.ForeColor = EditorChrome.LabelPrimary;
            status.Items.Add(lblPos);
            _status = status;
            _lblPos = lblPos;

            Shown += (_, _) =>
            {
                ApplyLayoutPercentages();
                PositionMinimap();
                BeginInvoke(new Action(RefreshMapEventMarkers));
            };
            ResizeEnd += (_, _) => ApplyLayoutPercentages();
        }
        else
        {
            _menuStrip = null;
            _mnuUndo = null;
            _mnuRedo = null;
            _mnuShowEventMarkers = null;
            _mnuShowEventNames = null;
            _status = null;
            _lblPos = null;
        }

        if (!embedAsWpfChild)
        {
            _splitLeft = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 260,
                FixedPanel = FixedPanel.Panel1,
                SplitterWidth = 6,
                BackColor = EditorChrome.CanvasInset,
            };
            _splitLeft.Panel1.BackColor = EditorChrome.SidebarBg;
            _splitLeft.Panel2.BackColor = EditorChrome.WorkspaceBg;

            _splitRight = new SplitContainer
            {
                Dock = DockStyle.Fill,
                Orientation = Orientation.Vertical,
                SplitterDistance = 760,
                FixedPanel = FixedPanel.Panel2,
                SplitterWidth = 6,
                BackColor = EditorChrome.CanvasInset,
            };
            _splitRight.Panel1.BackColor = EditorChrome.WorkspaceCenter;
            _splitRight.Panel2.BackColor = EditorChrome.SidebarBg;
        }
        else
        {
            _splitLeft = null;
            _splitRight = null;
        }

        _tileAssetCatalogue = CreateTileAssetCatalogue();
        _canvas = new MapCanvas { Dock = DockStyle.Fill, TileAssets = _tileAssetCatalogue };
        _canvas.HoveredTileChanged += OnHoveredTileChanged;
        _canvas.PaintGestureChanged += PushEditorStatusLine;
        _canvas.ViewTransformChanged += OnCanvasViewTransformChanged;
        _canvas.PlaytestSpawnChanged += OnPlaytestSpawnChanged;
        _canvas.PrefabPlacementsChanged += OnPrefabPlacementsChanged;
        _canvas.PrefabSelectionPicked += OnPrefabSelectionPicked;
        _canvas.PrefabCatalog = PrefabSpriteCache.LoadCatalog();
        EditorLocalWorkstate.TryReadLastPrefabSelection(out var lastPrefabId, out var lastFacing);
        if (!string.IsNullOrWhiteSpace(lastPrefabId))
        {
            _canvas.SelectedPrefabId = lastPrefabId;
            _canvas.SelectedPrefabFacing = lastFacing;
        }
        _canvas.TileClicked += OnTileClicked;
        _canvas.TileContextMenuRequested += OnTileContextMenuRequested;
        _canvas.MapReplaced += OnMapReplaced;
        _canvas.UndoHistoryChanged += UpdateUndoRedoButtons;
        _canvas.MapEdited += OnMapEdited;
        _canvas.BrushSampled += OnBrushSampled;

        if (_mnuShowEventMarkers is not null)
        {
            _mnuShowEventMarkers.CheckedChanged += (_, _) =>
            {
                MapEventMarkersVisible = _mnuShowEventMarkers.Checked;
            };
        }

        if (_mnuShowEventNames is not null)
        {
            _mnuShowEventNames.CheckedChanged += (_, _) =>
            {
                MapEventNamesVisible = _mnuShowEventNames.Checked;
            };
        }

        _canvas.MapEventMarkerPicked += OnCanvasMapEventMarkerPicked;
        _canvas.MapEventMarkerInteractionChanged += PushEditorStatusLine;

        _minimap = new MapMinimapControl
        {
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };
        _minimap.Attach(_canvas);

        _leftToolsWpf = new EditorLeftToolsWpf();
        _leftToolsWpf.ToolChanged += tool => SelectEditorTool(tool);
        _leftToolsWpf.FillVisibleUnlockedLayersChanged += enabled =>
        {
            _canvas.FillVisibleUnlockedLayers = enabled;
            PushEditorStatusLine();
        };
        _leftToolsWpf.FillRespectAttributesChanged += enabled =>
        {
            _canvas.FillRespectAttributes = enabled;
            PushEditorStatusLine();
        };
        _leftToolsWpf.RectangleOutlineChanged += enabled =>
        {
            _canvas.RectangleOutline = enabled;
            PushEditorStatusLine();
        };
        _leftToolsWpf.RectangleEllipseChanged += enabled =>
        {
            _canvas.RectangleEllipse = enabled;
            PushEditorStatusLine();
        };
        _leftToolsWpf.TileTypeChanged += type => _canvas.SelectedTileType = type;
        _leftToolsWpf.PrefabSelectionChanged += OnPrefabPaletteChanged;
        _leftToolsWpf.PrefabDuplicateRequested += OnDuplicateLastPrefab;
        _leftToolsWpf.PrefabEscapeRequested += () => TryHandlePrefabEscape();
        _leftToolsWpf.PipetteRequested += () => TryPipetteAtHover();
        _leftToolsWpf.BindPrefabCatalog(_canvas.PrefabCatalog, _canvas.SelectedPrefabId, _canvas.SelectedPrefabFacing);
        _leftToolsElementHost = new ElementHost
        {
            Dock = DockStyle.Top,
            AutoSize = true,
            BackColor = EditorChrome.SidebarBg,
            Margin = Padding.Empty,
            Child = _leftToolsWpf,
        };

        _mapsProjectPanel = new MapsProjectPanel();
        _mapsProjectPanel.CatalogMapOpenRequested += (_, mapId) => _ = OpenCatalogMapAsync(mapId);
        _mapsElementHost = new ElementHost
        {
            Dock = DockStyle.Fill,
            BackColor = EditorChrome.SidebarBg,
            Margin = Padding.Empty,
            Child = _mapsProjectPanel,
        };

        _tilesetPickerWpf = new TilesetPickerPanelWpf();
        _tilesetPickerWpf.SelectedTilesetChanged += id => _canvas.ActiveTilesetId = id;
        _tilesetPickerWpf.LoadTilesetsRequested += OpenTileset;
        _tilesetPickerWpf.StampSelectionChanged += OnPaletteStampChanged;
        _tilesetPickerWpf.AnimStatusChanged += message =>
        {
            _statusNotice = message;
            PushEditorStatusLine();
        };
        _tilesetPickerWpf.SyncPaletteTileSize(_canvas.TileSize);
        _tilesetPickerElementHost = new ElementHost
        {
            Dock = DockStyle.Fill,
            BackColor = EditorChrome.SidebarBg,
            Margin = Padding.Empty,
            Child = _tilesetPickerWpf,
        };

        _sheetTilesHost = new Panel { Dock = DockStyle.Fill, BackColor = EditorChrome.SidebarBg, Padding = new Padding(0) };
        _sheetTilesHost.Controls.Add(_tilesetPickerElementHost);
        _tileAssetWorkbench = new TileAssetWorkbench(_tileAssetCatalogue) { Visible = false };
        _tileAssetWorkbench.BrushTileChosen += id =>
        {
            _canvas.ActiveTileAssetId = id;
            _canvas.SelectedStampInTiles = new Size(1, 1);
            _canvas.Invalidate();
        };
        _canvas.TileAssetSampled += _tileAssetWorkbench.SelectTile;
        _btnPaletteSheet = new Button
        {
            Text = "Feuille",
            Dock = DockStyle.Left,
            Width = 110,
            FlatStyle = FlatStyle.Flat,
        };
        _btnPaletteAsset = new Button
        {
            Text = "TileAsset",
            Dock = DockStyle.Left,
            Width = 110,
            FlatStyle = FlatStyle.Flat,
        };
        _btnPaletteSheet.Click += (_, _) => ShowPaletteMode(tileAsset: false);
        _btnPaletteAsset.Click += (_, _) => ShowPaletteMode(tileAsset: true);
        ShowPaletteMode(tileAsset: false);
        var paletteModeBar = new Panel
        {
            Dock = DockStyle.Top,
            Height = 36,
            Padding = new Padding(8, 4, 8, 4),
            BackColor = EditorChrome.SidebarBg,
        };
        paletteModeBar.Controls.Add(_btnPaletteAsset);
        paletteModeBar.Controls.Add(_btnPaletteSheet);
        var paletteBody = new Panel { Dock = DockStyle.Fill, BackColor = EditorChrome.SidebarBg };
        paletteBody.Controls.Add(_tileAssetWorkbench);
        paletteBody.Controls.Add(_sheetTilesHost);
        var tilesHost = new Panel { Dock = DockStyle.Fill, BackColor = EditorChrome.SidebarBg, Padding = new Padding(0) };
        tilesHost.Controls.Add(paletteBody);
        tilesHost.Controls.Add(paletteModeBar);

        _leftLayout = new TableLayoutPanel
        {
            Dock = DockStyle.Fill,
            ColumnCount = 1,
            RowCount = 2,
            Padding = new Padding(2, 10, 2, 12),
            BackColor = EditorChrome.SidebarBg,
        };
        _leftLayout.RowStyles.Add(new RowStyle(SizeType.AutoSize));
        _leftLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

        var mapsHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10, 4, 10, 10), BackColor = EditorChrome.SidebarBg };
        mapsHost.Controls.Add(_mapsElementHost);

        _leftLayout.Controls.Add(_leftToolsElementHost, 0, 0);
        _leftLayout.Controls.Add(mapsHost, 0, 1);

        _leftColumnPanel = new Panel { Dock = DockStyle.Fill, BackColor = EditorChrome.SidebarBg };
        _leftColumnPanel.Controls.Add(_leftLayout);
        _splitLeft?.Panel1.Controls.Add(_leftColumnPanel);

        _mapHeader = new Panel { Dock = DockStyle.Top, Height = 33, BackColor = EditorChrome.RibbonBg };
        _lblMapWorkspaceTitle = new Label
        {
            Dock = DockStyle.Fill,
            TextAlign = ContentAlignment.MiddleLeft,
            Padding = new Padding(14, 0, 8, 0),
            ForeColor = EditorChrome.LabelPrimary,
            Font = EditorChrome.SectionFont,
            BackColor = Color.Transparent,
        };
        var mapAccent = new Panel { Dock = DockStyle.Bottom, Height = 3, BackColor = EditorChrome.RibbonAccent };
        _mapHeader.Controls.Add(_lblMapWorkspaceTitle);
        _mapHeader.Controls.Add(mapAccent);
        _wfMapDockPanel = new Panel
        {
            Dock = DockStyle.Fill,
            BackColor = EditorChrome.CanvasInset,
            Padding = new Padding(10, 0, 10, 12),
        };
        // Ordre de docking : d’abord le bandeau (Top), puis le canevas (Fill), sinon le Fill « mange » tout et le bandeau bleu se superpose mal.
        _wfMapDockPanel.Controls.Add(_mapHeader);
        _wfMapDockPanel.Controls.Add(_canvas);
        _wfMapDockPanel.Controls.Add(_minimap);
        _minimap.BringToFront();
        _wfMapDockPanel.Resize += (_, _) => PositionMinimap();
        _splitRight?.Panel1.Controls.Add(_wfMapDockPanel);
        PositionMinimap();

        _splitRightTileset = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterWidth = 6,
            FixedPanel = FixedPanel.None,
            // Minimums bas : au 1er layout la hauteur du split peut être petite ; SplitterDistance est appliquée plus tard.
            Panel1MinSize = 48,
            Panel2MinSize = 48,
            BackColor = EditorChrome.CanvasInset,
        };
        _splitRightTileset.Panel1.BackColor = EditorChrome.SidebarBg;
        _splitRightTileset.Panel2.BackColor = EditorChrome.SidebarBg;
        _splitRightTileset.Panel1.Controls.Add(tilesHost);

        _splitLayersProps = new SplitContainer
        {
            Dock = DockStyle.Fill,
            Orientation = Orientation.Horizontal,
            SplitterWidth = 6,
            BackColor = EditorChrome.CanvasInset,
        };
        _splitLayersProps.Panel2.BackColor = EditorChrome.SidebarBg;
        var layersHost = new Panel
        {
            Dock = DockStyle.Fill,
            Padding = new Padding(8, 14, 10, 12),
            BackColor = EditorChrome.SidebarBg,
        };
        _layersProjectPanel = new LayersProjectPanel();
        _layersProjectPanel.LayerSelected += (_, ix) =>
        {
            if (_suspendLayerListEvents)
            {
                return;
            }

            _canvas.ActiveLayerIndex = ix;
            _canvas.Invalidate();
            PushEditorStatusLine();
        };
        _layersProjectPanel.LayerVisibilityChanged += (_, t) =>
        {
            if (_suspendLayerListEvents || _canvas.Map is null)
            {
                return;
            }

            if (t.index < 0 || t.index >= _canvas.Map.Layers.Count)
            {
                return;
            }

            if (!_suppressDirtyTracking && _canvas.Map is not null)
            {
                _canvas.History.PushBeforeChange(_canvas.Map);
                _canvas.Map.Layers[t.index].Visible = t.visible;
                OnMapEdited();
                UpdateUndoRedoButtons();
            }
            else if (_canvas.Map is not null)
            {
                _canvas.Map.Layers[t.index].Visible = t.visible;
            }
            _canvas.Invalidate();
            PushEditorStatusLine();
        };
        _layersProjectPanel.RenameLayerRequested += (_, _) => RenameLayerDisplay();
        _layersProjectPanel.AddLayerRequested = AddLayer;
        _layersProjectPanel.RemoveLayerRequested = RemoveLayer;
        _layersProjectPanel.ChangeEngineTypeRequested = ChangeLayerEngineType;
        _layersProjectPanel.ToggleLockRequested = ToggleLayerLock;
        _layersElementHost = new ElementHost
        {
            Dock = DockStyle.Fill,
            BackColor = EditorChrome.SidebarBg,
            Child = _layersProjectPanel,
        };
        layersHost.Controls.Add(_layersElementHost);

        _splitLayersProps.Panel1.Controls.Add(layersHost);

        _propGrid = new PropertyGrid { Dock = DockStyle.Fill, HelpVisible = false };
        EditorChrome.StylePropertyGrid(_propGrid);
        _propGrid.Font = EditorChrome.BodyFont;
        _propGrid.SelectedObjectsChanged += (_, _) => _propGridUndoCaptured = false;
        _propGrid.MouseDown += (_, _) =>
        {
            if (_propGridUndoCaptured || _suppressDirtyTracking || _canvas.Map is null)
            {
                return;
            }

            _canvas.History.PushBeforeChange(_canvas.Map);
            _propGridUndoCaptured = true;
        };
        _propGrid.PropertyValueChanged += (_, _) =>
        {
            if (_suppressDirtyTracking || _canvas.Map is null)
            {
                return;
            }

            if (_propGrid.SelectedObject is Map map)
            {
                MapEditOperations.ClampDimensions(map);
                MapEditOperations.ClipTilesOutsideBounds(map);
                _canvas.ClipPlacedEntitiesToMap();
                EditorMapPlacedEntityWorkstate.Write(_workspace?.CurrentMapId, map, _canvas.PlacedEntities);
                UpdateMapChromeLabels();
                _propGrid.Refresh();
            }

            OnMapEdited();
            UpdateUndoRedoButtons();
            RefreshLayersUi();
            PushEditorStatusLine();
            _propGridUndoCaptured = false;
        };
        _mapsProjectPanel.CurrentMapNodeSelected += (_, _) =>
        {
            if (_propGrid.SelectedObject is not Map && _canvas.Map is not null)
            {
                _propGrid.SelectedObject = _canvas.Map;
            }
        };
        _transferIssuesPanel = new TransferIssuesPanel { Dock = DockStyle.Bottom, Height = 132 };
        _transferIssuesPanel.IssueActivated += FocusTransferIssue;
        _mapPropertiesBar = new MapPropertiesBar { Dock = DockStyle.Top };
        _mapPropertiesBar.EditRequested += (_, _) => ShowMapProperties();
        _placedEntityPanel = new MapPlacedEntityPropertiesPanel { Dock = DockStyle.Top };
        _placedEntityPanel.PlaceKindChanged += (_, _) =>
        {
            _canvas.PlaceKind = _placedEntityPanel.KindToPlace;
            if (_canvas.ActiveTool != EditorTool.Place)
            {
                SelectEditorTool(EditorTool.Place);
            }
            else
            {
                _canvas.Invalidate();
                PushEditorStatusLine();
            }
        };
        _placedEntityPanel.Edited += (_, _) => ApplyPlacedEntityProperties();
        _placedEntityPanel.DeleteRequested += (_, _) => _canvas.TryRemoveSelectedPlacedEntity();
        _placedEntityPanel.RosterPicked += (_, id) => _canvas.SelectPlacedEntity(id);
        _canvas.PlaceKind = _placedEntityPanel.KindToPlace;
        _canvas.PlacedEntitiesChanged += OnPlacedEntitiesChanged;
        _canvas.PlacedEntitySelectionChanged += OnPlacedEntitySelectionChanged;
        var propsBody = new Panel { Dock = DockStyle.Fill, BackColor = EditorChrome.SidebarBg };
        propsBody.Controls.Add(_propGrid);
        propsBody.Controls.Add(_placedEntityPanel);
        propsBody.Controls.Add(_mapPropertiesBar);
        var propsHost = new Panel { Dock = DockStyle.Fill, BackColor = EditorChrome.SidebarBg };
        propsHost.Controls.Add(_transferIssuesPanel);
        propsHost.Controls.Add(propsBody);
        _splitLayersProps.Panel2.Controls.Add(propsHost);

        _splitRightTileset.Panel2.Controls.Add(_splitLayersProps);
        _splitRightTileset.HandleCreated += (_, _) =>
        {
            if (IsHandleCreated)
            {
                BeginInvoke(new Action(ApplyRightTilesetSplitDistance));
            }
            else
            {
                ApplyRightTilesetSplitDistance();
            }
        };
        _splitRight?.Panel2.Controls.Add(_splitRightTileset);
        if (_splitLeft is not null && _splitRight is not null)
        {
            _splitLeft.Panel2.Controls.Add(_splitRight);
            _splitLeft.Dock = DockStyle.Fill;
        }

        if (!embedAsWpfChild && _menuStrip is not null && _status is not null)
        {
            // Ordre d’ancrage WinForms : bas (status), milieu (fill), haut (menu) pour réserver correctement l’espace sous le MenuStrip.
            _menuStrip.Dock = DockStyle.Top;
            _status.Dock = DockStyle.Bottom;
            Controls.Add(_status);
            Controls.Add(_splitLeft!);
            Controls.Add(_menuStrip);
        }
        else if (!embedAsWpfChild)
        {
            Controls.Add(_splitLeft!);
        }

        // Placeholder jusqu’à InitializeWorkspaceAsync (session catalogue + carte démo).
        var map = DemoMapFactory.CreateStarter();
        _canvas.Map = map;
        _propGrid.SelectedObject = _canvas.Map;
        RefreshLayersUi();
        UpdateUndoRedoButtons();
        RefreshTilesetList();
        SyncMapsTree();
        UpdateMapChromeLabels();
        RefreshTransferWarnings();
    }

    private Task? _workspaceInitTask;
    internal Task WorkspaceInitializationTask => _workspaceInitTask ?? Task.CompletedTask;

    internal bool IsWorkspaceInitializationPendingForTest =>
        _workspaceInitTask is { IsCompleted: false };

    internal bool CoordinatedShutdownAttemptedForTest { get; private set; }

    internal async Task<bool> TryCoordinatedShutdownAsync()
    {
        CoordinatedShutdownAttemptedForTest = true;
        if (_closeCoordinator is null || _closeCoordinator.AllowFinalCloseForTest)
        {
            return true;
        }

        var timeout = EditorTestHooks.GameDataCloseCleanupTimeoutForTest ?? TimeSpan.FromSeconds(30);
        return await _closeCoordinator.TryShutdownWithoutCloseAsync(ConfirmCloseForShutdownAsync, timeout)
            .ConfigureAwait(true);
    }

    /// <summary>Initialise le catalogue (PostgreSQL ou mémoire) et ouvre la carte démo.</summary>
    internal async System.Threading.Tasks.Task InitializeWorkspaceAsync()
    {
        if (_workspaceInitTask is { IsCompleted: false })
        {
            await _workspaceInitTask.ConfigureAwait(true);
            return;
        }

        _workspaceInitTask = InitializeWorkspaceCoreAsync();
        await _workspaceInitTask.ConfigureAwait(true);
    }

    private async System.Threading.Tasks.Task InitializeWorkspaceCoreAsync(CancellationToken cancellationToken = default)
    {
        _closeCoordinator?.BeginWorkspaceInitialization();
        var initToken = _closeCoordinator?.WorkspaceInitToken ?? cancellationToken;
        using var linked = CancellationTokenSource.CreateLinkedTokenSource(initToken, cancellationToken);

        if (EditorTestHooks.MainWorkspaceInitBarrierForTest is { } initBarrier)
        {
            await initBarrier("map", linked.Token).ConfigureAwait(true);
        }

        var bundle = await EditorMapRepositoryFactory.CreateBundleAsync(linked.Token).ConfigureAwait(true);
        linked.Token.ThrowIfCancellationRequested();
        _mapRepository = bundle.Repository;
        _persistenceCapabilities = bundle.Capabilities;
        _mapDatabaseScope = bundle.DatabaseScope;

        if (EditorTestHooks.MainWorkspaceInitBarrierForTest is { } mapEventBarrier)
        {
            await mapEventBarrier("mapEvent", linked.Token).ConfigureAwait(true);
        }

        var eventBundle = await EditorMapEventRepositoryFactory.CreateBundleAsync(linked.Token).ConfigureAwait(true);
        linked.Token.ThrowIfCancellationRequested();
        _mapEventService = eventBundle.Service;
        _mapEventDatabaseScope = eventBundle.DatabaseScope;

        if (EditorTestHooks.MainWorkspaceInitBarrierForTest is { } phase8Barrier)
        {
            await phase8Barrier("phase8", linked.Token).ConfigureAwait(true);
        }

        var phase8Bundle = await EditorPhase8ContentRepositoryFactory.CreateBundleAsync(linked.Token).ConfigureAwait(true);
        linked.Token.ThrowIfCancellationRequested();
        _phase8ContentService = phase8Bundle.Service;
        _phase8DatabaseScope = phase8Bundle.DatabaseScope;

        if (EditorTestHooks.MainWorkspaceInitBarrierForTest is { } workspaceBarrier)
        {
            await workspaceBarrier("workspace", linked.Token).ConfigureAwait(true);
        }

        _workspace = new MapWorkspaceSession(bundle.Repository);
        await _workspace.InitializeAsync().ConfigureAwait(true);
        ApplyWorkspaceMapToUi();
        await HydrateTilesetCacheFromPublishedAsync().ConfigureAwait(true);
        UpdatePersistenceMenuState();
        RestoreSavedPrefabSelection();
        PushEditorStatusLine();
    }

    private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_closeCoordinator is null)
        {
            return;
        }

        if (_closeCoordinator.TryHandleFormClosing(e, ConfirmCloseForShutdownAsync))
        {
            return;
        }
    }

    private async Task<bool> ConfirmCloseForShutdownAsync()
    {
        if (_closeConfirmed || _closeCoordinator?.AllowFinalCloseForTest == true)
        {
            return true;
        }

        if (_workspace?.IsDirty == true)
        {
            var proceed = await TryDiscardOrSaveBeforeSwitchAsync().ConfigureAwait(true);
            if (!proceed)
            {
                return false;
            }
        }

        _closeConfirmed = true;
        return true;
    }

    private void DisposeWorkspaceServicesAndScopes()
    {
        _mapEventService?.Dispose();
        _mapEventService = null;
        _phase8ContentService?.Dispose();
        _phase8ContentService = null;
        _mapEventDatabaseScope?.Dispose();
        _mapEventDatabaseScope = null;
        _phase8DatabaseScope?.Dispose();
        _phase8DatabaseScope = null;
        _mapDatabaseScope?.Dispose();
        _mapDatabaseScope = null;
    }

    private void SetClosingUiState(bool enabled)
    {
        if (_menuStrip is not null)
        {
            _menuStrip.Enabled = enabled;
        }

        if (_mnuSave is not null)
        {
            _mnuSave.Enabled = enabled;
        }

        if (_mnuPublish is not null)
        {
            _mnuPublish.Enabled = enabled;
        }

        if (_canvas is not null)
        {
            _canvas.Enabled = enabled;
        }
    }

    internal async System.Threading.Tasks.Task RefreshMapCatalogAsync()
    {
        if (_workspace is null)
        {
            await InitializeWorkspaceAsync().ConfigureAwait(true);
            return;
        }

        await _workspace.RefreshCatalogAsync().ConfigureAwait(true);
        SyncMapsTree();
        PushEditorStatusLine();
    }

    private async System.Threading.Tasks.Task OpenCatalogMapAsync(Guid mapId)
    {
        if (_workspace is null || _catalogOpenInProgress)
        {
            return;
        }

        if (_workspace.CurrentMapId == mapId)
        {
            return;
        }

        _catalogOpenInProgress = true;
        try
        {
            if (_workspace.IsDirty && !await TryDiscardOrSaveBeforeSwitchAsync().ConfigureAwait(true))
            {
                return;
            }

            if (!await _workspace.OpenMapAsync(mapId).ConfigureAwait(true))
            {
                MessageBox.Show(GetDialogOwner(), $"Carte {mapId} introuvable dans le catalogue.", "Monde", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            ApplyWorkspaceMapToUi();
            await HydrateTilesetCacheFromPublishedAsync().ConfigureAwait(true);
        }
        finally
        {
            _catalogOpenInProgress = false;
        }
    }

    private void ApplyWorkspaceMapToUi()
    {
        if (_workspace?.CurrentMap is null)
        {
            return;
        }

        _suppressDirtyTracking = true;
        try
        {
            _canvas.ClearHistory();
            _canvas.Map = _workspace.CurrentMap;
            _canvas.DefaultWarpTargetMapId = _workspace.CurrentMapId;
            _propGrid.SelectedObject = _canvas.Map;
            RefreshLayersUi();
            _canvas.Invalidate();
            UpdateUndoRedoButtons();
            SyncMapsTree();
            UpdateMapChromeLabels();
            RefreshMapEventMarkers();
            RestorePlaytestSpawnFromWorkstate();
            RestorePrefabPlacementsFromWorkstate();
            RestorePlacedEntitiesFromWorkstate();
            SyncPaletteModeToMap();
        }
        finally
        {
            _suppressDirtyTracking = false;
        }

        PushEditorStatusLine();
    }

    private async System.Threading.Tasks.Task HydrateTilesetCacheFromPublishedAsync()
    {
        if (_canvas.Map is not { } map)
        {
            return;
        }

        try
        {
            var bundle = EditorTilesetRepositoryFactory.CreateBundle();
            var images = new CompositePublishedTilesetImageSource(
                EmbeddedPublishedTilesetImageSource.Instance,
                new ProjectAssetTilesetImageSource(
                    EditorTestHooks.OverrideProjectAssetRoot ?? ProjectAssetRoot.Resolve()));
            var files = await PublishedTilesetCacheHydrator
                .CollectPngFilesAsync(map, bundle.PublishedCatalog, images)
                .ConfigureAwait(true);
            foreach (var file in files)
            {
                try
                {
                    TilesetCache.LoadFromPngBytesAtId(file.PngBytes, file.Id);
                }
                catch
                {
                    // PNG illisible — le manifeste fichier peut encore compléter
                }
            }

            if (TilesetCache.ListRegistered().Count > 0 && _canvas.ActiveTilesetId <= 0)
            {
                _canvas.ActiveTilesetId = TilesetCache.ListRegistered()[0].Id;
            }

            RefreshTilesetList();
            _canvas.Invalidate();
        }
        catch
        {
            // hydrate optionnel — ne bloque pas l’ouverture de carte
        }
    }

    private async System.Threading.Tasks.Task SyncPublishedTilesetsFromCacheAsync()
    {
        if (_canvas.Map is null)
        {
            return;
        }

        try
        {
            var bundle = EditorTilesetRepositoryFactory.CreateBundle();
            if (!bundle.Capabilities.AllowsSave)
            {
                return;
            }

            var files = TilesetCache.SnapshotPngFiles();
            if (files.Count == 0)
            {
                return;
            }

            await MapPublishedTilesetSync
                .PublishUsedAsync(bundle.Repository, _canvas.Map, files)
                .ConfigureAwait(true);
        }
        catch
        {
            // publication tileset best-effort — la carte reste publiable
        }
    }

    private bool _suppressSpawnPersist;
    private bool _suppressPrefabPersist;
    private bool _suppressPlacedPersist;
    private EditorTool _toolBeforePrefab = EditorTool.Brush;
    private int _prefabEscapeTick;

    internal void RefreshShapePreview() => _canvas.RefreshShapePreviewForTest();

    internal bool TryCancelShapeGesture() => _canvas.TryCancelShapeGesture();

    internal void SelectEditorTool(EditorTool tool)
    {
        if (tool == EditorTool.Prefab && _canvas.ActiveTool != EditorTool.Prefab)
        {
            _toolBeforePrefab = _canvas.ActiveTool;
        }

        _leftToolsWpf.SetSelectedTool(tool);
        _leftToolsWpf.SetPrefabPlaceMode(tool == EditorTool.Prefab);
        _canvas.ActiveTool = tool;
        _canvas.Invalidate();
        PushEditorStatusLine();
    }

    internal bool IsPrefabSearchFocused => _leftToolsWpf.IsPrefabSearchFocused;

    /// <summary>
    /// Échap : vide le filtre s’il a le focus, sinon quitte le placement prefab.
    /// Retourne false si rien de spécifique au prefab n’a été consommé.
    /// </summary>
    internal bool TryHandlePrefabEscape()
    {
        var now = Environment.TickCount;
        if (_prefabEscapeTick != 0 && unchecked(now - _prefabEscapeTick) < 80)
        {
            return true;
        }

        if (_leftToolsWpf.TryConsumePrefabFilterEscape())
        {
            _prefabEscapeTick = now;
            return true;
        }

        if (!TryCancelPrefabPlaceMode())
        {
            return false;
        }

        _prefabEscapeTick = now;
        return true;
    }

    private bool TryCancelPrefabPlaceMode()
    {
        if (_canvas.ActiveTool != EditorTool.Prefab)
        {
            return false;
        }

        var restore = _toolBeforePrefab == EditorTool.Prefab ? EditorTool.Brush : _toolBeforePrefab;
        SelectEditorTool(restore);
        return true;
    }

    internal void CopyTileSelection(bool activeLayerOnly) => _canvas.TryCopyTileSelection(activeLayerOnly);

    internal void CutTileSelection(bool activeLayerOnly) => _canvas.TryCutTileSelection(activeLayerOnly);

    internal void PasteTileSelection(bool activeLayerOnly) => _canvas.TryPasteAtHover(activeLayerOnly);

    public void SaveSelectionAsMapTemplate()
    {
        if (_canvas.Map is null)
        {
            _dialogService.ShowInfo("Aucune carte chargée.", "Modèle");
            return;
        }

        if (!_canvas.TryGetCommittedSelectionBounds(out var rect))
        {
            _dialogService.ShowInfo("Tracez une sélection (outil M), ou enregistrez la carte entière.", "Modèle");
            return;
        }

        PromptAndSaveMapTemplate(rect);
    }

    public void SaveCurrentMapAsTemplate()
    {
        if (_canvas.Map is not { } map)
        {
            _dialogService.ShowInfo("Aucune carte chargée.", "Modèle");
            return;
        }

        PromptAndSaveMapTemplate(new Rectangle(0, 0, map.Width, map.Height));
    }

    public void PromptStampMapTemplate()
    {
        if (!EditorMapTemplateStore.TryLoad(out var library, out var loadError))
        {
            _dialogService.ShowError(loadError ?? "Le fichier de modèles est illisible.", "Modèle");
            return;
        }

        if (library.Templates.Count == 0)
        {
            _dialogService.ShowInfo("Aucun modèle enregistré. Enregistrez la sélection ou la carte depuis le menu Édition.", "Modèle");
            return;
        }

        using var dialog = new MapTemplateListDialog(library.Templates);
        if (dialog.ShowDialog(GetDialogOwner()) != DialogResult.OK || dialog.Selected is not { } selected)
        {
            return;
        }

        if (dialog.Choice == MapTemplateListChoice.Delete)
        {
            if (!_dialogService.ConfirmYesNo($"Supprimer le modèle « {selected.Name} » ?", "Modèle"))
            {
                return;
            }

            if (!library.TryRemove(selected.Name, out var removeError)
                || !library.TryWrite(EditorMapTemplateStore.DirectoryPath(), out removeError))
            {
                _dialogService.ShowWarning(removeError ?? "Suppression impossible.", "Modèle");
                return;
            }

            _statusNotice = $"Modèle « {selected.Name} » supprimé.";
            PushEditorStatusLine();
            return;
        }

        if (_canvas.Map is null)
        {
            _dialogService.ShowInfo("Aucune carte chargée.", "Modèle");
            return;
        }

        var anchor = dialog.Choice == MapTemplateListChoice.Origin
            ? new Point(0, 0)
            : _canvas.HoveredTile;
        if (!TryStampMapTemplate(selected, anchor.X, anchor.Y, out var error))
        {
            _dialogService.ShowWarning(error ?? "Pose impossible.", "Modèle");
        }
    }

    internal bool TrySaveMapTemplateForTest(Rectangle rect, string name, out string? error)
    {
        if (_canvas.Map is null)
        {
            error = "Aucune carte chargée.";
            return false;
        }

        return TrySaveMapTemplate(rect, name, confirmReplace: true, out error);
    }

    internal bool TryStampNamedTemplateForTest(string name, int anchorX, int anchorY, out string? error)
    {
        if (!EditorMapTemplateStore.TryLoad(out var library, out error))
        {
            return false;
        }

        var template = library.FindByName(name);
        if (template is null)
        {
            error = "Modèle introuvable.";
            return false;
        }

        return TryStampMapTemplate(template, anchorX, anchorY, out error);
    }

    private void PromptAndSaveMapTemplate(Rectangle rect)
    {
        var suggested = string.IsNullOrWhiteSpace(_canvas.Map?.Name) ? "Modèle" : _canvas.Map.Name.Trim();
        var name = SimpleInputDialog.Show(GetDialogOwner(), "Enregistrer un modèle", "Nom du modèle", suggested);
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        if (!TrySaveMapTemplate(rect, name, confirmReplace: false, out var error) && error is not null)
        {
            _dialogService.ShowWarning(error, "Modèle");
        }
    }

    private bool TrySaveMapTemplate(Rectangle rect, string name, bool confirmReplace, out string? error)
    {
        if (_canvas.Map is not { } map)
        {
            error = "Aucune carte chargée.";
            return false;
        }

        if (!EditorMapTemplateStore.TryLoad(out var library, out error))
        {
            return false;
        }

        if (!confirmReplace && library.FindByName(name) is not null
            && !_dialogService.ConfirmYesNo($"Remplacer le modèle « {name.Trim()} » ?", "Modèle"))
        {
            error = null;
            return false;
        }

        if (!MapStampTemplateOperations.TryCapture(
                map,
                rect.Left,
                rect.Top,
                rect.Width,
                rect.Height,
                _canvas.PrefabPlacements,
                name,
                out var template,
                out error)
            || template is null
            || !library.TryUpsert(template, out error)
            || !library.TryWrite(EditorMapTemplateStore.DirectoryPath(), out error))
        {
            return false;
        }

        _statusNotice = MapStampTemplateOperations.FormatSaved(template);
        PushEditorStatusLine();
        return true;
    }

    private bool TryStampMapTemplate(MapStampTemplate template, int anchorX, int anchorY, out string? error)
    {
        if (!_canvas.TryApplyMapTemplate(template, anchorX, anchorY, out var status, out error))
        {
            return false;
        }

        _statusNotice = status;
        PushEditorStatusLine();
        return true;
    }

    internal bool TryRotateSelection90()
        => _canvas.TryTransformSelection(TileSelectionTransformKind.Rotate90Clockwise);

    internal bool TryMirrorSelectionHorizontal()
        => _canvas.TryTransformSelection(TileSelectionTransformKind.MirrorHorizontal);

    internal bool TryMirrorSelectionVertical()
        => _canvas.TryTransformSelection(TileSelectionTransformKind.MirrorVertical);

    internal bool TryPipetteAtHover()
        => _canvas.TryPipetteAtHover(switchToBrush: true);

    private void OnPlaytestSpawnChanged(Point tile)
    {
        if (!_suppressSpawnPersist && _canvas.Map is { } map)
        {
            EditorMapSpawnWorkstate.Write(_workspace?.CurrentMapId, map, tile.X, tile.Y);
        }

        _leftToolsWpf.SetSpawnDisplay(tile.X, tile.Y);
        RefreshMapPropertiesBar();
        PushEditorStatusLine();
    }

    private void OnPlacedEntitiesChanged()
    {
        if (!_suppressPlacedPersist && _canvas.Map is { } map)
        {
            EditorMapPlacedEntityWorkstate.Write(_workspace?.CurrentMapId, map, _canvas.PlacedEntities);
        }

        SyncPlacedEntityPanel();
        PushEditorStatusLine();
    }

    private void OnPlacedEntitySelectionChanged()
    {
        SyncPlacedEntityPanel();
        PushEditorStatusLine();
    }

    private void SyncPlacedEntityPanel()
    {
        _placedEntityPanel.Sync(_canvas.PlacedEntities, _canvas.SelectedPlacedEntity, _canvas.PlaceKind);
    }

    private void ApplyPlacedEntityProperties()
    {
        if (!_placedEntityPanel.TryReadEdit(out var kind, out var name, out var notes, out var facing, out var respawn, out var level))
        {
            return;
        }

        if (_canvas.TryUpdateSelectedPlacedEntity(kind, name, notes, facing, respawn, level, out var error))
        {
            _placedEntityPanel.ShowError(null);
            return;
        }

        _placedEntityPanel.ShowError(error);
    }

    private void RestorePlacedEntitiesFromWorkstate()
    {
        if (_canvas.Map is not { } map)
        {
            return;
        }

        _suppressPlacedPersist = true;
        try
        {
            if (EditorMapPlacedEntityWorkstate.TryRead(_workspace?.CurrentMapId, map, out var entities))
            {
                _canvas.ReplacePlacedEntities(entities);
            }
            else
            {
                _canvas.ReplacePlacedEntities(Array.Empty<MapPlacedEntity>());
            }
        }
        finally
        {
            _suppressPlacedPersist = false;
        }
    }

    private void RestorePlaytestSpawnFromWorkstate()
    {
        if (_canvas.Map is not { } map)
        {
            return;
        }

        _suppressSpawnPersist = true;
        try
        {
            if (EditorMapSpawnWorkstate.TryRead(_workspace?.CurrentMapId, map, out var x, out var y))
            {
                _canvas.TrySetPlaytestSpawn(x, y);
            }
            else
            {
                _canvas.ClearPlaytestSpawn();
            }

            var spawn = _canvas.PlaytestSpawnTile;
            _leftToolsWpf.SetSpawnDisplay(spawn?.X, spawn?.Y);
            RefreshMapPropertiesBar();
        }
        finally
        {
            _suppressSpawnPersist = false;
        }
    }

    private bool PersistPlaytestSpawnFromUser(int tileX, int tileY)
    {
        if (!_canvas.TrySetPlaytestSpawn(tileX, tileY))
        {
            return false;
        }

        if (_canvas.Map is { } map)
        {
            EditorMapSpawnWorkstate.Write(_workspace?.CurrentMapId, map, tileX, tileY);
        }

        return true;
    }

    private void OnPrefabPaletteChanged(string prefabId, PrefabFacing facing)
    {
        _canvas.SelectedPrefabId = prefabId;
        _canvas.SelectedPrefabFacing = facing;
        EditorLocalWorkstate.WriteLastPrefabSelection(prefabId, facing);
        _canvas.Invalidate();
        PushEditorStatusLine();
    }

    /// <summary>
    /// Réapplique le dernier prefab mémorisé après le chargement de la carte.
    /// La liste WPF peut sinon rester sur le premier objet (Canapé) alors que le canevas a déjà l’id restauré.
    /// </summary>
    private void RestoreSavedPrefabSelection()
    {
        EditorLocalWorkstate.TryReadLastPrefabSelection(out var id, out var facing);
        if (string.IsNullOrWhiteSpace(id))
        {
            id = _canvas.SelectedPrefabId;
            facing = _canvas.SelectedPrefabFacing;
        }

        if (string.IsNullOrWhiteSpace(id))
        {
            return;
        }

        if (!PrefabPlacementService.TryGetDefinition(_canvas.PrefabCatalog, id, out _))
        {
            _canvas.PrefabCatalog = MapPrefabPersistDocument.MergeCatalogs(
                _canvas.PrefabCatalog,
                BuiltInPrefabCatalog.Create());
        }

        if (!PrefabPlacementService.TryGetDefinition(_canvas.PrefabCatalog, id, out _))
        {
            return;
        }

        _canvas.SelectedPrefabId = id;
        _canvas.SelectedPrefabFacing = facing;
        _leftToolsWpf.BindPrefabCatalog(_canvas.PrefabCatalog, id, facing);
    }

    private void OnPrefabSelectionPicked(string prefabId, PrefabFacing facing)
    {
        _leftToolsWpf.SetPrefabSelection(prefabId, facing);
        EditorLocalWorkstate.WriteLastPrefabSelection(prefabId, facing);
        PushEditorStatusLine();
    }

    private void AttachCurrentPrefabsToWorkspace()
    {
        if (_workspace is null)
        {
            return;
        }

        var catalog = _canvas.PrefabCatalog ?? PrefabSpriteCache.LoadCatalog();
        var sprites = PrefabSpriteCache.SnapshotPngFiles(catalog);
        _workspace.CurrentPrefabs = MapPrefabPersistDocument.CreateMerged(
            catalog,
            _canvas.PrefabPlacements,
            sprites,
            _workspace.CurrentPrefabs);
    }

    private void OnPrefabPlacementsChanged()
    {
        _leftToolsWpf.SetCanDuplicateLastPrefab(_canvas.PrefabPlacements.Count > 0);
        if (_suppressPrefabPersist)
        {
            PushEditorStatusLine();
            return;
        }

        _workspace?.MarkDirty();
        if (_workspace?.CurrentPrefabs is { } persisted)
        {
            persisted.Placements = PrefabPlacementService.ClonePlacements(_canvas.PrefabPlacements);
        }

        if (_canvas.Map is { } map)
        {
            EditorMapPrefabWorkstate.Write(_workspace?.CurrentMapId, map, _canvas.PrefabPlacements);
        }

        PushEditorStatusLine();
    }

    private void RestorePrefabPlacementsFromWorkstate()
    {
        if (_canvas.Map is not { } map)
        {
            return;
        }

        var workstatePresent = EditorMapPrefabWorkstate.TryRead(_workspace?.CurrentMapId, map, out var workstatePlacements);
        var persisted = _workspace?.CurrentPrefabs;
        var source = PrefabEditorRestore.Choose(
            _workspace?.IsDirty == true,
            workstatePresent,
            persisted is not null);

        _suppressPrefabPersist = true;
        try
        {
            switch (source)
            {
                case PrefabEditorRestore.Source.UnsavedWorkstate:
                    _canvas.ReplacePrefabPlacements(workstatePlacements);
                    break;
                case PrefabEditorRestore.Source.PersistedPackage:
                    HydratePersistedPrefabCatalog(persisted!);
                    _canvas.ReplacePrefabPlacements(persisted!.Placements);
                    EditorMapPrefabWorkstate.Write(_workspace!.CurrentMapId, map, _canvas.PrefabPlacements);
                    break;
                case PrefabEditorRestore.Source.DiskWorkstate:
                    _canvas.ReplacePrefabPlacements(workstatePlacements);
                    break;
                default:
                    _canvas.ReplacePrefabPlacements(Array.Empty<PrefabPlacement>());
                    break;
            }
        }
        finally
        {
            _suppressPrefabPersist = false;
        }
    }

    private void HydratePersistedPrefabCatalog(MapPrefabPersistDocument document)
    {
        _canvas.PrefabCatalog = MapPrefabPersistDocument.MergeCatalogs(document.Catalog, _canvas.PrefabCatalog);
        var prefabsDir = PrefabSpriteCache.ResolvePrefabsDirectory();
        Directory.CreateDirectory(prefabsDir);
        foreach (var sprite in document.ToSpriteFiles())
        {
            var path = Path.Combine(prefabsDir, sprite.FileName);
            if (!File.Exists(path) && sprite.PngBytes.Length > 0)
            {
                File.WriteAllBytes(path, sprite.PngBytes);
            }
        }

        _leftToolsWpf.BindPrefabCatalog(_canvas.PrefabCatalog, _canvas.SelectedPrefabId, _canvas.SelectedPrefabFacing);
    }

    internal void CycleSelectedPrefabFacingForTest(bool next) => CycleSelectedPrefabFacing(next);

    private void CycleSelectedPrefabFacing(bool next)
    {
        var facing = next
            ? PrefabPlacementService.NextFacing(_canvas.SelectedPrefabFacing)
            : PrefabPlacementService.PreviousFacing(_canvas.SelectedPrefabFacing);
        _canvas.SelectedPrefabFacing = facing;
        _leftToolsWpf.SetPrefabSelection(_canvas.SelectedPrefabId, facing);
        EditorLocalWorkstate.WriteLastPrefabSelection(_canvas.SelectedPrefabId, facing);
        _canvas.Invalidate();
        PushEditorStatusLine();
    }

    private void OnDuplicateLastPrefab()
    {
        if (_canvas.TryDuplicateLastPrefab(out var placed, out var error) && placed is not null)
        {
            _leftToolsWpf.SetPrefabActionMessage($"Copie posée en ({placed.TileX}, {placed.TileY}).");
            PushEditorStatusLine();
            return;
        }

        _leftToolsWpf.SetPrefabActionMessage(error ?? "Duplication impossible.");
    }

    private void PersistCurrentPlaytestSpawnUnderMapId(Guid? mapId)
    {
        if (_canvas.PlaytestSpawnTile is not { } spawn || _canvas.Map is not { } map)
        {
            return;
        }

        EditorMapSpawnWorkstate.Write(mapId ?? _workspace?.CurrentMapId, map, spawn.X, spawn.Y);
    }

    internal void PersistCurrentPlaytestSpawnUnderMapIdForTest(Guid mapId) =>
        PersistCurrentPlaytestSpawnUnderMapId(mapId);

    internal (int X, int Y) ResolvePlaytestDialogDefaultsForTest()
    {
        if (_canvas.Map is not { } map)
        {
            return (0, 0);
        }

        int? storedX = null;
        int? storedY = null;
        if (EditorMapSpawnWorkstate.TryRead(_workspace?.CurrentMapId, map, out var memoX, out var memoY))
        {
            storedX = memoX;
            storedY = memoY;
        }
        else if (_canvas.PlaytestSpawnTile is { } canvasSpawn)
        {
            storedX = canvasSpawn.X;
            storedY = canvasSpawn.Y;
        }

        return MapPlaytestSpawn.ResolvePreferred(map, storedX, storedY, _lastHoverTile.X, _lastHoverTile.Y);
    }

    internal void SetHoverTileForTest(int x, int y) => _lastHoverTile = new Point(x, y);

    private void OnPaletteStampChanged(Rectangle stampPixels)
    {
        _canvas.SelectedSrc = stampPixels.Location;
        var ts = Math.Max(1, _canvas.TileSize);
        _canvas.SelectedStampInTiles = new Size(
            Math.Max(1, stampPixels.Width / ts),
            Math.Max(1, stampPixels.Height / ts));
    }

    private void OnBrushSampled(MapCanvas.BrushSample sample)
    {
        _leftToolsWpf.SetSelectedTileType(sample.Type);
        if (sample.SwitchToBrush)
        {
            SelectEditorTool(EditorTool.Brush);
        }

        _canvas.ActiveTilesetId = sample.TilesetId;
        if (!_tilesetPickerWpf.TrySelectTilesetById(sample.TilesetId))
        {
            _tilesetPickerWpf.SetPaletteTileset(sample.TilesetId);
        }

        var ts = Math.Max(1, _canvas.TileSize);
        _tilesetPickerWpf.TrySetStampPixels(new Point(sample.SrcX, sample.SrcY), new Size(ts, ts));
        PushEditorStatusLine();
    }

    private void OnHoveredTileChanged(Point p)
    {
        _lastHoverTile = p;
        PushEditorStatusLine();
    }

    private void OnCanvasViewTransformChanged()
    {
        PushEditorStatusLine();
    }

    private void PushEditorStatusLine()
    {
        var zoomPct = (int)Math.Round(_canvas.Zoom * 100f);
        var backend = _persistenceCapabilities.DisplayLabel;
        var busy = _saveInProgress || _workspace?.IsSaveInProgress == true ? "    ·    enregistrement…" : "";
        var rev = _workspace is null
            ? ""
            : _workspace.CurrentMapId is Guid id
                ? $"    ·    carte {id.ToString("N")[..8]} r{_workspace.CurrentRevision}{FormatStatusSuffix()}"
                : "    ·    brouillon local";
        var dirty = _workspace?.IsDirty == true ? "    ·    modifié" : "";
        var spawn = _canvas.PlaytestSpawnTile is { } sp
            ? $"    ·    départ ({sp.X},{sp.Y})"
            : "";
        var prefabCount = _canvas.PrefabPlacements.Count > 0
            ? $"    ·    prefabs {_canvas.PrefabPlacements.Count}"
            : "";
        var prefabPlace = _canvas.ActiveTool == EditorTool.Prefab
            ? $"    ·    {_leftToolsWpf.SelectedPrefabSummary} — clic pour placer"
            : "";
        var placedCount = _canvas.PlacedEntities.Count > 0
            ? $"    ·    entités {_canvas.PlacedEntities.Count}"
            : "";
        var eventCaption = _canvas.ActiveMapEventCaption;
        var eventKind = _canvas.ActiveMapEventTriggerLabel;
        var eventText = string.IsNullOrEmpty(eventCaption)
            ? ""
            : string.IsNullOrEmpty(eventKind)
                ? $"    ·    événement {eventCaption}"
                : $"    ·    événement {eventCaption} · {eventKind}";
        var paintHint = _canvas.GetPaintStatusHint();
        _leftToolsWpf?.SetLiveToolHint(paintHint);
        var toolHint = _canvas.ActiveTool == EditorTool.Prefab
            ? ""
            : $"    ·    {paintHint}";
        var animToggle = !TilesetAnimCatalog.PreviewEnabled
                         && paintHint.IndexOf("aperçu", StringComparison.OrdinalIgnoreCase) < 0
            ? "    ·    aperçu des tuiles animées : arrêté"
            : "";
        var transferCount = _transferIssues.Count;
        var transferText = transferCount == 0
            ? ""
            : transferCount == 1
                ? "    ·    1 transfert à corriger"
                : $"    ·    {transferCount} transferts à corriger";
        var notice = string.IsNullOrEmpty(_statusNotice) ? "" : _statusNotice + "    ·    ";
        var text =
            $"{notice}Tuile · x = {_lastHoverTile.X}, y = {_lastHoverTile.Y}{FormatActiveLayerStatus()}{toolHint}{animToggle}    ·    Zoom {zoomPct} %{rev}{dirty}{busy}{spawn}{prefabCount}{prefabPlace}{placedCount}{eventText}{transferText}    ·    catalogue {backend}";
        if (_lblPos is not null)
        {
            _lblPos.Text = text;
            _lblPos.ToolTipText = text;
            _lblPos.ForeColor = transferCount > 0 ? EditorChrome.WarningAmber : EditorChrome.LabelPrimary;
        }

        TileHoverStatusChanged?.Invoke(text);
    }

    private string FormatActiveLayerStatus()
    {
        if (_canvas.Map is not { Layers.Count: > 0 } map)
        {
            return "";
        }

        var index = Math.Clamp(_canvas.ActiveLayerIndex, 0, map.Layers.Count - 1);
        var layer = map.Layers[index];
        var rank = LayerTypeLabels.StackRank(index, map.Layers.Count);
        var hidden = layer.Visible ? "" : " · masquée";
        var locked = layer.Locked ? " · verrouillée" : "";
        var rankText = string.IsNullOrEmpty(rank) ? "" : $" · {rank}";
        return $"    ·    peinture {layer.GetDisplayLabel()}{rankText}{hidden}{locked}";
    }

    internal void ResetMapView() => _canvas.ResetViewTransform();

    internal void EditorZoomIn() => _canvas.ZoomInTowardCenter();

    internal void EditorZoomOut() => _canvas.ZoomOutTowardCenter();

    private void SyncMapsTree()
    {
        if (_workspace is not null)
        {
            _mapsProjectPanel.RefreshCatalog(
                _workspace.Catalog,
                _workspace.CurrentMapId,
                _workspace.CurrentMapId is null ? _workspace.CurrentMap?.Name ?? _canvas.Map?.Name : null);
            return;
        }

        _mapsProjectPanel.RefreshFromMap(_canvas.Map?.Name);
    }

    private static TileAssetCatalogue CreateTileAssetCatalogue()
    {
        var catalogue = new TileAssetCatalogue();
        try
        {
            var store = TileAssetCatalogue.DefaultStoreDirectory();
            catalogue.StoreDirectory = store;
            catalogue.LoadFromDirectory(store);
        }
        catch (Exception)
        {
            var store = catalogue.StoreDirectory;
            catalogue = new TileAssetCatalogue { StoreDirectory = store };
        }

        catalogue.EnsureDefaultWorkingTileset();
        return catalogue;
    }

    private void ShowPaletteMode(bool tileAsset)
    {
        _sheetTilesHost.Visible = !tileAsset;
        _tileAssetWorkbench.Visible = tileAsset;
        _btnPaletteSheet.BackColor = tileAsset ? EditorChrome.SidebarElevated : EditorChrome.PrimaryButtonBg;
        _btnPaletteAsset.BackColor = tileAsset ? EditorChrome.PrimaryButtonBg : EditorChrome.SidebarElevated;
        _btnPaletteSheet.ForeColor = EditorChrome.LabelPrimary;
        _btnPaletteAsset.ForeColor = Color.White;
        _btnPaletteSheet.FlatStyle = FlatStyle.Flat;
        _btnPaletteAsset.FlatStyle = FlatStyle.Flat;
    }

    private void SyncPaletteModeToMap()
    {
        ShowPaletteMode(TileAssetMapEditing.IsTileAssetMap(_canvas.Map));
    }

    internal void ImportTileAssetSheet()
    {
        ShowPaletteMode(tileAsset: true);
        _tileAssetWorkbench.PromptImport();
    }

    internal void ConvertCurrentMapToTileAsset()
    {
        if (_canvas.Map is null)
        {
            return;
        }

        if (_canvas.Map.GraphicIdentity != TileGraphicIdentity.TileAsset)
        {
            var blocked = _canvas.Map.Layers.SelectMany(layer => layer.Tiles)
                .Any(tile => tile.TilesetId != 0 || tile.SrcX != 0 || tile.SrcY != 0 || !tile.AssetId.IsNone);
            if (blocked)
            {
                _dialogService.ShowWarning(
                    "Cette carte contient des coordonnées de feuille (Src). Elle reste en v5. Ré-auteur les tuiles en TileAsset ; pas de conversion automatique.",
                    "TileAsset");
                return;
            }

            _canvas.History.PushBeforeChange(_canvas.Map);
        }

        if (!TileAssetMapEditing.TryAdoptTileAssetIdentity(_canvas.Map, out var error))
        {
            _dialogService.ShowWarning(error ?? "Conversion impossible.", "TileAsset");
            return;
        }

        _canvas.SelectedStampInTiles = new Size(1, 1);
        _canvas.Invalidate();
        ShowPaletteMode(tileAsset: true);
        UpdateMapChromeLabels();
        OnMapEdited();
        _statusNotice = "Carte en TileAsset (v6, 48 px). Le pinceau pose des TileAssetId.";
        PushEditorStatusLine();
    }

    private void UpdateMapChromeLabels()
    {
        if (_canvas.Map is null)
        {
            _lblMapWorkspaceTitle.Text = "Carte : —";
            RefreshMapPropertiesBar();
            return;
        }

        var identity = _canvas.Map.GraphicIdentity == TileGraphicIdentity.TileAsset
            ? "    ·    TileAsset v6 · 48 px"
            : "    ·    feuille v5";
        _lblMapWorkspaceTitle.Text =
            $"Carte : {_canvas.Map.Name}    ({_canvas.Map.Width} × {_canvas.Map.Height} tuiles){identity}";
        _mapsProjectPanel.UpdateCurrentMapDisplayName(_canvas.Map.Name);
        RefreshMapPropertiesBar();
    }

    private void RefreshMapPropertiesBar()
    {
        if (_mapPropertiesBar is null)
        {
            return;
        }

        _mapPropertiesBar.Bind(_canvas.Map, _canvas.PlaytestSpawnTile);
    }

    private int GetSelectedLayerIndex() => _layersProjectPanel.GetSelectedLayerIndex();

    private void RefreshTilesetList()
    {
        var selId = GetSelectedTilesetId();
        _tilesetPickerWpf.ApplyEntries(TilesetCache.ListRegistered().ToList(), selId);
    }

    private int GetSelectedTilesetId()
    {
        if (_tilesetPickerWpf.TryGetSelectedTilesetId() is { } id)
        {
            return id;
        }

        return _canvas.ActiveTilesetId > 0 ? _canvas.ActiveTilesetId : 0;
    }

    protected override bool ProcessCmdKey(ref Message msg, Keys keyData)
    {
        var code = keyData & Keys.KeyCode;
        var ctrl = (keyData & Keys.Control) == Keys.Control;

        if (EditorTextInputFocus.ShouldIgnoreToolHotkeys(ActiveControl))
        {
            return base.ProcessCmdKey(ref msg, keyData);
        }

        if (!ctrl && code == Keys.Escape)
        {
            if (TryHandlePrefabEscape() || CancelQuickNpcPlacement(userInitiated: true))
            {
                return true;
            }

            if (_canvas.TryCancelShapeGesture())
            {
                return true;
            }

            _canvas.ClearSelection();
            return true;
        }

        if (_leftToolsWpf.IsPrefabSearchFocused)
        {
            return base.ProcessCmdKey(ref msg, keyData);
        }

        if (EditorToolHotkeys.TryResolve(keyData, out var tool))
        {
            SelectEditorTool(tool);
            return true;
        }

        if (!ctrl && code is Keys.OemOpenBrackets or Keys.Oem6)
        {
            CycleSelectedPrefabFacing(next: code == Keys.Oem6);
            return true;
        }

        if (!ctrl && code == Keys.I && _canvas.ActiveTool == EditorTool.Prefab)
        {
            _canvas.TryPipettePrefabAt(_canvas.HoveredTile.X, _canvas.HoveredTile.Y);
            return true;
        }

        if (_canvas.HandleEditorShortcuts(keyData))
        {
            return true;
        }

        if (ctrl && (code == Keys.Oemplus || code == Keys.Add))
        {
            _canvas.ZoomInTowardCenter();
            return true;
        }

        if (ctrl && (code == Keys.OemMinus || code == Keys.Subtract))
        {
            _canvas.ZoomOutTowardCenter();
            return true;
        }

        if (_embedAsWpfChild && ctrl)
        {
            if (code == Keys.Z)
            {
                DoUndo();
                return true;
            }

            if (code == Keys.Y)
            {
                DoRedo();
                return true;
            }

            if (code == Keys.N)
            {
                CreateNewMap();
                return true;
            }

            if (code == Keys.O)
            {
                LoadMap();
                return true;
            }

            if (code == Keys.S)
            {
                SaveMap();
                return true;
            }
        }

        return base.ProcessCmdKey(ref msg, keyData);
    }

    private void OnTileClicked(Tile? tile)
    {
        _propGrid.SelectedObject = tile ?? (object?)_canvas.Map;
    }

    internal void ShowMapProperties()
    {
        if (_canvas.Map is null)
        {
            _dialogService.ShowInfo("Aucune carte chargée.", "Propriétés de la carte");
            return;
        }

        using var dlg = new MapPropertiesDialog(_canvas.Map, _canvas.PlaytestSpawnTile);
        if (dlg.ShowDialog(GetDialogOwner()) != DialogResult.OK)
        {
            return;
        }

        var applied = MapEditOperations.TryApplyProperties(
            _canvas.Map,
            dlg.PendingEdit,
            out var error,
            () => _canvas.History.PushBeforeChange(_canvas.Map));
        if (!string.IsNullOrEmpty(error))
        {
            _dialogService.ShowWarning(error, "Propriétés de la carte");
            return;
        }

        if (!applied)
        {
            return;
        }

        ReconcileSpawnMemoAfterMapMetaChange();
        _canvas.ClipPlacedEntitiesToMap();
        if (_canvas.Map is { } placedMap)
        {
            EditorMapPlacedEntityWorkstate.Write(_workspace?.CurrentMapId, placedMap, _canvas.PlacedEntities);
        }

        _canvas.Invalidate();
        UpdateMapChromeLabels();
        OnMapEdited();
        UpdateUndoRedoButtons();
        if (_propGrid.SelectedObject is Map)
        {
            _propGrid.Refresh();
        }

        PushEditorStatusLine();
    }

    private void ReconcileSpawnMemoAfterMapMetaChange()
    {
        if (_canvas.Map is not { } map || _canvas.PlaytestSpawnTile is not { } spawn)
        {
            return;
        }

        if (spawn.X < 0 || spawn.Y < 0 || spawn.X >= map.Width || spawn.Y >= map.Height)
        {
            _canvas.TrySetPlaytestSpawn(spawn.X, spawn.Y);
            return;
        }

        EditorMapSpawnWorkstate.Write(_workspace?.CurrentMapId, map, spawn.X, spawn.Y);
    }

    internal string MapPropertiesSummaryForTest => _mapPropertiesBar.SummaryForTest;

    internal void ValidateMap()
    {
        if (_canvas.Map is null)
        {
            MessageBox.Show(GetDialogOwner(), "Aucune carte chargée.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (_canvas.Map.Validate(out var err))
        {
            MessageBox.Show(GetDialogOwner(), "Carte valide (dimensions, couches, tuiles, warps).", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }
        else
        {
            MessageBox.Show(GetDialogOwner(), err ?? "Erreur inconnue.", "Validation", MessageBoxButtons.OK, MessageBoxIcon.Warning);
        }
    }

    private string FormatStatusSuffix()
    {
        if (_workspace is null)
        {
            return string.Empty;
        }

        return _workspace.CurrentStatus == MapPublishStatus.Published ? " publiée" : " brouillon";
    }

    private void OnMapEdited()
    {
        if (_suppressDirtyTracking)
        {
            return;
        }

        _workspace?.MarkDirty();
        RefreshTransferWarnings();
    }

    private void OnMapReplaced()
    {
        CancelQuickNpcPlacement(userInitiated: false);
        RefreshLayersUi();
        _propGrid.SelectedObject = _canvas.Map;
        UpdateUndoRedoButtons();
        UpdateMapChromeLabels();
        OnMapEdited();
    }

    internal void DoUndo()
    {
        _canvas.PerformUndo();
        RefreshLayersUi();
        _propGrid.Refresh();
        UpdateUndoRedoButtons();
        PushEditorStatusLine();
    }

    internal void DoRedo()
    {
        _canvas.PerformRedo();
        RefreshLayersUi();
        _propGrid.Refresh();
        UpdateUndoRedoButtons();
        PushEditorStatusLine();
    }

    private void UpdateUndoRedoButtons()
    {
        if (_mnuUndo is not null)
        {
            _mnuUndo.Enabled = _canvas.History.CanUndo;
        }

        if (_mnuRedo is not null)
        {
            _mnuRedo.Enabled = _canvas.History.CanRedo;
        }

        UndoRedoStateChanged?.Invoke(_canvas.History.CanUndo, _canvas.History.CanRedo);
    }

    private void RefreshLayersUi()
    {
        _suspendLayerListEvents = true;
        try
        {
            var rows = new List<LayerListRow>();
            if (_canvas.Map is null)
            {
                _layersProjectPanel.ApplyRows(rows, -1);
                return;
            }

            var layerCount = _canvas.Map.Layers.Count;
            foreach (var i in LayerTypeLabels.TopFirstIndices(layerCount))
            {
                var l = _canvas.Map.Layers[i];
                rows.Add(new LayerListRow
                {
                    Index = i,
                    Visible = l.Visible,
                    Display = l.GetDisplayLabel(),
                    OrderHint = LayerTypeLabels.OrderHint(i, layerCount, l.LayerType, l.DisplayName),
                    EngineType = LayerTypeLabels.French(l.LayerType),
                    LockLabel = LayerTypeLabels.LockCaption(l.Locked),
                });
            }

            var want = rows.Count > 0
                ? Math.Clamp(_canvas.ActiveLayerIndex, 0, rows.Count - 1)
                : -1;
            _layersProjectPanel.ApplyRows(rows, want);
        }
        finally
        {
            _suspendLayerListEvents = false;
        }
    }

    private void AddLayer()
    {
        if (_canvas.Map is null)
        {
            return;
        }

        _canvas.History.PushBeforeChange(_canvas.Map);
        _canvas.Map.Layers.Add(new Layer { LayerType = LayerType.Ground });
        RefreshLayersUi();
        _canvas.Invalidate();
        UpdateUndoRedoButtons();
        OnMapEdited();
    }

    private void RemoveLayer()
    {
        var ix = GetSelectedLayerIndex();
        if (_canvas.Map is null || ix < 0)
        {
            return;
        }

        _canvas.History.PushBeforeChange(_canvas.Map);
        _canvas.Map.Layers.RemoveAt(ix);
        _canvas.ActiveLayerIndex = Math.Clamp(_canvas.ActiveLayerIndex, 0, Math.Max(0, _canvas.Map.Layers.Count - 1));
        RefreshLayersUi();
        _canvas.Invalidate();
        UpdateUndoRedoButtons();
        OnMapEdited();
    }

    private void RenameLayerDisplay()
    {
        var ix = GetSelectedLayerIndex();
        if (ix < 0 || _canvas.Map is null)
        {
            return;
        }

        var layer = _canvas.Map.Layers[ix];
        var current = string.IsNullOrWhiteSpace(layer.DisplayName) ? layer.GetDisplayLabel() : layer.DisplayName;
        var input = SimpleInputDialog.Show(GetDialogOwner(), "Nom affiché", "Libellé dans la liste (vide = nom du type moteur) :", current);
        if (input is null)
        {
            return;
        }

        _canvas.History.PushBeforeChange(_canvas.Map);
        layer.DisplayName = input.Trim();
        RefreshLayersUi();
        _canvas.Invalidate();
        UpdateUndoRedoButtons();
        OnMapEdited();
    }

    private void ChangeLayerEngineType()
    {
        var ix = GetSelectedLayerIndex();
        if (ix < 0 || _canvas.Map is null)
        {
            return;
        }

        var layer = _canvas.Map.Layers[ix];
        var input = SimpleInputDialog.Show(
            GetDialogOwner(),
            "Type de couche",
            "Sol, Masque, Masque 2, Frange, Frange 2 ou Attributs :",
            LayerTypeLabels.French(layer.LayerType));
        if (string.IsNullOrWhiteSpace(input))
        {
            return;
        }

        if (!LayerTypeLabels.TryParse(input, out var type))
        {
            MessageBox.Show(GetDialogOwner(), "Type de couche non reconnu.", "Type de couche", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _canvas.History.PushBeforeChange(_canvas.Map);
        layer.LayerType = type;
        RefreshLayersUi();
        _canvas.Invalidate();
        UpdateUndoRedoButtons();
        OnMapEdited();
    }

    private void ToggleLayerLock()
    {
        var ix = GetSelectedLayerIndex();
        if (ix < 0 || _canvas.Map is null)
        {
            return;
        }

        _canvas.History.PushBeforeChange(_canvas.Map);
        _canvas.Map.Layers[ix].Locked = !_canvas.Map.Layers[ix].Locked;
        RefreshLayersUi();
        _canvas.Invalidate();
        UpdateUndoRedoButtons();
        OnMapEdited();
    }

    internal void CreateNewMap() => _ = CreateNewMapCoreAsync();

    private async System.Threading.Tasks.Task CreateNewMapCoreAsync()
    {
        if (_workspace?.IsDirty == true && !await TryDiscardOrSaveBeforeSwitchAsync().ConfigureAwait(true))
        {
            return;
        }

        using var dlg = new NewMapDialog();
        if (dlg.ShowDialog(GetDialogOwner()) != DialogResult.OK)
        {
            return;
        }

        var map = dlg.UseTileAsset
            ? TileAssetMapEditing.CreateMap(dlg.MapName, dlg.MapWidth, dlg.MapHeight)
            : new Map { Width = dlg.MapWidth, Height = dlg.MapHeight, Name = dlg.MapName };
        if (!dlg.UseTileAsset)
        {
            map.Layers.Add(new Layer { LayerType = LayerType.Ground });
        }

        _tileAssetMapPath = null;
        _workspace?.AdoptLocalDraft(map);
        _canvas.DefaultWarpTargetMapId = null;
        _canvas.ClearHistory();
        _canvas.Map = map;
        _propGrid.SelectedObject = map;
        RefreshLayersUi();
        _canvas.Invalidate();
        UpdateUndoRedoButtons();
        SyncMapsTree();
        UpdateMapChromeLabels();
        RefreshMapEventMarkers();
        RestorePlaytestSpawnFromWorkstate();
        RestorePrefabPlacementsFromWorkstate();
        RestorePlacedEntitiesFromWorkstate();
        SyncPaletteModeToMap();
        PushEditorStatusLine();
    }

    internal void OpenTileset()
    {
        string? picked = null;
        if (!string.IsNullOrWhiteSpace(EditorTestHooks.OverrideImportSourcePath))
        {
            picked = EditorTestHooks.OverrideImportSourcePath;
        }
        else
        {
            using var ofd = new OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp;*.gif;*.webp" };
            if (ofd.ShowDialog(GetDialogOwner()) != DialogResult.OK)
            {
                return;
            }

            picked = ofd.FileName;
        }

        LoadTilesetFromPath(picked);
    }

    internal void ImportProjectAsset()
    {
        if (!GameDataAssetImport.TryPickAndImport(GetDialogOwner(), ProjectAssetKind.Tiles, out var imported))
        {
            if (!string.IsNullOrWhiteSpace(imported.Error) && imported.Error != "Annulé.")
            {
                _dialogService.ShowWarning(imported.Error, "Import asset");
            }

            return;
        }

        if (!string.IsNullOrWhiteSpace(imported.AbsolutePath))
        {
            LoadTilesetFromPath(imported.AbsolutePath);
        }
    }

    internal int LoadTilesetFromPath(string path)
    {
        var toLoad = path;
        if (!IsUnderProjectAssetRoot(path))
        {
            var imported = GameDataAssetImport.ImportFromPath(path, ProjectAssetKind.Tiles);
            if (imported.Success && !string.IsNullOrWhiteSpace(imported.AbsolutePath))
            {
                toLoad = imported.AbsolutePath;
            }
        }

        var id = TilesetCache.LoadFromFile(toLoad);
        TilesetAnimCatalog.TryAttachImageSidecar(id, path);
        if (!string.Equals(path, toLoad, StringComparison.OrdinalIgnoreCase))
        {
            TilesetAnimCatalog.TryAttachImageSidecar(id, toLoad);
        }

        _canvas.ActiveTilesetId = id;
        RefreshTilesetList();
        return id;
    }

    private static bool IsUnderProjectAssetRoot(string path)
    {
        try
        {
            var root = EditorTestHooks.OverrideProjectAssetRoot ?? ProjectAssetRoot.Resolve();
            var full = Path.GetFullPath(path);
            var rootFull = Path.GetFullPath(root);
            if (!rootFull.EndsWith(Path.DirectorySeparatorChar))
            {
                rootFull += Path.DirectorySeparatorChar;
            }

            return full.StartsWith(rootFull, StringComparison.OrdinalIgnoreCase);
        }
        catch
        {
            return false;
        }
    }

    private async System.Threading.Tasks.Task RunSaveOperationAsync(Func<System.Threading.Tasks.Task> operation)
    {
        if (_saveInProgress)
        {
            return;
        }

        _saveInProgress = true;
        UpdatePersistenceMenuState();
        PushEditorStatusLine();
        try
        {
            if (EditorTestHooks.MainFormSaveBarrierForTest is { } saveBarrier)
            {
                await saveBarrier("save", _closeCoordinator?.PendingOperationsToken ?? CancellationToken.None)
                    .ConfigureAwait(true);
            }

            await operation().ConfigureAwait(true);
        }
        catch (OperationCanceledException)
        {
            // Close cancelled the pending save — expected for P8-I1 cooperative drain.
        }
        catch (Exception ex)
        {
            _dialogService.ShowError(
                $"Erreur inattendue pendant l’enregistrement : {ex.Message}\nLa carte modifiée reste en mémoire.",
                "Enregistrement échoué");
        }
        finally
        {
            _saveInProgress = false;
            UpdatePersistenceMenuState();
            PushEditorStatusLine();
            _pendingSaveOperation = null;
        }
    }

    internal void ExportMapToFile()
    {
        if (_canvas.Map is null)
        {
            return;
        }

        using var sfd = new SaveFileDialog { Filter = "Frog Map|*.fmap" };
        if (sfd.ShowDialog(GetDialogOwner()) != DialogResult.OK)
        {
            return;
        }

        var bytes = TileAssetMapEditing.WriteEditorMap(_canvas.Map);
        File.WriteAllBytes(sfd.FileName, bytes);
        var tileAsset = TileAssetMapEditing.IsTileAssetMap(_canvas.Map);
        if (tileAsset)
        {
            _tileAssetMapPath = sfd.FileName;
            SavePrefabSidecarNextToMap(sfd.FileName);
        }
        else
        {
            SaveTilesetManifestNextToMap(sfd.FileName);
            SavePrefabSidecarNextToMap(sfd.FileName);
            TilesetAnimCatalog.WriteMapSidecars(
                sfd.FileName,
                Path.GetDirectoryName(sfd.FileName),
                TilesetCache.ListRegistered().Select(entry => entry.Id));
        }

        MessageBox.Show(
            GetDialogOwner(),
            tileAsset
                ? "Carte TileAsset enregistrée (format v6, tuiles 48 px). Le fichier stocke des TileAssetId, pas la position dans la palette."
                : "Carte, PNG tileset, manifeste (.tilesets.json), animations (.anims.json) et sidecar prefabs (.prefabs.json) exportés.",
            "Export",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
    }

    private async System.Threading.Tasks.Task SaveMapCoreAsync()
    {
        if (_canvas.Map is null)
        {
            _dialogService.ShowInfo("Aucune carte chargée.", "Enregistrement");
            return;
        }

        if (TileAssetMapEditing.IsTileAssetMap(_canvas.Map))
        {
            SaveTileAssetMapFile(promptIfMissing: _tileAssetMapPath is null);
            return;
        }

        if (_workspace is null)
        {
            _dialogService.ShowInfo("Catalogue non initialisé.", "Enregistrement");
            return;
        }

        if (!_persistenceCapabilities.AllowsSave)
        {
            _dialogService.ShowWarning(
                "Cette session n’est pas persistante. Configurez PostgreSQL pour enregistrer durablement.",
                "Enregistrement indisponible");
            return;
        }

        if (!_canvas.Map.Validate(out var err))
        {
            _dialogService.ShowWarning(err ?? "Carte invalide.", "Enregistrement");
            return;
        }

        AttachCurrentPrefabsToWorkspace();
        var result = await _workspace.SaveCurrentAsync(SaveMapIntent.SaveDraft).ConfigureAwait(true);
        await HandleSaveResultAsync(result, published: false).ConfigureAwait(true);
    }

    private async System.Threading.Tasks.Task PublishMapCoreAsync()
    {
        if (_canvas.Map is null)
        {
            _dialogService.ShowInfo("Aucune carte chargée.", "Publication");
            return;
        }

        if (TileAssetMapEditing.IsTileAssetMap(_canvas.Map))
        {
            _dialogService.ShowInfo(
                "La publication PostgreSQL des cartes TileAsset n’est pas dans cette phase. Le fichier .fmap v6 (TileAssetId, 48 px) est la sauvegarde.",
                "Publication");
            SaveTileAssetMapFile(promptIfMissing: true);
            return;
        }

        if (_workspace is null)
        {
            _dialogService.ShowInfo("Catalogue non initialisé.", "Publication");
            return;
        }

        if (!_persistenceCapabilities.AllowsSave)
        {
            _dialogService.ShowWarning(
                "Cette session n’est pas persistante. Configurez PostgreSQL pour publier durablement.",
                "Publication indisponible");
            return;
        }

        if (!_canvas.Map.Validate(out var err))
        {
            _dialogService.ShowWarning(err ?? "Carte invalide.", "Publication");
            return;
        }

        var persistenceLabel = _persistenceCapabilities.IsDurablePersistence ? "PostgreSQL" : _persistenceCapabilities.DisplayLabel;
        if (!_dialogService.ConfirmYesNo(
                $"Publier cette carte vers {persistenceLabel} ? Une révision publiée immuable sera conservée.",
                "Publication"))
        {
            return;
        }

        await SyncPublishedTilesetsFromCacheAsync().ConfigureAwait(true);
        AttachCurrentPrefabsToWorkspace();
        var result = await _workspace.SaveCurrentAsync(SaveMapIntent.Publish).ConfigureAwait(true);
        await HandleSaveResultAsync(result, published: true).ConfigureAwait(true);
    }

    private async System.Threading.Tasks.Task HandleSaveResultAsync(SaveMapResult result, bool published)
    {
        var persistenceLabel = _persistenceCapabilities.IsDurablePersistence ? "PostgreSQL" : _persistenceCapabilities.DisplayLabel;
        switch (result)
        {
            case SaveMapResult.Success success:
                _canvas.DefaultWarpTargetMapId = _workspace!.CurrentMapId;
                if (_canvas.PlaytestSpawnTile is { } savedSpawn && _canvas.Map is { } savedMap)
                {
                    EditorMapSpawnWorkstate.Write(success.MapId, savedMap, savedSpawn.X, savedSpawn.Y);
                }

                if (_canvas.Map is { } prefabMap)
                {
                    EditorMapPrefabWorkstate.Write(success.MapId, prefabMap, _canvas.PrefabPlacements);
                    EditorMapPlacedEntityWorkstate.Write(success.MapId, prefabMap, _canvas.PlacedEntities);
                }

                SyncMapsTree();
                PushEditorStatusLine();
                UpdateMapChromeLabels();
                var label = published ? "publiée" : "enregistrée";
                _dialogService.ShowInfo(
                    $"Carte {label} ({persistenceLabel}, id {success.MapId:N}, révision {success.NewRevision}).",
                    published ? $"Publication {persistenceLabel}" : $"Enregistrement {persistenceLabel}");
                break;
            case SaveMapResult.ValidationFailed failed:
                _dialogService.ShowWarning(failed.Error, "Validation");
                break;
            case SaveMapResult.NotDurable notDurable:
                _dialogService.ShowWarning(notDurable.Message, published ? "Publication indisponible" : "Enregistrement indisponible");
                break;
            case SaveMapResult.PersistenceFailed failed:
                _dialogService.ShowError(
                    $"Échec de persistance ({persistenceLabel}) : {failed.Error}\nLa carte modifiée reste en mémoire.",
                    published ? "Publication échouée" : "Enregistrement échoué");
                break;
            case SaveMapResult.Conflict conflict:
                if (!_dialogService.ConfirmYesNo(
                        $"Conflit de révision (attendue r{_workspace!.CurrentRevision}, serveur r{conflict.CurrentRevision}). Recharger la carte depuis le catalogue ?",
                        "Conflit"))
                {
                    break;
                }

                if (await _workspace.ReloadCurrentAsync().ConfigureAwait(true))
                {
                    ApplyWorkspaceMapToUi();
                    await HydrateTilesetCacheFromPublishedAsync().ConfigureAwait(true);
                }

                break;
        }
    }

    private async System.Threading.Tasks.Task<bool> TryDiscardOrSaveBeforeSwitchAsync()
    {
        var answer = _dialogService.PromptSaveDiscardCancel(
            "Modifications non enregistrées. Enregistrer avant de continuer ?",
            "Modifications");
        return answer switch
        {
            EditorPromptChoice.Cancel => false,
            EditorPromptChoice.Discard => true,
            EditorPromptChoice.Save => await TrySaveBeforeSwitchAsync(),
            _ => false,
        };
    }

    private async System.Threading.Tasks.Task<bool> TrySaveBeforeSwitchAsync()
    {
        await SaveMapCoreAsync().ConfigureAwait(true);
        return _workspace?.IsDirty != true;
    }

    private void UpdatePersistenceMenuState()
    {
        var enabled = CanExecuteSaveOrPublish();
        if (_mnuSave is not null)
        {
            _mnuSave.Enabled = enabled;
            _mnuSave.Text = _persistenceCapabilities.IsDurablePersistence
                ? "Enregistrer (PostgreSQL)"
                : _persistenceCapabilities.AllowsSave
                    ? "Enregistrer (test mémoire)"
                    : "Enregistrer (non persistant)";
        }

        if (_mnuPublish is not null)
        {
            _mnuPublish.Enabled = enabled;
            _mnuPublish.Text = _persistenceCapabilities.IsDurablePersistence
                ? "Publier (PostgreSQL)…"
                : _persistenceCapabilities.AllowsSave
                    ? "Publier (test mémoire)…"
                    : "Publier (non persistant)…";
        }
    }

    internal void EditSelectedWarpDestination()
    {
        if (_canvas.Map is null || _workspace is null)
        {
            return;
        }

        var layerIndex = GetSelectedLayerIndex();
        if (layerIndex < 0 || layerIndex >= _canvas.Map.Layers.Count)
        {
            return;
        }

        var tile = _canvas.Map.Layers[layerIndex].Tiles.FirstOrDefault(t => t.X == _lastHoverTile.X && t.Y == _lastHoverTile.Y);
        if (tile is null || tile.Type != TileType.Warp)
        {
            MessageBox.Show(GetDialogOwner(), "Sélectionnez une tuile warp (couche attributs) sous le curseur.", "Warp", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        EditWarpDestination(tile);
    }

    internal void EditWarpDestination(Tile tile)
    {
        if (_canvas.Map is null || _workspace is null)
        {
            return;
        }

        using var dlg = new Dialogs.WarpDestinationDialog(
            _workspace.Catalog,
            tile.WarpTargetMapId == Guid.Empty ? _workspace.CurrentMapId ?? Guid.Empty : tile.WarpTargetMapId,
            tile.WarpTargetX,
            tile.WarpTargetY,
            _canvas.Map.Width,
            _canvas.Map.Height);
        if (dlg.ShowDialog(GetDialogOwner()) != DialogResult.OK)
        {
            return;
        }

        if (!dlg.TryValidate(out var verr))
        {
            MessageBox.Show(GetDialogOwner(), verr, "Warp", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        _canvas.History.PushBeforeChange(_canvas.Map);
        tile.WarpTargetMapId = dlg.TargetMapId;
        tile.WarpTargetX = dlg.TargetX;
        tile.WarpTargetY = dlg.TargetY;
        _workspace.MarkDirty();
        _canvas.Invalidate();
        UpdateUndoRedoButtons();
        RefreshTransferWarnings();
    }

    internal void LaunchFrogGameClient()
    {
        EditorFrogClientLauncher.Launch(GetDialogOwner());
    }

    internal bool IsPlaytestActiveForTest()
        => _playtestOrchestrator?.ActiveSession?.IsActive == true;

    internal bool IsPlaytestBusyForTest() => _playtestBusy;

    internal bool HasOwnedPlaytestProcessesForTest()
        => _playtestLauncher?.HasOwnedProcesses == true
           || _playtestOrchestrator?.ActiveSession is { Server: not null } or { Client: not null };

    internal string? LastPlaytestErrorForTest { get; private set; }

    internal IReadOnlyList<string> DrainPlaytestLauncherLogsForTest()
        => _playtestLauncher?.DrainLogsSnapshot() ?? Array.Empty<string>();

    internal PlaytestSessionState? GetPlaytestSessionForTest()
        => _playtestOrchestrator?.ActiveSession;

    internal async Task StartPlaytestAsync()
    {
        if (_playtestBusy)
        {
            return;
        }

        if (_workspace is null)
        {
            LastPlaytestErrorForTest = "Workspace non initialisé.";
            _dialogService.ShowWarning(LastPlaytestErrorForTest, "Playtest");
            return;
        }

        _playtestBusy = true;
        LastPlaytestErrorForTest = null;
        UpdatePlaytestMenuState();
        _playtestCts = new CancellationTokenSource();
        var ct = _playtestCts.Token;

        try
        {
            // Gate durable avant résolution des exécutables (messages actionnables stables en smoke).
            if (!EditorTestHooks.AllowNonDurablePlaytest && !_persistenceCapabilities.IsDurablePersistence)
            {
                LastPlaytestErrorForTest =
                    "Playtest impossible : PostgreSQL durable requis (les brouillons mémoire ne sont pas playtestables).";
                _dialogService.ShowWarning(LastPlaytestErrorForTest, "Playtest");
                return;
            }

            if (!TryResolvePlaytestExecutables(out var serverExe, out var clientExe))
            {
                return;
            }

            _playtestLauncher = new EditorPlaytestProcessLauncher();
            var launcher = EditorTestHooks.OverridePlaytestProcessLauncher
                           ?? (IPlaytestProcessLauncher)_playtestLauncher;
            if (_mapRepository is null)
            {
                LastPlaytestErrorForTest = "Dépôt carte non initialisé.";
                _dialogService.ShowWarning(LastPlaytestErrorForTest, "Playtest");
                return;
            }

            var preparer = new PlaytestMapPreparer(_mapRepository);
            _playtestOrchestrator = new PlaytestOrchestrator(
                preparer,
                launcher,
                new EditorPlaytestTilesetSidecar(() => _canvas.PrefabPlacements, () => _canvas.PlacedEntities));

            if (_workspace.CurrentMap is null)
            {
                LastPlaytestErrorForTest = "Aucune carte ouverte.";
                _dialogService.ShowWarning(LastPlaytestErrorForTest, "Playtest");
                return;
            }

            if (!ConfirmTransferPlaytestGate())
            {
                LastPlaytestErrorForTest = "Playtest annulé : transferts à corriger.";
                return;
            }

            var map = _workspace.CurrentMap;
            if (_workspace.IsDirty
                && !_dialogService.ConfirmYesNo(PlaytestHotload.DirtySavePrompt, "Tester"))
            {
                LastPlaytestErrorForTest = PlaytestHotload.CancelledDirtyMessage;
                return;
            }

            int? storedX = null;
            int? storedY = null;
            if (EditorMapSpawnWorkstate.TryRead(_workspace.CurrentMapId, map, out var memoX, out var memoY))
            {
                storedX = memoX;
                storedY = memoY;
            }
            else if (_canvas.PlaytestSpawnTile is { } canvasSpawn)
            {
                storedX = canvasSpawn.X;
                storedY = canvasSpawn.Y;
            }

            var hotload = PlaytestHotload.Decide(new PlaytestHotloadRequest
            {
                IsDirty = _workspace.IsDirty,
                SaveChoice = PlaytestSaveChoice.Save,
                CurrentMapId = _workspace.CurrentMapId,
                Map = map,
                RememberedTileX = storedX,
                RememberedTileY = storedY,
                FallbackTileX = _lastHoverTile.X,
                FallbackTileY = _lastHoverTile.Y,
            });
            if (hotload is PlaytestHotloadDecision.Cancelled cancelledHotload)
            {
                LastPlaytestErrorForTest = cancelledHotload.Reason;
                _dialogService.ShowWarning(cancelledHotload.Reason, "Tester");
                return;
            }

            var ready = (PlaytestHotloadDecision.Ready)hotload;
            int spawnX;
            int spawnY;
            if (EditorTestHooks.OverrideSpawnTile is { } forcedSpawn)
            {
                spawnX = forcedSpawn.X;
                spawnY = forcedSpawn.Y;
            }
            else if (ready.UsedRememberedSpawn)
            {
                spawnX = ready.TileX;
                spawnY = ready.TileY;
            }
            else
            {
                using var spawnDlg = new Dialogs.PlaytestSpawnDialog(map.Width, map.Height, ready.TileX, ready.TileY);
                if (spawnDlg.ShowDialog(GetDialogOwner()) != DialogResult.OK)
                {
                    LastPlaytestErrorForTest = "Playtest annulé (spawn).";
                    return;
                }

                spawnX = spawnDlg.TileX;
                spawnY = spawnDlg.TileY;
            }

            PersistPlaytestSpawnFromUser(spawnX, spawnY);
            await SyncPublishedTilesetsFromCacheAsync().ConfigureAwait(true);

            var port = EditorFrogServerLauncher.FindFreeTcpPort();
            var prepare = new PlaytestPrepareRequest
            {
                CorrelationId = Guid.NewGuid(),
                Host = "127.0.0.1",
                Port = port,
                SpawnTileX = spawnX,
                SpawnTileY = spawnY,
                RequireDurablePersistence = !EditorTestHooks.AllowNonDurablePlaytest,
                PublishCurrentBeforeLaunch = true,
            };

            var result = await _playtestOrchestrator.StartAsync(_workspace, prepare, serverExe, clientExe, ct)
                .ConfigureAwait(true);

            if (result is PlaytestPreparationResult.Failed failed)
            {
                LastPlaytestErrorForTest = failed.Error;
                _dialogService.ShowWarning(
                    failed.Error + Environment.NewLine + Environment.NewLine + $"(code: {failed.Kind})",
                    "Playtest");
                return;
            }

            if (result is PlaytestPreparationResult.Success success)
            {
                PersistCurrentPlaytestSpawnUnderMapId(success.Plan.PrimaryCanonicalMapId);
                var lines = new List<string>();
                if (_playtestOrchestrator.ActiveSession?.LogLines is { } sessionLogs)
                {
                    lines.AddRange(sessionLogs);
                }

                if (_playtestLauncher is not null)
                {
                    lines.AddRange(_playtestLauncher.DrainLogsSnapshot());
                }

                var summary = lines.Count > 0
                    ? string.Join(Environment.NewLine, lines.TakeLast(40))
                    : $"Playtest prêt — MapId={success.Plan.PrimaryCanonicalMapId} rev={success.Plan.PrimaryPublishedRevision} spawn=({success.Plan.Spawn.TileX},{success.Plan.Spawn.TileY})";
                _dialogService.ShowInfo(summary, "Playtest");
            }
        }
        catch (OperationCanceledException)
        {
            LastPlaytestErrorForTest = "Playtest annulé.";
        }
        catch (Exception ex)
        {
            LastPlaytestErrorForTest = ex.Message;
            _dialogService.ShowError("Échec playtest : " + ex.Message, "Playtest");
        }
        finally
        {
            _playtestBusy = false;
            UpdatePlaytestMenuState();
            PushEditorStatusLine();
        }
    }

    internal async Task StopPlaytestAsync()
    {
        try
        {
            _playtestCts?.Cancel();
            if (_playtestOrchestrator is not null)
            {
                await _playtestOrchestrator.StopAsync().ConfigureAwait(true);
            }

            if (_playtestLauncher is not null)
            {
                await _playtestLauncher.StopAllOwnedAsync().ConfigureAwait(true);
            }
        }
        finally
        {
            UpdatePlaytestMenuState();
            PushEditorStatusLine();
        }
    }

    private void UpdatePlaytestMenuState()
    {
        var active = _playtestOrchestrator?.ActiveSession?.IsActive == true;
        if (_mnuPlaytest is not null)
        {
            _mnuPlaytest.Enabled = !_playtestBusy && !active;
        }

        if (_mnuStopPlaytest is not null)
        {
            _mnuStopPlaytest.Enabled = active || _playtestBusy;
        }

        PlaytestStateChanged?.Invoke();
    }

    private bool TryResolvePlaytestExecutables(out string serverExe, out string clientExe)
    {
        serverExe = string.Empty;
        clientExe = string.Empty;
        var serverOverridden = !string.IsNullOrWhiteSpace(EditorTestHooks.OverrideServerExePath);
        var clientOverridden = !string.IsNullOrWhiteSpace(EditorTestHooks.OverrideClientExePath);
        if (serverOverridden)
        {
            serverExe = EditorTestHooks.OverrideServerExePath!;
        }

        if (clientOverridden)
        {
            clientExe = EditorTestHooks.OverrideClientExePath!;
        }

        if (!serverOverridden && !clientOverridden
            && PlaytestPublishLayouts.TryReusePair(
                _playtestReuseClientExe,
                _playtestReuseServerExe,
                out var reusedClient,
                out var reusedServer))
        {
            clientExe = reusedClient;
            serverExe = reusedServer;
        }

        if (string.IsNullOrWhiteSpace(serverExe))
        {
            if (!EditorFrogServerLauncher.TryResolveExecutable(out serverExe, out _))
            {
                LastPlaytestErrorForTest = "Frog.Server introuvable. Compilez le serveur (Release/Debug) ou indiquez le chemin.";
                _dialogService.ShowWarning(LastPlaytestErrorForTest, "Tester");
                return false;
            }
        }

        if (string.IsNullOrWhiteSpace(clientExe))
        {
            if (!EditorFrogClientLauncher.TryResolveExecutable(out clientExe))
            {
                LastPlaytestErrorForTest = "Frog.Client.exe introuvable.";
                _dialogService.ShowWarning(LastPlaytestErrorForTest, "Tester");
                return false;
            }
        }

        if (!serverOverridden && !clientOverridden)
        {
            _playtestReuseClientExe = clientExe;
            _playtestReuseServerExe = serverExe;
        }

        return true;
    }

    private void OnTileContextMenuRequested(Point tile)
    {
        _lastHoverTile = tile;
        PushEditorStatusLine();
        var menu = new ContextMenuStrip();
        menu.Closed += (_, _) => menu.Dispose();
        menu.Items.Add("PNJ rapide…", null, (_, _) => OpenQuickTalkingNpc());
        menu.Items.Add("Événements carte (cette tuile)…", null, (_, _) => BrowseMapEvents());
        menu.Show(Cursor.Position);
    }

    internal void OpenQuickTalkingNpc()
    {
        if (_phase8ContentService is null || !_phase8ContentService.IsAvailable
            || _mapEventService is null || !_mapEventService.IsAvailable)
        {
            MessageBox.Show(
                GetDialogOwner(),
                QuickTalkingNpcMessages.PostgresRequired,
                "PNJ rapide",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        CancelQuickNpcPlacement(userInitiated: false);
        using var dlg = new QuickTalkingNpcDialog(_phase8ContentService, _mapEventService);
        if (dlg.ShowDialog(GetDialogOwner()) != DialogResult.OK || dlg.Result is not { Success: true } created)
        {
            return;
        }

        var mapId = _workspace?.CurrentMapId ?? Guid.Empty;
        if (_canvas.Map is null || mapId == Guid.Empty)
        {
            _statusNotice = $"PNJ « {created.DisplayName} » créé. Enregistrez la carte au catalogue avant de le placer.";
            PushEditorStatusLine();
            MessageBox.Show(
                GetDialogOwner(),
                QuickTalkingNpcMessages.NeedsCatalogMap(created.DisplayName),
                "PNJ rapide",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        MessageBox.Show(
            GetDialogOwner(),
            QuickTalkingNpcMessages.CreatedPlacementPrompt(created.DisplayName),
            "PNJ rapide",
            MessageBoxButtons.OK,
            MessageBoxIcon.Information);
        ArmQuickNpcPlacement(created.EventId, created.TriggerKind, created.DisplayName);
    }

    internal bool IsQuickNpcPlacementArmedForTest => _pendingQuickNpc is not null;

    internal string? StatusNoticeForTest => _statusNotice;

    internal void ArmQuickNpcPlacementForTest(Guid eventId, string triggerKind, string displayName) =>
        ArmQuickNpcPlacement(eventId, triggerKind, displayName);

    internal bool TryPlaceArmedNpcForTest(int tileX, int tileY) =>
        TryConsumeQuickNpcPlacement(new Point(tileX, tileY));

    private void ArmQuickNpcPlacement(Guid eventId, string triggerKind, string displayName)
    {
        _pendingQuickNpc = new PendingQuickNpc(eventId, triggerKind, displayName);
        _canvas.QuickNpcPlacementClick = TryConsumeQuickNpcPlacement;
        _statusNotice = QuickTalkingNpcMessages.StatusPlacementPrompt(displayName);
        PushEditorStatusLine();
    }

    internal bool CancelQuickNpcPlacement(bool userInitiated)
    {
        if (_pendingQuickNpc is null)
        {
            return false;
        }

        _pendingQuickNpc = null;
        _canvas.QuickNpcPlacementClick = null;
        _statusNotice = userInitiated ? QuickTalkingNpcMessages.PlacementCancelled : null;
        PushEditorStatusLine();
        return true;
    }

    private bool TryConsumeQuickNpcPlacement(Point tile)
    {
        var pending = _pendingQuickNpc;
        if (pending is null || _mapEventService is null)
        {
            return false;
        }

        var mapId = _workspace?.CurrentMapId ?? Guid.Empty;
        if (mapId == Guid.Empty)
        {
            MessageBox.Show(
                GetDialogOwner(),
                QuickTalkingNpcMessages.NeedsCatalogMap(pending.DisplayName),
                "PNJ rapide",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return true;
        }

        if (!_mapEventService.TryInsertPlacement(mapId, pending.EventId, tile.X, tile.Y, pending.TriggerKind, out var err))
        {
            MessageBox.Show(
                GetDialogOwner(),
                string.IsNullOrWhiteSpace(err) ? "Placement impossible." : err,
                "PNJ rapide",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
            return true;
        }

        _pendingQuickNpc = null;
        _canvas.QuickNpcPlacementClick = null;
        _statusNotice = QuickTalkingNpcMessages.Placed(pending.DisplayName, tile.X, tile.Y);
        RefreshMapEventMarkers();
        PushEditorStatusLine();
        return true;
    }

    private sealed record PendingQuickNpc(Guid EventId, string TriggerKind, string DisplayName);

    internal void BrowseMapEvents()
    {
        if (_mapEventService is null || !_mapEventService.IsAvailable)
        {
            MessageBox.Show(
                GetDialogOwner(),
                "Événements carte nécessitent PostgreSQL (FROG_POSTGRES_CONNECTION_STRING ou appsettings.Local.json).",
                "Événements carte",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        var mapId = _workspace?.CurrentMapId ?? Guid.Empty;
        if (_mapEventsDialog is { IsDisposed: false })
        {
            _mapEventsDialog.SetMapContext(mapId, _lastHoverTile.X, _lastHoverTile.Y);
            if (_mapEventsDialog.WindowState == FormWindowState.Minimized)
            {
                _mapEventsDialog.WindowState = FormWindowState.Normal;
            }

            _mapEventsDialog.Activate();
            return;
        }

        var dlg = new MapEventsBrowseDialog(
            _mapEventService,
            mapId,
            defaultTileX: _lastHoverTile.X,
            defaultTileY: _lastHoverTile.Y);
        dlg.ShowEventNamesChanged += visible => MapEventNamesVisible = visible;
        dlg.PlacementHighlighted += OnMapEventPlacementHighlighted;
        dlg.PlacementsChanged += RefreshMapEventMarkers;
        dlg.SetShowEventNames(_canvas.ShowMapEventNames);
        dlg.FormClosed += (_, _) =>
        {
            var openQuick = dlg.QuickNpcRequested;
            if (ReferenceEquals(_mapEventsDialog, dlg))
            {
                _mapEventsDialog = null;
            }

            RefreshMapEventMarkers();
            if (!dlg.IsDisposed)
            {
                dlg.Dispose();
            }

            if (openQuick)
            {
                OpenQuickTalkingNpc();
            }
        };
        _mapEventsDialog = dlg;
        dlg.Show(GetDialogOwner());
    }

    private void OnCanvasMapEventMarkerPicked(MapEventMarkerView marker)
    {
        if (_mapEventsDialog is not { IsDisposed: false })
        {
            return;
        }

        _mapEventsDialog.TrySelectPlacement(marker.TileX, marker.TileY, marker.PrimaryPlacementKey);
    }

    private void OnMapEventPlacementHighlighted(PgMapEventPlacementRow row)
    {
        _canvas.HighlightMapEventMarker(row.TileX, row.TileY, row.Id.ToString("D"));
    }

    internal void BrowsePhase8Content()
    {
        if (_phase8ContentService is null || !_phase8ContentService.IsAvailable)
        {
            MessageBox.Show(
                GetDialogOwner(),
                "Contenu Phase 8 nécessite PostgreSQL (FROG_POSTGRES_CONNECTION_STRING ou appsettings.Local.json).",
                "Contenu Phase 8",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
            return;
        }

        using var dlg = new Phase8.Phase8ContentBrowseDialog(_phase8ContentService);
        dlg.ShowDialog(GetDialogOwner());
    }

    /// <summary>Recharge les placements d'événements pour la carte catalogue courante et met à jour l'overlay canevas.</summary>
    internal void RefreshMapEventMarkers()
    {
        if (_syncingMapEventOverlay || IsDisposed || _canvas.IsDisposed)
        {
            return;
        }

        _syncingMapEventOverlay = true;
        try
        {
            _eventTransferLinks = Array.Empty<MapTransferLink>();
            if (_mapEventService is null || !_mapEventService.IsAvailable)
            {
                _canvas.MapEventMarkers = null;
            }
            else if (_workspace?.CurrentMapId is not Guid mapId || mapId == Guid.Empty)
            {
                _canvas.MapEventMarkers = null;
            }
            else
            {
                try
                {
                    var rows = _mapEventService.LoadPlacementsForMap(mapId);
                    _canvas.MapEventMarkers = MapEventsPostgreSqlService.ToMarkerViews(rows);
                    _eventTransferLinks = BuildEventTransferLinks(rows);
                }
                catch
                {
                    _canvas.MapEventMarkers = null;
                    _eventTransferLinks = Array.Empty<MapTransferLink>();
                }
            }

            var current = _workspace?.CurrentMapId ?? Guid.Empty;
            if (_mapEventsDialog is { IsDisposed: false })
            {
                _mapEventsDialog.EnsureMap(current);
            }
        }
        finally
        {
            _syncingMapEventOverlay = false;
        }

        RefreshTransferWarnings();
    }

    internal void ShowTransferIssues()
    {
        RefreshMapEventMarkers();
        if (_transferIssues.Count == 0)
        {
            _dialogService.ShowInfo(
                "Aucun problème de transfert (carte manquante, hors limites, tuile bloquée ou carte non publiée).",
                "Transferts");
            return;
        }

        FocusTransferIssue(_transferIssues[0]);
        _dialogService.ShowWarning(MapTransferValidator.FormatIssueList(_transferIssues), "Transferts");
    }

    internal IReadOnlyList<MapTransferIssue> TransferIssuesForTest => _transferIssues;

    private bool ConfirmTransferPlaytestGate()
    {
        RefreshTransferWarnings();
        var message = MapTransferValidator.FormatPlaytestGateMessage(_transferIssues);
        if (message is null)
        {
            return true;
        }

        return _dialogService.ConfirmYesNo(message, "Transferts");
    }

    private void RefreshTransferWarnings()
    {
        if (IsDisposed || _canvas.IsDisposed)
        {
            return;
        }

        IReadOnlyList<MapTransferIssue> issues;
        if (_canvas.Map is null)
        {
            issues = Array.Empty<MapTransferIssue>();
        }
        else
        {
            var links = MapTransferScanner.ScanWarps(_canvas.Map);
            foreach (var link in _eventTransferLinks)
            {
                links.Add(link);
            }

            var context = MapTransferCatalogBuilder.Build(
                _canvas.Map,
                _workspace?.CurrentMapId,
                _workspace?.Catalog ?? Array.Empty<MapCatalogEntry>(),
                TryLoadTransferMap);
            issues = MapTransferValidator.Validate(links, context);
        }

        _transferIssues = issues.ToList();
        _transferIssuesPanel.SetIssues(_transferIssues);
        _canvas.SetTransferIssueTiles(_transferIssues.Select(issue => (issue.SourceX, issue.SourceY)));
        PushEditorStatusLine();
    }

    private void FocusTransferIssue(MapTransferIssue issue)
    {
        _lastHoverTile = new Point(issue.SourceX, issue.SourceY);
        _canvas.CenterViewOnTile(issue.SourceX, issue.SourceY);
        if (_canvas.MapEventMarkers is { } markers)
        {
            foreach (var marker in markers)
            {
                if (marker.TileX == issue.SourceX && marker.TileY == issue.SourceY)
                {
                    _canvas.HighlightMapEventMarker(issue.SourceX, issue.SourceY, marker.PrimaryPlacementKey);
                    break;
                }
            }
        }

        if (issue.SourceKind == MapTransferSourceKind.Warp && _canvas.Map is not null)
        {
            Tile? tile = null;
            foreach (var layer in _canvas.Map.Layers)
            {
                tile = layer.Tiles.FirstOrDefault(t =>
                    t.X == issue.SourceX && t.Y == issue.SourceY && t.Type == TileType.Warp);
                if (tile is not null)
                {
                    break;
                }
            }

            if (tile is not null)
            {
                _propGrid.SelectedObject = tile;
            }
        }

        PushEditorStatusLine();
    }

    private MapTransferLoadedMap TryLoadTransferMap(Guid mapId)
    {
        if (_workspace is null || _mapRepository is null || mapId == Guid.Empty)
        {
            return new MapTransferLoadedMap(null, false);
        }

        if (_workspace.CurrentMapId == mapId)
        {
            return new MapTransferLoadedMap(null, false);
        }

        var entry = _workspace.Catalog.FirstOrDefault(item => item.MapId == mapId);
        if (entry is null)
        {
            return new MapTransferLoadedMap(null, false);
        }

        if (_transferMapCache.TryGetValue(mapId, out var cached)
            && cached.DraftRevision == entry.Revision
            && cached.PublishedRevision == entry.PublishedRevision)
        {
            return new MapTransferLoadedMap(cached.Map, cached.IsPublished);
        }

        try
        {
            StoredMap? stored = null;
            var published = false;
            if (entry.PublishedRevision is not null)
            {
                stored = MapEventsPostgreSqlService.RunOffUiSyncContext(
                    () => _mapRepository.LoadPublishedByIdAsync(mapId));
                published = stored is not null;
            }

            if (stored is null)
            {
                stored = MapEventsPostgreSqlService.RunOffUiSyncContext(
                    () => _mapRepository.LoadByIdAsync(mapId));
                published = false;
            }

            if (stored?.Map is null)
            {
                return new MapTransferLoadedMap(null, false);
            }

            _transferMapCache[mapId] = new CachedTransferMap(entry.Revision, entry.PublishedRevision, stored.Map, published);
            return new MapTransferLoadedMap(stored.Map, published);
        }
        catch
        {
            return new MapTransferLoadedMap(null, false);
        }
    }

    private IReadOnlyList<MapTransferLink> BuildEventTransferLinks(IReadOnlyList<PgMapEventPlacementRow> rows)
    {
        if (_mapEventService is null || rows.Count == 0)
        {
            return Array.Empty<MapTransferLink>();
        }

        var links = new List<MapTransferLink>();
        var definitions = new Dictionary<Guid, MapEventDefinition?>();
        var commonEvents = new Dictionary<Guid, CommonEventDefinition?>();
        IReadOnlyList<Phase8ContentListRow>? commonRows = null;
        foreach (var row in rows)
        {
            if (!definitions.TryGetValue(row.EventDefinitionId, out var definition))
            {
                definition = _mapEventService.TryLoadDefinition(row.EventDefinitionId);
                definitions[row.EventDefinitionId] = definition;
            }

            if (definition is null)
            {
                continue;
            }

            var name = string.IsNullOrWhiteSpace(row.DisplayName) ? row.Slug : row.DisplayName;
            MapTransferScanner.AppendEventTeleports(
                links,
                row.TileX,
                row.TileY,
                name,
                definition.Pages,
                LookupCommonEvent);
        }

        return links;

        (bool Found, string Name, IReadOnlyList<MapEventPageDefinition> Pages) LookupCommonEvent(Guid? id, int? alias)
        {
            var resolved = id is Guid guid && guid != Guid.Empty ? guid : Guid.Empty;
            if (resolved == Guid.Empty && alias is int editorAlias && editorAlias > 0 && _phase8ContentService is { IsAvailable: true })
            {
                try
                {
                    commonRows ??= MapEventsPostgreSqlService.RunOffUiSyncContext(
                        () => _phase8ContentService.ListAsync(Phase8ContentKind.CommonEvent));
                }
                catch
                {
                    commonRows = Array.Empty<Phase8ContentListRow>();
                }

                foreach (var commonRow in commonRows)
                {
                    if (commonRow.EditorAliasId == editorAlias)
                    {
                        resolved = commonRow.Id;
                        break;
                    }
                }
            }

            if (resolved == Guid.Empty)
            {
                return (false, string.Empty, Array.Empty<MapEventPageDefinition>());
            }

            if (!commonEvents.TryGetValue(resolved, out var commonEvent))
            {
                commonEvent = TryLoadCommonEvent(resolved);
                commonEvents[resolved] = commonEvent;
            }

            if (commonEvent is null)
            {
                return (false, string.Empty, Array.Empty<MapEventPageDefinition>());
            }

            return (true, commonEvent.Name, commonEvent.Pages);
        }
    }

    private CommonEventDefinition? TryLoadCommonEvent(Guid id)
    {
        if (_phase8ContentService is not { IsAvailable: true })
        {
            return null;
        }

        try
        {
            var stored = MapEventsPostgreSqlService.RunOffUiSyncContext(
                () => _phase8ContentService.LoadDraftAsync(id));
            if (stored is null
                || !Phase8ContentPostgreSqlService.TryDeserialize(stored.PayloadJson, out CommonEventDefinition definition, out _))
            {
                return null;
            }

            return definition;
        }
        catch
        {
            return null;
        }
    }

    private sealed record CachedTransferMap(long DraftRevision, long? PublishedRevision, Map Map, bool IsPublished);

    internal bool MapEventMarkersVisible
    {
        get => _canvas.ShowMapEventMarkers;
        set
        {
            _canvas.ShowMapEventMarkers = value;
            if (_mnuShowEventMarkers is not null && _mnuShowEventMarkers.Checked != value)
            {
                _mnuShowEventMarkers.Checked = value;
            }

            _canvas.Invalidate();
        }
    }

    internal event Action? MapEventNamesVisibilityChanged;

    internal bool MapEventNamesVisible
    {
        get => _canvas.ShowMapEventNames;
        set
        {
            var changed = _canvas.ShowMapEventNames != value;
            _canvas.ShowMapEventNames = value;
            if (_mnuShowEventNames is not null && _mnuShowEventNames.Checked != value)
            {
                _mnuShowEventNames.Checked = value;
            }

            _mapEventsDialog?.SetShowEventNames(value);
            if (changed)
            {
                MapEventNamesVisibilityChanged?.Invoke();
            }
        }
    }

    private static void SaveTilesetManifestNextToMap(string mapFilePath)
    {
        var dir = Path.GetDirectoryName(mapFilePath);
        if (string.IsNullOrEmpty(dir))
        {
            return;
        }

        var stem = Path.GetFileNameWithoutExtension(mapFilePath);
        if (string.IsNullOrEmpty(stem))
        {
            return;
        }

        var files = TilesetCache.SnapshotPngFiles();
        MapTilesetPackage.WriteSidecars(dir, [stem], files);
        var manifest = MapTilesetPackage.BuildManifest(files);
        File.WriteAllBytes(Path.Combine(dir, stem + ".tilesets.json"), TilesetManifestJson.Serialize(manifest));
        foreach (var file in files)
        {
            File.WriteAllBytes(Path.Combine(dir, MapTilesetPackage.FileNameFor(file.Id)), file.PngBytes);
        }
    }

    private void SavePrefabSidecarNextToMap(string mapFilePath)
    {
        MapPrefabPackage.WritePlacementSidecarNextToMap(mapFilePath, _canvas.PrefabPlacements);
        if (_canvas.Map is { } map)
        {
            EditorMapPrefabWorkstate.Write(_workspace?.CurrentMapId, map, _canvas.PrefabPlacements);
        }
    }

    private void TryApplyPrefabSidecarFromMapPath(string mapFilePath)
    {
        _suppressPrefabPersist = true;
        try
        {
            var document = MapPrefabPackage.TryReadPlacementSidecarNextToMap(mapFilePath);
            if (document?.Placements is { Count: > 0 })
            {
                _canvas.ReplacePrefabPlacements(document.Placements);
                if (_canvas.Map is { } map)
                {
                    EditorMapPrefabWorkstate.Write(_workspace?.CurrentMapId, map, _canvas.PrefabPlacements);
                }

                return;
            }

            RestorePrefabPlacementsFromWorkstate();
        }
        finally
        {
            _suppressPrefabPersist = false;
        }
    }

    internal void LoadMap() => _ = LoadMapCoreAsync();

    private async System.Threading.Tasks.Task LoadMapCoreAsync()
    {
        if (_workspace?.IsDirty == true && !await TryDiscardOrSaveBeforeSwitchAsync().ConfigureAwait(true))
        {
            return;
        }

        using var ofd = new OpenFileDialog { Filter = "Frog Map|*.fmap" };
        if (ofd.ShowDialog(GetDialogOwner()) != DialogResult.OK)
        {
            return;
        }

        var mapPath = ofd.FileName;
        var data = File.ReadAllBytes(mapPath);
        var map = TileAssetMapEditing.ReadEditorMap(data);
        _tileAssetMapPath = TileAssetMapEditing.IsTileAssetMap(map) ? mapPath : null;

        TilesetCache.Clear();
        var manifestOutcome = TryApplyTilesetManifestFromMapPath(mapPath);
        TryApplyAnimSidecarsFromMapPath(mapPath);

        _canvas.ClearHistory();
        _canvas.Map = map;
        _workspace?.AdoptLocalDraft(map);
        _canvas.ActiveTilesetId = 0;
        if (TilesetCache.ListRegistered().Count > 0)
        {
            _canvas.ActiveTilesetId = TilesetCache.ListRegistered()[0].Id;
        }

        _propGrid.SelectedObject = map;
        RefreshLayersUi();
        RefreshTilesetList();
        _canvas.Invalidate();
        UpdateUndoRedoButtons();
        SyncMapsTree();
        UpdateMapChromeLabels();
        RefreshMapEventMarkers();
        RestorePlaytestSpawnFromWorkstate();
        TryApplyPrefabSidecarFromMapPath(mapPath);
        RestorePlacedEntitiesFromWorkstate();
        SyncPaletteModeToMap();

        if (manifestOutcome.HadManifest && manifestOutcome.MissingFiles.Count > 0)
        {
            var list = string.Join(Environment.NewLine, manifestOutcome.MissingFiles.Take(12));
            var tail = manifestOutcome.MissingFiles.Count > 12 ? Environment.NewLine + "…" : string.Empty;
            MessageBox.Show(
                GetDialogOwner(),
                "Fichiers PNG introuvables ou illisibles (manifeste à côté du .fmap) :" + Environment.NewLine + list + tail,
                "Tilesets",
                MessageBoxButtons.OK,
                MessageBoxIcon.Warning);
        }

        PushEditorStatusLine();
    }

    private void SaveTileAssetMapFile(bool promptIfMissing)
    {
        if (_canvas.Map is null || !TileAssetMapEditing.IsTileAssetMap(_canvas.Map))
        {
            return;
        }

        if (!_canvas.Map.Validate(out var error))
        {
            _dialogService.ShowWarning(error ?? "Carte invalide.", "Enregistrement TileAsset");
            return;
        }

        var path = _tileAssetMapPath;
        if (promptIfMissing || string.IsNullOrWhiteSpace(path))
        {
            using var dialog = new SaveFileDialog
            {
                Filter = "Frog Map|*.fmap",
                FileName = SafeMapFileStem(_canvas.Map.Name) + ".fmap",
            };
            if (dialog.ShowDialog(GetDialogOwner()) != DialogResult.OK)
            {
                return;
            }

            path = dialog.FileName;
        }

        var bytes = TileAssetMapEditing.Write(_canvas.Map);
        File.WriteAllBytes(path, bytes);
        _tileAssetMapPath = path;
        SavePrefabSidecarNextToMap(path);
        _workspace?.ClearDirty();
        _statusNotice = "Carte TileAsset enregistrée (v6, 48 px).";
        UpdateMapChromeLabels();
        PushEditorStatusLine();
    }

    private static string SafeMapFileStem(string name)
    {
        var stem = string.IsNullOrWhiteSpace(name) ? "carte" : name.Trim();
        foreach (var invalid in Path.GetInvalidFileNameChars())
        {
            stem = stem.Replace(invalid, '_');
        }

        return string.IsNullOrWhiteSpace(stem) ? "carte" : stem;
    }

    /// <summary>
    /// Lit <c>{stem}.tilesets.json</c> à côté du fichier carte et réinjecte les bitmaps avec les mêmes <see cref="TilesetManifestEntry.Id"/> que lors de l’enregistrement.
    /// </summary>
    private static (bool HadManifest, List<string> MissingFiles) TryApplyTilesetManifestFromMapPath(string mapFilePath)
    {
        var missing = new List<string>();
        var dir = Path.GetDirectoryName(mapFilePath);
        var stem = Path.GetFileNameWithoutExtension(mapFilePath);
        if (string.IsNullOrEmpty(dir) || string.IsNullOrEmpty(stem))
        {
            return (false, missing);
        }

        var manifestPath = Path.Combine(dir, stem + ".tilesets.json");
        var manifest = TilesetManifestJson.TryDeserializeFromFile(manifestPath);
        if (manifest is null)
        {
            return (false, missing);
        }

        foreach (var entry in manifest.Entries.OrderBy(e => e.Id))
        {
            if (entry.Id < 1)
            {
                continue;
            }

            var nameOnly = Path.GetFileName(entry.FileName);
            if (string.IsNullOrEmpty(nameOnly))
            {
                missing.Add($"id {entry.Id} (nom vide)");
                continue;
            }

            var full = Path.Combine(dir, nameOnly);
            if (!File.Exists(full))
            {
                missing.Add(nameOnly);
                continue;
            }

            try
            {
                TilesetCache.LoadFromFileAtId(full, entry.Id);
            }
            catch
            {
                missing.Add(nameOnly);
            }
        }

        return (true, missing);
    }

    private static void TryApplyAnimSidecarsFromMapPath(string mapFilePath)
    {
        foreach (var (id, _) in TilesetCache.ListRegistered())
        {
            var source = TilesetCache.TryGetSourcePath(id);
            if (!string.IsNullOrWhiteSpace(source))
            {
                TilesetAnimCatalog.TryAttachImageSidecar(id, source);
            }
        }

        TilesetAnimCatalog.TryReplaceFromMapSidecar(mapFilePath);
    }

    internal bool AnimatedTilePreviewVisible
    {
        get => TilesetAnimCatalog.PreviewEnabled;
        set
        {
            TilesetAnimCatalog.PreviewEnabled = value;
            _tilesetPickerWpf.SyncAnimPreview();
            _canvas.Invalidate();
            PushEditorStatusLine();
        }
    }

    internal string MarkSelectedTilesAnimated()
    {
        var message = _tilesetPickerWpf.MarkSelectionAnimated();
        _canvas.Invalidate();
        return message;
    }

    internal string ClearSelectedTilesAnimated()
    {
        var message = _tilesetPickerWpf.ClearSelectionAnimated();
        _canvas.Invalidate();
        return message;
    }

    private void PositionMinimap()
    {
        var pad = _wfMapDockPanel.Padding;
        _minimap.Location = new Point(
            _wfMapDockPanel.ClientSize.Width - _minimap.Width - pad.Right,
            pad.Top);
    }

    private void ApplyLayoutPercentages()
    {
        if (_embedAsWpfChild || _splitLeft is null || _splitRight is null)
        {
            ApplyLayersPropertySplitDistance();
            PositionMinimap();
            ApplyRightTilesetSplitDistance();
            return;
        }

        var totalW = ClientSize.Width;
        if (totalW <= 0)
        {
            return;
        }

        var leftW = (int)(totalW * 0.20f);
        leftW = Math.Max(328, leftW);
        _splitLeft.SplitterDistance = leftW;

        var rightContainerW = _splitRight.Width;
        if (rightContainerW <= 0)
        {
            rightContainerW = totalW - leftW;
        }

        var propsW = (int)(rightContainerW * 0.25f);
        propsW = Math.Max(340, propsW);
        _splitRight.SplitterDistance = Math.Max(200, _splitRight.Width - propsW);

        ApplyLayersPropertySplitDistance();
        PositionMinimap();
        ApplyRightTilesetSplitDistance();
    }

    private System.Windows.Forms.IWin32Window GetDialogOwner()
    {
        if (_wpfOwnerWindow is not null)
        {
            var helper = new WindowInteropHelper(_wpfOwnerWindow);
            helper.EnsureHandle();
            return new Win32Window(helper.Handle);
        }

        return this;
    }

    /// <summary>
    /// Règle la barre tuiles / couches uniquement quand la hauteur du split le permet (évite InvalidOperationException au démarrage).
    /// </summary>
    private void ApplyRightTilesetSplitDistance()
    {
        var sc = _splitRightTileset;
        var h = sc.Height;
        var sw = sc.SplitterWidth;
        if (h <= sw + 12)
        {
            return;
        }

        var minD = sc.Panel1MinSize;
        var maxD = h - sc.Panel2MinSize - sw;
        if (maxD <= minD)
        {
            return;
        }

        var want = Math.Clamp((int)(h * 0.48f), minD, maxD);
        sc.SplitterDistance = want;
    }

    /// <summary>
    /// WinForms valide SplitterDistance dès l’assignation : au constructeur la hauteur du split est souvent 0,
    /// d’où l’impossibilité de fixer 280 + Panel2MinSize ici. On applique après layout.
    /// </summary>
    private void ApplyLayersPropertySplitDistance()
    {
        var h = _splitLayersProps.ClientSize.Height;
        var sw = _splitLayersProps.SplitterWidth;
        if (h <= sw + 8)
        {
            return;
        }

        var panel1Min = 100;
        var panel2Min = 280;
        var maxDist = h - panel2Min - sw;
        if (maxDist < panel1Min)
        {
            panel2Min = Math.Max(80, h - panel1Min - sw - 1);
            maxDist = h - panel2Min - sw;
        }

        if (maxDist < panel1Min)
        {
            return;
        }

        // Il faut une SplitterDistance valide *avant* Panel1MinSize / Panel2MinSize, sinon WinForms lève
        // InvalidOperationException (« doit se situer entre Panel1MinSize et … »).
        _splitLayersProps.SplitterDistance = Math.Clamp(_splitLayersProps.SplitterDistance, panel1Min, maxDist);

        _splitLayersProps.Panel1MinSize = panel1Min;
        _splitLayersProps.Panel2MinSize = panel2Min;

        maxDist = h - _splitLayersProps.Panel2MinSize - sw;
        if (maxDist < panel1Min)
        {
            return;
        }

        _splitLayersProps.SplitterDistance = Math.Clamp(280, panel1Min, maxDist);
    }

    internal sealed class NewMapDialog : Form
    {
        private readonly NumericUpDown _numW;
        private readonly NumericUpDown _numH;
        private readonly TextBox _txtName;
        private readonly CheckBox _tileAsset;

        public int MapWidth => (int)_numW.Value;
        public int MapHeight => (int)_numH.Value;
        public string MapName => _txtName.Text.Trim();
        public bool UseTileAsset => _tileAsset.Checked;

        public NewMapDialog()
        {
            Text = "Nouvelle carte";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(440, 300);
            ClientSize = new Size(520, 280);
            AutoScaleMode = AutoScaleMode.Dpi;
            Padding = new Padding(0);
            EditorChrome.ApplyFormChrome(this);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 5,
                Padding = new Padding(20, 18, 20, 16),
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 168f));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 42f));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 56f));

            var lblW = new Label
            {
                Text = "Largeur (tuiles)",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = EditorChrome.LabelPrimary,
                AutoSize = false,
            };
            _numW = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 512,
                Value = 20,
                Dock = DockStyle.Fill,
                Margin = new Padding(10, 6, 0, 6),
                TextAlign = HorizontalAlignment.Right,
            };
            var lblH = new Label
            {
                Text = "Hauteur (tuiles)",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = EditorChrome.LabelPrimary,
                AutoSize = false,
            };
            _numH = new NumericUpDown
            {
                Minimum = 1,
                Maximum = 512,
                Value = 15,
                Dock = DockStyle.Fill,
                Margin = new Padding(10, 6, 0, 6),
                TextAlign = HorizontalAlignment.Right,
            };
            var lblN = new Label
            {
                Text = "Nom de la carte",
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = EditorChrome.LabelPrimary,
                AutoSize = false,
            };
            _txtName = new TextBox
            {
                Text = "Nouvelle carte",
                Dock = DockStyle.Fill,
                Margin = new Padding(10, 6, 0, 6),
            };

            root.Controls.Add(lblW, 0, 0);
            root.Controls.Add(_numW, 1, 0);
            root.Controls.Add(lblH, 0, 1);
            root.Controls.Add(_numH, 1, 1);
            root.Controls.Add(lblN, 0, 2);
            root.Controls.Add(_txtName, 1, 2);
            _tileAsset = new CheckBox
            {
                Text = "Carte TileAsset (48×48, format v6)",
                AutoSize = true,
                ForeColor = EditorChrome.LabelPrimary,
                Margin = new Padding(10, 8, 0, 0),
            };
            root.SetColumnSpan(_tileAsset, 2);
            root.Controls.Add(_tileAsset, 0, 3);

            var buttons = new FlowLayoutPanel
            {
                FlowDirection = FlowDirection.RightToLeft,
                Dock = DockStyle.Fill,
                WrapContents = false,
                AutoSize = false,
                Padding = new Padding(0, 8, 0, 0),
                Margin = new Padding(0),
            };
            var btnOk = new Button { Text = "Créer", DialogResult = DialogResult.OK, AutoSize = true, MinimumSize = new Size(108, 34), Margin = new Padding(10, 0, 0, 0) };
            var btnCancel = new Button { Text = "Annuler", DialogResult = DialogResult.Cancel, AutoSize = true, MinimumSize = new Size(108, 34) };
            EditorChrome.StyleDialogButton(btnOk, primary: true);
            EditorChrome.StyleDialogButton(btnCancel, primary: false);
            buttons.Controls.Add(btnOk);
            buttons.Controls.Add(btnCancel);
            root.SetColumnSpan(buttons, 2);
            root.Controls.Add(buttons, 0, 4);

            Controls.Add(root);
            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }
    }
}
