using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Forms.Integration;
using System.Windows.Interop;
using Frog.Application.Maps;
using Frog.Application.Playtest;
using Frog.Core.Enums;
using Frog.Core.IO;
using Frog.Core.Models;
using Frog.Editor.Assets;
using Frog.Editor.Controls;
using Frog.Editor.Dialogs;
using Frog.Editor.Panels;
using Frog.Editor.Ui;

using Frog.Editor.Config;
using Frog.Editor.Interop;
using Frog.Editor.Services;

namespace Frog.Editor.Forms;

public sealed class MainForm : Form
{
    private readonly MenuStrip? _menuStrip;
    private readonly ToolStripMenuItem? _mnuUndo;
    private readonly ToolStripMenuItem? _mnuRedo;
    private readonly ToolStripMenuItem? _mnuShowEventMarkers;
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
    private readonly MapCanvas _canvas;
    private readonly MapMinimapControl _minimap;
    private Point _lastHoverTile;
    private int _lastPublishedFrogMapId = 1;
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
    private Phase8ContentPostgreSqlService? _phase8ContentService;
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
    private CancellationTokenSource? _playtestCts;
    private bool _playtestBusy;

    /// <summary>Colonne gauche (outils, cartes) pour hébergement dans un <c>WindowsFormsHost</c> WPF.</summary>
    internal Control LeftShellForWpf => _leftColumnPanel;

    /// <summary>Zone carte (bandeau + canevas + mini-carte).</summary>
    internal Control CenterShellForWpf => _wfMapDockPanel;

    /// <summary>Tuiles + couches + grille de propriétés.</summary>
    internal Control RightShellForWpf => _splitRightTileset;

    internal Map? GetCanvasMapForTest() => _canvas.Map;

    internal MapWorkspaceSession? GetWorkspaceSessionForTest() => _workspace;

    internal MapRepositoryCapabilities GetPersistenceCapabilitiesForTest() => _persistenceCapabilities;

    internal bool IsSaveInProgressForTest() => _saveInProgress;

    internal Task? PendingSaveOperationForTest => _pendingSaveOperation;

    internal bool HasUnsavedChangesForTest() => _workspace?.IsDirty == true;

    internal bool IsCloseConfirmedForTest() => _closeConfirmed;

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

    public MapUndoController UndoHistory => _canvas.History;

    /// <param name="embedAsWpfChild">Si vrai, la fenêtre est hébergée dans un <c>WindowsFormsHost</c> WPF (pas de chrome fenêtre).</param>
    public MainForm(bool embedAsWpfChild = false)
    {
        _embedAsWpfChild = embedAsWpfChild;
        _lastPublishedFrogMapId = EditorLocalWorkstate.ReadLastPublishedFrogMapId();
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

        if (!embedAsWpfChild)
        {
            FormClosing += (_, e) =>
            {
                if (_closeConfirmed || _workspace?.IsDirty != true)
                {
                    return;
                }

                e.Cancel = true;
                BeginInvoke(new Action(async () =>
                {
                    if (await ConfirmCloseAsync().ConfigureAwait(true))
                    {
                        _closeConfirmed = true;
                        Close();
                    }
                }));
            };
        }

        FormClosed += async (_, _) =>
        {
            // Fallback only — primary await is MainWindow coordinated close / Quit.
            try
            {
                await StopPlaytestAsync().ConfigureAwait(true);
            }
            catch
            {
                // best-effort cleanup
            }

            TilesetCache.Clear();
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
            mFile.DropDownItems.Add(new ToolStripMenuItem("Publier vers MariaDB… (héritage)", null, (_, _) => PublishMapToMariaDb()));
            mFile.DropDownItems.Add(new ToolStripMenuItem("Lancer le client Frog…", null, (_, _) => LaunchFrogGameClient()));
            _mnuPlaytest = new ToolStripMenuItem("Playtest (publier + serveur + client)…", null, async (_, _) => await StartPlaytestAsync())
            {
                ShortcutKeys = Keys.F5 | Keys.Control,
            };
            _mnuStopPlaytest = new ToolStripMenuItem("Arrêter le playtest", null, async (_, _) => await StopPlaytestAsync())
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

            var mResources = new ToolStripMenuItem("Ressources");
            mResources.DropDownItems.Add("Charger une image tuiles…", null, (_, _) => OpenTileset());

            var mMap = new ToolStripMenuItem("Carte");
            mMap.DropDownItems.Add("Valider la carte…", null, (_, _) => ValidateMap());
            mMap.DropDownItems.Add("Configurer warp sélectionné…", null, (_, _) => EditSelectedWarpDestination());
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

            menuStrip.Items.AddRange(new ToolStripItem[] { mFile, mEdit, mResources, mMap, mView });
            MainMenuStrip = menuStrip;
            _menuStrip = menuStrip;
            _mnuUndo = mnuUndo;
            _mnuRedo = mnuRedo;
            _mnuShowEventMarkers = mnuShowEventMarkers;

            var status = new StatusStrip { SizingGrip = false, GripStyle = ToolStripGripStyle.Hidden, Dock = DockStyle.Bottom };
            status.BackColor = EditorChrome.RibbonBg;
            status.Padding = new Padding(8, 4, 8, 4);
            var lblPos = new ToolStripStatusLabel("Tuile · x = 0, y = 0") { BorderSides = ToolStripStatusLabelBorderSides.None };
            lblPos.ForeColor = EditorChrome.LabelMuted;
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

        _canvas = new MapCanvas { Dock = DockStyle.Fill };
        _canvas.HoveredTileChanged += OnHoveredTileChanged;
        _canvas.ViewTransformChanged += OnCanvasViewTransformChanged;
        _canvas.TileClicked += OnTileClicked;
        _canvas.TileContextMenuRequested += OnTileContextMenuRequested;
        _canvas.MapReplaced += OnMapReplaced;
        _canvas.UndoHistoryChanged += UpdateUndoRedoButtons;
        _canvas.MapEdited += OnMapEdited;

        if (_mnuShowEventMarkers is not null)
        {
            _mnuShowEventMarkers.CheckedChanged += (_, _) =>
            {
                _canvas.ShowMapEventMarkers = _mnuShowEventMarkers.Checked;
                _canvas.Invalidate();
            };
        }

        _minimap = new MapMinimapControl
        {
            Anchor = AnchorStyles.Top | AnchorStyles.Right,
        };
        _minimap.Attach(_canvas);

        _leftToolsWpf = new EditorLeftToolsWpf();
        _leftToolsWpf.ToolChanged += tool =>
        {
            _canvas.ActiveTool = tool;
            _canvas.Invalidate();
        };
        _leftToolsWpf.TileTypeChanged += type => _canvas.SelectedTileType = type;
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
        _tilesetPickerWpf.SyncPaletteTileSize(_canvas.TileSize);
        _tilesetPickerElementHost = new ElementHost
        {
            Dock = DockStyle.Fill,
            BackColor = EditorChrome.SidebarBg,
            Margin = Padding.Empty,
            Child = _tilesetPickerWpf,
        };

        var tilesHost = new Panel { Dock = DockStyle.Fill, BackColor = EditorChrome.SidebarBg, Padding = new Padding(0) };
        tilesHost.Controls.Add(_tilesetPickerElementHost);

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

            if (_propGrid.SelectedObject is Map)
            {
                UpdateMapChromeLabels();
            }

            OnMapEdited();
            UpdateUndoRedoButtons();
            _propGridUndoCaptured = false;
        };
        _mapsProjectPanel.CurrentMapNodeSelected += (_, _) =>
        {
            if (_propGrid.SelectedObject is not Map && _canvas.Map is not null)
            {
                _propGrid.SelectedObject = _canvas.Map;
            }
        };
        _splitLayersProps.Panel2.Controls.Add(_propGrid);

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
        PushEditorStatusLine();
    }

    private Task? _workspaceInitTask;
    internal Task WorkspaceInitializationTask => _workspaceInitTask ?? Task.CompletedTask;

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

    private async System.Threading.Tasks.Task InitializeWorkspaceCoreAsync()
    {
        var bundle = EditorMapRepositoryFactory.CreateBundle();
        _mapRepository = bundle.Repository;
        _persistenceCapabilities = bundle.Capabilities;
        var eventBundle = EditorMapEventRepositoryFactory.CreateBundle();
        _mapEventService = eventBundle.Service;
        var phase8Bundle = EditorPhase8ContentRepositoryFactory.CreateBundle();
        _phase8ContentService = phase8Bundle.Service;
        _workspace = new MapWorkspaceSession(bundle.Repository);
        await _workspace.InitializeAsync().ConfigureAwait(true);
        ApplyWorkspaceMapToUi();
        UpdatePersistenceMenuState();
        PushEditorStatusLine();
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
        }
        finally
        {
            _suppressDirtyTracking = false;
        }

        PushEditorStatusLine();
    }

    private void OnPaletteStampChanged(Rectangle stampPixels)
    {
        _canvas.SelectedSrc = stampPixels.Location;
        var ts = Math.Max(1, _canvas.TileSize);
        _canvas.SelectedStampInTiles = new Size(
            Math.Max(1, stampPixels.Width / ts),
            Math.Max(1, stampPixels.Height / ts));
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
        var text =
            $"Tuile · x = {_lastHoverTile.X}, y = {_lastHoverTile.Y}    ·    Zoom {zoomPct} %{rev}{dirty}{busy}    ·    catalogue {backend}";
        if (_lblPos is not null)
        {
            _lblPos.Text = text;
        }

        TileHoverStatusChanged?.Invoke(text);
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

    private void UpdateMapChromeLabels()
    {
        if (_canvas.Map is null)
        {
            _lblMapWorkspaceTitle.Text = "Carte : —";
            return;
        }

        _lblMapWorkspaceTitle.Text =
            $"Carte : {_canvas.Map.Name}    ({_canvas.Map.Width} × {_canvas.Map.Height} tuiles)";
        _mapsProjectPanel.UpdateCurrentMapDisplayName(_canvas.Map.Name);
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

        if (ActiveControl is TextBoxBase)
        {
            return base.ProcessCmdKey(ref msg, keyData);
        }

        if (!ctrl && code == Keys.Escape)
        {
            _canvas.ClearSelection();
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
        PushEditorStatusLine();
    }

    private void OnMapReplaced()
    {
        RefreshLayersUi();
        _propGrid.SelectedObject = _canvas.Map;
        UpdateUndoRedoButtons();
        UpdateMapChromeLabels();
        OnMapEdited();
    }

    internal void DoUndo()
    {
        _canvas.PerformUndo();
        UpdateUndoRedoButtons();
    }

    internal void DoRedo()
    {
        _canvas.PerformRedo();
        UpdateUndoRedoButtons();
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

            for (var i = 0; i < _canvas.Map.Layers.Count; i++)
            {
                var l = _canvas.Map.Layers[i];
                rows.Add(new LayerListRow
                {
                    Index = i,
                    Visible = l.Visible,
                    Display = l.GetDisplayLabel(),
                    EngineType = l.LayerType.ToString(),
                    LockLabel = l.Locked ? "Oui" : "—",
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
            "Type moteur",
            "LayerType (Ground, Mask, Mask2, Fringe, Fringe2, Attributes) :",
            layer.LayerType.ToString());
        if (string.IsNullOrWhiteSpace(input))
        {
            return;
        }

        if (!Enum.TryParse(input, true, out LayerType type))
        {
            MessageBox.Show(GetDialogOwner(), "Valeur d’énumération non reconnue.", "Type moteur", MessageBoxButtons.OK, MessageBoxIcon.Warning);
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

        var map = new Map { Width = dlg.MapWidth, Height = dlg.MapHeight, Name = dlg.MapName };
        map.Layers.Add(new Layer { LayerType = LayerType.Ground });
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
        PushEditorStatusLine();
    }

    internal void OpenTileset()
    {
        using var ofd = new OpenFileDialog { Filter = "Images|*.png;*.jpg;*.jpeg;*.bmp" };
        if (ofd.ShowDialog(GetDialogOwner()) != DialogResult.OK)
        {
            return;
        }

        var id = TilesetCache.LoadFromFile(ofd.FileName);
        _canvas.ActiveTilesetId = id;
        RefreshTilesetList();
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
            await operation().ConfigureAwait(true);
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

        var serializer = new MapSerializer();
        var bytes = serializer.Serialize(_canvas.Map);
        File.WriteAllBytes(sfd.FileName, bytes);
        SaveTilesetManifestNextToMap(sfd.FileName);
        MessageBox.Show(GetDialogOwner(), "Carte et manifeste tilesets (.tilesets.json) exportés.", "Export", MessageBoxButtons.OK, MessageBoxIcon.Information);
    }

    private async System.Threading.Tasks.Task SaveMapCoreAsync()
    {
        if (_workspace is null || _canvas.Map is null)
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

        var result = await _workspace.SaveCurrentAsync(SaveMapIntent.SaveDraft).ConfigureAwait(true);
        await HandleSaveResultAsync(result, published: false).ConfigureAwait(true);
    }

    private async System.Threading.Tasks.Task PublishMapCoreAsync()
    {
        if (_workspace is null || _canvas.Map is null)
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
        PushEditorStatusLine();
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

            string serverExe;
            if (!string.IsNullOrWhiteSpace(EditorTestHooks.OverrideServerExePath))
            {
                serverExe = EditorTestHooks.OverrideServerExePath;
            }
            else if (!EditorFrogServerLauncher.TryResolveExecutable(out serverExe, out _))
            {
                LastPlaytestErrorForTest = "Frog.Server introuvable. Compilez le serveur (Release/Debug) ou indiquez le chemin.";
                _dialogService.ShowWarning(LastPlaytestErrorForTest, "Playtest");
                return;
            }

            string clientExe;
            if (!string.IsNullOrWhiteSpace(EditorTestHooks.OverrideClientExePath))
            {
                clientExe = EditorTestHooks.OverrideClientExePath;
            }
            else if (!EditorFrogClientLauncher.TryResolveExecutable(out clientExe))
            {
                LastPlaytestErrorForTest = "Frog.Client.exe introuvable.";
                _dialogService.ShowWarning(LastPlaytestErrorForTest, "Playtest");
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
            _playtestOrchestrator = new PlaytestOrchestrator(preparer, launcher);

            if (_workspace.CurrentMap is null)
            {
                LastPlaytestErrorForTest = "Aucune carte ouverte.";
                _dialogService.ShowWarning(LastPlaytestErrorForTest, "Playtest");
                return;
            }

            var map = _workspace.CurrentMap;
            var defaultX = Math.Clamp(_lastHoverTile.X, 0, Math.Max(0, map.Width - 1));
            var defaultY = Math.Clamp(_lastHoverTile.Y, 0, Math.Max(0, map.Height - 1));
            int spawnX;
            int spawnY;
            if (EditorTestHooks.OverrideSpawnTile is { } forcedSpawn)
            {
                spawnX = forcedSpawn.X;
                spawnY = forcedSpawn.Y;
            }
            else
            {
                using var spawnDlg = new Dialogs.PlaytestSpawnDialog(map.Width, map.Height, defaultX, defaultY);
                if (spawnDlg.ShowDialog(GetDialogOwner()) != DialogResult.OK)
                {
                    LastPlaytestErrorForTest = "Playtest annulé (spawn).";
                    return;
                }

                spawnX = spawnDlg.TileX;
                spawnY = spawnDlg.TileY;
            }

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
    }

    internal void PublishMapToMariaDb()
    {
        if (_canvas.Map is null)
        {
            MessageBox.Show(GetDialogOwner(), "Aucune carte chargée.", "Publication MariaDB", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        if (!_canvas.Map.Validate(out var err))
        {
            MessageBox.Show(GetDialogOwner(), err ?? "Carte invalide.", "Publication MariaDB", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        if (!EditorMariaDbConfig.TryGetEnabledConnection(out var connectionString, out var hint))
        {
            MessageBox.Show(GetDialogOwner(), hint, "Publication MariaDB", MessageBoxButtons.OK, MessageBoxIcon.Information);
            return;
        }

        var display = MapPublishNaming.ClampDisplayName(_canvas.Map.Name);
        var key = MapPublishNaming.SlugFromName(_canvas.Map.Name);
        using var dlg = new PublishMapDialog(display, key, _lastPublishedFrogMapId);
        if (dlg.ShowDialog(GetDialogOwner()) != DialogResult.OK)
        {
            return;
        }

        if (!dlg.TryValidate(out var verr))
        {
            MessageBox.Show(GetDialogOwner(), verr, "Publication MariaDB", MessageBoxButtons.OK, MessageBoxIcon.Warning);
            return;
        }

        try
        {
            var serializer = new MapSerializer();
            var bytes = serializer.Serialize(_canvas.Map);
            MariaMapBlobPublisher.UpsertMap(
                connectionString,
                dlg.PublishedMapId,
                dlg.PublishedMapKey,
                dlg.PublishedDisplayName,
                bytes);
            _lastPublishedFrogMapId = dlg.PublishedMapId;
            EditorLocalWorkstate.WriteLastPublishedFrogMapId(_lastPublishedFrogMapId);
            RefreshMapEventMarkers();
            MessageBox.Show(
                GetDialogOwner(),
                $"Carte publiée : frog_map id={dlg.PublishedMapId}, clé « {dlg.PublishedMapKey} ».",
                "Publication MariaDB",
                MessageBoxButtons.OK,
                MessageBoxIcon.Information);
        }
        catch (Exception ex)
        {
            MessageBox.Show(
                GetDialogOwner(),
                "Publication échouée : " + ex.Message,
                "Publication MariaDB",
                MessageBoxButtons.OK,
                MessageBoxIcon.Error);
        }
    }

    private void OnTileContextMenuRequested(Point tile)
    {
        _lastHoverTile = tile;
        PushEditorStatusLine();
        var menu = new ContextMenuStrip();
        menu.Closed += (_, _) => menu.Dispose();
        menu.Items.Add("Événements carte (cette tuile)…", null, (_, _) => BrowseMapEvents());
        menu.Show(Cursor.Position);
    }

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
        using var dlg = new MapEventsBrowseDialog(
            _mapEventService,
            mapId,
            defaultTileX: _lastHoverTile.X,
            defaultTileY: _lastHoverTile.Y);
        dlg.ShowDialog(GetDialogOwner());
        RefreshMapEventMarkers();
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
        if (_mapEventService is null || !_mapEventService.IsAvailable)
        {
            _canvas.MapEventMarkers = null;
            return;
        }

        if (_workspace?.CurrentMapId is not Guid mapId || mapId == Guid.Empty)
        {
            _canvas.MapEventMarkers = null;
            return;
        }

        try
        {
            var rows = _mapEventService.LoadPlacementsForMap(mapId);
            _canvas.MapEventMarkers = MapEventsPostgreSqlService.ToMarkerViews(rows);
        }
        catch
        {
            _canvas.MapEventMarkers = null;
        }
    }

    internal bool MapEventMarkersVisible
    {
        get => _canvas.ShowMapEventMarkers;
        set
        {
            _canvas.ShowMapEventMarkers = value;
            if (_mnuShowEventMarkers is not null)
            {
                _mnuShowEventMarkers.Checked = value;
            }

            _canvas.Invalidate();
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

        var manifest = new TilesetManifest();
        foreach (var (id, label) in TilesetCache.ListRegistered())
        {
            manifest.Entries.Add(new TilesetManifestEntry { Id = id, FileName = label });
        }

        var manifestPath = Path.Combine(dir, stem + ".tilesets.json");
        File.WriteAllBytes(manifestPath, TilesetManifestJson.Serialize(manifest));
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
        var serializer = new MapSerializer();
        var map = serializer.Deserialize(data);

        TilesetCache.Clear();
        var manifestOutcome = TryApplyTilesetManifestFromMapPath(mapPath);

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
        var panel2Min = 160;
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

        public int MapWidth => (int)_numW.Value;
        public int MapHeight => (int)_numH.Value;
        public string MapName => _txtName.Text.Trim();

        public NewMapDialog()
        {
            Text = "Nouvelle carte";
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(440, 260);
            ClientSize = new Size(480, 240);
            AutoScaleMode = AutoScaleMode.Dpi;
            Padding = new Padding(0);
            EditorChrome.ApplyFormChrome(this);

            var root = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 2,
                RowCount = 4,
                Padding = new Padding(20, 18, 20, 16),
            };
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 168f));
            root.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
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
            root.Controls.Add(buttons, 0, 3);

            Controls.Add(root);
            AcceptButton = btnOk;
            CancelButton = btnCancel;
        }
    }
}
