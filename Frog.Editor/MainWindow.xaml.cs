using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Threading;
using Frog.Application.Content;
using Frog.Application.Maps;
using Frog.Editor.Config;
using Frog.Editor.Enums;
using Frog.Editor.Forms;
using Frog.Editor.Services;

namespace Frog.Editor;

public partial class MainWindow : Window
{
    public static readonly RoutedUICommand CmdNewMap = new(
        "Nouvelle carte…",
        nameof(CmdNewMap),
        typeof(MainWindow),
        new InputGestureCollection { new KeyGesture(Key.N, ModifierKeys.Control) });

    public static readonly RoutedUICommand CmdOpenMap = new(
        "Ouvrir…",
        nameof(CmdOpenMap),
        typeof(MainWindow),
        new InputGestureCollection { new KeyGesture(Key.O, ModifierKeys.Control) });

    public static readonly RoutedUICommand CmdSaveMap = new(
        "Enregistrer (PostgreSQL)",
        nameof(CmdSaveMap),
        typeof(MainWindow),
        new InputGestureCollection { new KeyGesture(Key.S, ModifierKeys.Control) });

    public static readonly RoutedUICommand CmdPublishMap = new(
        "Publier (PostgreSQL)…",
        nameof(CmdPublishMap),
        typeof(MainWindow));

    public static readonly RoutedUICommand CmdExportMap = new(
        "Exporter fichier .fmap…",
        nameof(CmdExportMap),
        typeof(MainWindow));

    public static readonly RoutedUICommand CmdEditWarp = new(
        "Configurer warp sélectionné…",
        nameof(CmdEditWarp),
        typeof(MainWindow));

    public static readonly RoutedUICommand CmdLaunchFrogClient = new(
        "Lancer le client…",
        nameof(CmdLaunchFrogClient),
        typeof(MainWindow));

    public static readonly RoutedUICommand CmdPlaytest = new(
        "Tester (playtest)…",
        nameof(CmdPlaytest),
        typeof(MainWindow),
        new InputGestureCollection { new KeyGesture(Key.F5, ModifierKeys.Control) });

    public static readonly RoutedUICommand CmdStopPlaytest = new(
        "Arrêter le test",
        nameof(CmdStopPlaytest),
        typeof(MainWindow));

    public static readonly RoutedUICommand CmdQuit = new("_Quitter", nameof(CmdQuit), typeof(MainWindow));

    public static readonly RoutedUICommand CmdUndo = new(
        "Annuler",
        nameof(CmdUndo),
        typeof(MainWindow),
        new InputGestureCollection { new KeyGesture(Key.Z, ModifierKeys.Control) });

    public static readonly RoutedUICommand CmdRedo = new(
        "Rétablir",
        nameof(CmdRedo),
        typeof(MainWindow),
        new InputGestureCollection { new KeyGesture(Key.Y, ModifierKeys.Control) });

    public static readonly RoutedUICommand CmdMarkTilesAnimated = new(
        "Animer la sélection de tuiles",
        nameof(CmdMarkTilesAnimated),
        typeof(MainWindow));

    public static readonly RoutedUICommand CmdClearTilesAnimated = new(
        "Retirer l’animation de la sélection",
        nameof(CmdClearTilesAnimated),
        typeof(MainWindow));

    public static readonly RoutedUICommand CmdEditAutotile = new(
        Frog.Core.Maps.TileAssetFlagLabels.EditMenu,
        nameof(CmdEditAutotile),
        typeof(MainWindow));

    public static readonly RoutedUICommand CmdOpenTileset = new(
        "Charger une image tuiles…",
        nameof(CmdOpenTileset),
        typeof(MainWindow));

    public static readonly RoutedUICommand CmdImportTileAssetSheet = new(
        "Importer une feuille TileAsset…",
        nameof(CmdImportTileAssetSheet),
        typeof(MainWindow));

    public static readonly RoutedUICommand CmdConvertMapToTileAsset = new(
        "Passer cette carte en TileAsset (v6)…",
        nameof(CmdConvertMapToTileAsset),
        typeof(MainWindow));

    public static readonly RoutedUICommand CmdGameData = new(
        "Données de jeu…",
        nameof(CmdGameData),
        typeof(MainWindow));

    public static readonly RoutedUICommand CmdValidateMap = new(
        "Valider la carte…",
        nameof(CmdValidateMap),
        typeof(MainWindow));

    public static readonly RoutedUICommand CmdShowTransferIssues = new(
        "Vérifier les transferts…",
        nameof(CmdShowTransferIssues),
        typeof(MainWindow));

    public static readonly RoutedUICommand CmdEraserTool = new(
        "Outil gomme",
        nameof(CmdEraserTool),
        typeof(MainWindow));

    public static readonly RoutedUICommand CmdMapProperties = new(
        "Propriétés de la carte…",
        nameof(CmdMapProperties),
        typeof(MainWindow));

    public static readonly RoutedUICommand CmdMapResizeShift = new(
        MapResizeShift.CommandLabel,
        nameof(CmdMapResizeShift),
        typeof(MainWindow));

    public static readonly RoutedUICommand CmdFillTool = new(
        "Outil remplissage",
        nameof(CmdFillTool),
        typeof(MainWindow));

    public static readonly RoutedUICommand CmdRectangleTool = new(
        "Outil rectangle",
        nameof(CmdRectangleTool),
        typeof(MainWindow));

    public static readonly RoutedUICommand CmdLineTool = new(
        "Outil ligne",
        nameof(CmdLineTool),
        typeof(MainWindow));

    public static readonly RoutedUICommand CmdSpawnTool = new(
        "Outil point de départ",
        nameof(CmdSpawnTool),
        typeof(MainWindow));

    public static readonly RoutedUICommand CmdPrefabTool = new(
        "Outil prefab / objet",
        nameof(CmdPrefabTool),
        typeof(MainWindow));

    public static readonly RoutedUICommand CmdPlaceTool = new(
        "Outil entités",
        nameof(CmdPlaceTool),
        typeof(MainWindow));

    public static readonly RoutedUICommand CmdRegionTool = new(
        "Outil région",
        nameof(CmdRegionTool),
        typeof(MainWindow));

    public static readonly RoutedUICommand CmdCopySelection = new(
        "Copier la sélection",
        nameof(CmdCopySelection),
        typeof(MainWindow));

    public static readonly RoutedUICommand CmdCutSelection = new(
        "Couper la sélection",
        nameof(CmdCutSelection),
        typeof(MainWindow));

    public static readonly RoutedUICommand CmdPasteSelection = new(
        "Coller la sélection",
        nameof(CmdPasteSelection),
        typeof(MainWindow));

    public static readonly RoutedUICommand CmdSaveSelectionTemplate = new(
        "Enregistrer la sélection comme modèle…",
        nameof(CmdSaveSelectionTemplate),
        typeof(MainWindow));

    public static readonly RoutedUICommand CmdSaveMapTemplate = new(
        "Enregistrer la carte comme modèle…",
        nameof(CmdSaveMapTemplate),
        typeof(MainWindow));

    public static readonly RoutedUICommand CmdStampTemplate = new(
        "Poser un modèle…",
        nameof(CmdStampTemplate),
        typeof(MainWindow));

    public static readonly RoutedUICommand CmdRotateSelection = new(
        "Rotation 90°",
        nameof(CmdRotateSelection),
        typeof(MainWindow));

    public static readonly RoutedUICommand CmdMirrorHorizontal = new(
        "Miroir horizontal",
        nameof(CmdMirrorHorizontal),
        typeof(MainWindow));

    public static readonly RoutedUICommand CmdMirrorVertical = new(
        "Miroir vertical",
        nameof(CmdMirrorVertical),
        typeof(MainWindow));

    public static readonly RoutedUICommand CmdTilePipette = new(
        "Pipette tuile",
        nameof(CmdTilePipette),
        typeof(MainWindow));

    public static readonly RoutedUICommand CmdQuickTalkingNpc = new(
        "PNJ rapide…",
        nameof(CmdQuickTalkingNpc),
        typeof(MainWindow));

    public static readonly RoutedUICommand CmdQuickChest = new(
        "Coffre…",
        nameof(CmdQuickChest),
        typeof(MainWindow));

    public static readonly RoutedUICommand CmdQuickDoor = new(
        "Porte…",
        nameof(CmdQuickDoor),
        typeof(MainWindow));

    public static readonly RoutedUICommand CmdQuickInn = new(
        "Auberge…",
        nameof(CmdQuickInn),
        typeof(MainWindow));

    public static readonly RoutedUICommand CmdBrowseMapEvents = new(
        "Événements carte…",
        nameof(CmdBrowseMapEvents),
        typeof(MainWindow));

    public static readonly RoutedUICommand CmdBrowsePhase8Content = new(
        "Contenu Phase 8 (PostgreSQL)…",
        nameof(CmdBrowsePhase8Content),
        typeof(MainWindow));

    public static readonly RoutedUICommand CmdRefreshMapEventMarkers = new(
        "Actualiser marqueurs événements",
        nameof(CmdRefreshMapEventMarkers),
        typeof(MainWindow));

    public static readonly RoutedUICommand CmdRefreshCatalog = new(
        "Actualiser le catalogue",
        nameof(CmdRefreshCatalog),
        typeof(MainWindow),
        new InputGestureCollection { new KeyGesture(Key.F5) });

    public static readonly RoutedUICommand CmdResetView = new(
        "Réinitialiser la vue (zoom 100 %)",
        nameof(CmdResetView),
        typeof(MainWindow));

    public static readonly RoutedUICommand CmdZoomIn = new(
        "Zoom avant",
        nameof(CmdZoomIn),
        typeof(MainWindow),
        new InputGestureCollection { new KeyGesture(Key.Add, ModifierKeys.Control), new KeyGesture(Key.OemPlus, ModifierKeys.Control) });

    public static readonly RoutedUICommand CmdZoomOut = new(
        "Zoom arrière",
        nameof(CmdZoomOut),
        typeof(MainWindow),
        new InputGestureCollection { new KeyGesture(Key.Subtract, ModifierKeys.Control), new KeyGesture(Key.OemMinus, ModifierKeys.Control) });

    private readonly MainForm _editor;

    internal MainForm EditorForm => _editor;

    public MainWindow()
    {
        InitializeComponent();
        _editor = new MainForm(embedAsWpfChild: true)
        {
            TopLevel = false,
            FormBorderStyle = FormBorderStyle.None,
            ShowInTaskbar = false,
        };
        _editor.SetWpfOwnerWindow(this);
        HostLeft.Child = _editor.LeftShellForWpf;
        HostCenter.Child = _editor.CenterShellForWpf;
        HostRight.Child = _editor.RightShellForWpf;

        _editor.TileHoverStatusChanged += OnTileHoverStatusChanged;
        _editor.UndoRedoStateChanged += (_, _) => Dispatcher.Invoke(CommandManager.InvalidateRequerySuggested);
        _editor.PlaytestStateChanged += () => Dispatcher.Invoke(CommandManager.InvalidateRequerySuggested);

        CommandBindings.Add(new CommandBinding(CmdNewMap, (_, _) => _editor.CreateNewMap()));
        CommandBindings.Add(new CommandBinding(CmdOpenMap, (_, _) => _editor.LoadMap()));
        CommandBindings.Add(new CommandBinding(CmdSaveMap, (_, _) => _editor.SaveMap(), (_, e) => e.CanExecute = _editor.CanExecuteSaveOrPublish()));
        CommandBindings.Add(new CommandBinding(CmdPublishMap, (_, _) => _editor.PublishMap(), (_, e) => e.CanExecute = _editor.CanExecuteSaveOrPublish()));
        CommandBindings.Add(new CommandBinding(CmdExportMap, (_, _) => _editor.ExportMapToFile()));
        CommandBindings.Add(new CommandBinding(CmdEditWarp, (_, _) => _editor.EditSelectedWarpDestination()));
        CommandBindings.Add(new CommandBinding(CmdLaunchFrogClient, (_, _) => _editor.LaunchFrogGameClient()));
        CommandBindings.Add(new CommandBinding(
            CmdPlaytest,
            async (_, _) => await _editor.StartPlaytestAsync(),
            (_, e) => e.CanExecute = !_editor.IsPlaytestBusyForTest() && !_editor.IsPlaytestActiveForTest()));
        CommandBindings.Add(new CommandBinding(CmdStopPlaytest, async (_, _) => await _editor.StopPlaytestAsync(),
            (_, e) => e.CanExecute = _editor.IsPlaytestActiveForTest() || _editor.IsPlaytestBusyForTest()));
        CommandBindings.Add(new CommandBinding(CmdQuit, (_, _) => Close()));
        CommandBindings.Add(new CommandBinding(CmdUndo, (_, _) => _editor.DoUndo(), (_, e) => e.CanExecute = _editor.UndoHistory.CanUndo));
        CommandBindings.Add(new CommandBinding(CmdRedo, (_, _) => _editor.DoRedo(), (_, e) => e.CanExecute = _editor.UndoHistory.CanRedo));
        CommandBindings.Add(new CommandBinding(CmdOpenTileset, (_, _) => _editor.OpenTileset()));
        CommandBindings.Add(new CommandBinding(CmdImportTileAssetSheet, (_, _) => _editor.ImportTileAssetSheet()));
        CommandBindings.Add(new CommandBinding(CmdConvertMapToTileAsset, (_, _) => _editor.ConvertCurrentMapToTileAsset()));
        CommandBindings.Add(new CommandBinding(CmdMarkTilesAnimated, (_, _) => _editor.MarkSelectedTilesAnimated()));
        CommandBindings.Add(new CommandBinding(CmdClearTilesAnimated, (_, _) => _editor.ClearSelectedTilesAnimated()));
        CommandBindings.Add(new CommandBinding(CmdEditAutotile, (_, _) => _editor.BeginAutotileEdit()));
        CommandBindings.Add(new CommandBinding(CmdGameData, (_, _) => OpenGameData()));
        CommandBindings.Add(new CommandBinding(CmdValidateMap, (_, _) => _editor.ValidateMap()));
        CommandBindings.Add(new CommandBinding(CmdShowTransferIssues, (_, _) => _editor.ShowTransferIssues()));
        CommandBindings.Add(new CommandBinding(CmdEraserTool, (_, _) => _editor.SelectEditorTool(EditorTool.Eraser)));
        CommandBindings.Add(new CommandBinding(CmdMapProperties, (_, _) => _editor.ShowMapProperties()));
        CommandBindings.Add(new CommandBinding(CmdMapResizeShift, (_, _) => _editor.ShowMapResizeShift()));
        CommandBindings.Add(new CommandBinding(CmdFillTool, (_, _) => _editor.SelectEditorTool(EditorTool.Fill)));
        CommandBindings.Add(new CommandBinding(CmdRectangleTool, (_, _) => _editor.SelectEditorTool(EditorTool.Rectangle)));
        CommandBindings.Add(new CommandBinding(CmdLineTool, (_, _) => _editor.SelectEditorTool(EditorTool.Line)));
        CommandBindings.Add(new CommandBinding(CmdSpawnTool, (_, _) => _editor.SelectEditorTool(EditorTool.Spawn)));
        CommandBindings.Add(new CommandBinding(CmdPrefabTool, (_, _) => _editor.SelectEditorTool(EditorTool.Prefab)));
        CommandBindings.Add(new CommandBinding(CmdPlaceTool, (_, _) => _editor.SelectEditorTool(EditorTool.Place)));
        CommandBindings.Add(new CommandBinding(CmdRegionTool, (_, _) => _editor.SelectEditorTool(EditorTool.Region)));
        CommandBindings.Add(new CommandBinding(CmdCopySelection, (_, e) => _editor.CopyTileSelection(ActiveLayerMenu(e))));
        CommandBindings.Add(new CommandBinding(CmdCutSelection, (_, e) => _editor.CutTileSelection(ActiveLayerMenu(e))));
        CommandBindings.Add(new CommandBinding(CmdPasteSelection, (_, e) => _editor.PasteTileSelection(ActiveLayerMenu(e))));
        CommandBindings.Add(new CommandBinding(CmdRotateSelection, (_, _) => _editor.TryRotateSelection90()));
        CommandBindings.Add(new CommandBinding(CmdMirrorHorizontal, (_, _) => _editor.TryMirrorSelectionHorizontal()));
        CommandBindings.Add(new CommandBinding(CmdMirrorVertical, (_, _) => _editor.TryMirrorSelectionVertical()));
        CommandBindings.Add(new CommandBinding(CmdTilePipette, (_, _) => _editor.TryPipetteAtHover()));
        CommandBindings.Add(new CommandBinding(CmdSaveSelectionTemplate, (_, _) => _editor.SaveSelectionAsMapTemplate()));
        CommandBindings.Add(new CommandBinding(CmdSaveMapTemplate, (_, _) => _editor.SaveCurrentMapAsTemplate()));
        CommandBindings.Add(new CommandBinding(CmdStampTemplate, (_, _) => _editor.PromptStampMapTemplate()));
        CommandBindings.Add(new CommandBinding(CmdQuickTalkingNpc, (_, _) => _editor.OpenQuickTalkingNpc()));
        CommandBindings.Add(new CommandBinding(CmdQuickChest, (_, _) => _editor.OpenQuickEventPreset(QuickEventPresetKind.Chest)));
        CommandBindings.Add(new CommandBinding(CmdQuickDoor, (_, _) => _editor.OpenQuickEventPreset(QuickEventPresetKind.Door)));
        CommandBindings.Add(new CommandBinding(CmdQuickInn, (_, _) => _editor.OpenQuickEventPreset(QuickEventPresetKind.Inn)));
        CommandBindings.Add(new CommandBinding(CmdBrowseMapEvents, (_, _) => _editor.BrowseMapEvents()));
        CommandBindings.Add(new CommandBinding(CmdBrowsePhase8Content, (_, _) => _editor.BrowsePhase8Content()));
        CommandBindings.Add(new CommandBinding(CmdRefreshMapEventMarkers, (_, _) => _editor.RefreshMapEventMarkers()));
        CommandBindings.Add(new CommandBinding(CmdRefreshCatalog, async (_, _) => await _editor.RefreshMapCatalogAsync()));
        CommandBindings.Add(new CommandBinding(CmdResetView, (_, _) => _editor.ResetMapView()));
        CommandBindings.Add(new CommandBinding(CmdZoomIn, (_, _) => _editor.EditorZoomIn()));
        CommandBindings.Add(new CommandBinding(CmdZoomOut, (_, _) => _editor.EditorZoomOut()));
        _editor.MapEventNamesVisibilityChanged += SyncMapEventNamesMenu;

        PreviewKeyDown += OnPreviewToolHotkey;
        PreviewKeyUp += OnPreviewShapeModifier;
        Loaded += OnMainWindowLoaded;
        SizeChanged += (_, _) => _editor.NotifyWpfShellLayout();
        Closing += OnMainWindowClosing;
        Closed += OnMainWindowClosed;
    }

    private bool _closingAfterConfirm;
    private bool _allowCloseWithoutPrompt;
    private bool _closePromptInFlight;

    private void OpenGameData()
    {
        var dlg = new Forms.GameData.GameDataForm();
        if (EditorTestHooks.GameDataNonModalForTest)
        {
            dlg.Shown += (_, _) => EditorTestHooks.OnGameDataFormShown?.Invoke(dlg);
            dlg.Show();
            return;
        }

        using (dlg)
        {
            dlg.ShowDialog();
        }
    }

    internal void AllowCloseWithoutPromptForTest()
    {
        _allowCloseWithoutPrompt = true;
        _closingAfterConfirm = true;
    }

    private void OnMainWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (_closingAfterConfirm || _allowCloseWithoutPrompt)
        {
            return;
        }

        // Every close — clean, dirty, playtest, or pending — goes through the coordinator
        // so StopPlaytestAsync shares the same global cleanup deadline.
        e.Cancel = true;
        if (_closePromptInFlight)
        {
            return;
        }

        _closePromptInFlight = true;
        Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(async () =>
        {
            try
            {
                if (!await _editor.TryCoordinatedShutdownAsync().ConfigureAwait(true))
                {
                    return;
                }

                _closingAfterConfirm = true;
                Close();
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    this,
                    "Erreur lors de la fermeture : " + ex.Message,
                    "MMO Maker",
                    MessageBoxButton.OK,
                    MessageBoxImage.Warning);
            }
            finally
            {
                _closePromptInFlight = false;
            }
        }));
    }

    private async void OnMainWindowLoaded(object sender, RoutedEventArgs e)
    {
        RestoreShellColumnWidths();
        CommandManager.InvalidateRequerySuggested();
        _editor.NotifyWpfShellLayout();
        if (EditorTestHooks.PackagedSmokeLaunch)
        {
            AllowCloseWithoutPromptForTest();
            _ = Dispatcher.BeginInvoke(
                new Action(() =>
                {
                    Close();
                    System.Windows.Application.Current?.Shutdown(0);
                }),
                DispatcherPriority.ApplicationIdle);
            return;
        }

        try
        {
            await _editor.InitializeWorkspaceAsync();
        }
        catch (System.Exception ex)
        {
            System.Windows.MessageBox.Show(
                this,
                "Échec d’initialisation du catalogue cartes :\n" + ex.Message,
                "MMO Maker",
                MessageBoxButton.OK,
                MessageBoxImage.Warning);
        }

        if (!EditorTestHooks.SkipMariaDbOnStartup)
        {
            _editor.RefreshMapEventMarkers();
        }
        if (MnuShowEventMarkers is not null)
        {
            MnuShowEventMarkers.IsChecked = _editor.MapEventMarkersVisible;
        }

        SyncMapEventNamesMenu();
    }

    private void OnMainWindowClosed(object? sender, System.EventArgs e)
    {
        PersistShellColumnWidths();
        _editor.Close();
    }

    private void OnShellSplitterDragCompleted(object sender, DragCompletedEventArgs e) =>
        PersistShellColumnWidths();

    private void RestoreShellColumnWidths()
    {
        if (!EditorLocalWorkstate.TryReadShellColumnWidths(out var left, out var right))
        {
            return;
        }

        ColLeft.Width = new GridLength(left, GridUnitType.Pixel);
        ColRight.Width = new GridLength(right, GridUnitType.Pixel);
    }

    private void PersistShellColumnWidths()
    {
        var left = ColLeft.ActualWidth;
        var right = ColRight.ActualWidth;
        if (left > 0 && right > 0)
        {
            EditorLocalWorkstate.WriteShellColumnWidths(left, right);
        }
    }

    private void OnToggleAnimPreview(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.MenuItem mi)
        {
            _editor.AnimatedTilePreviewVisible = mi.IsChecked == true;
        }
    }

    private void OnToggleAutotileJoin(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.MenuItem mi)
        {
            _editor.AutotileJoin = mi.IsChecked == true;
        }
    }

    private void OnToggleMapEventMarkers(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.MenuItem mi)
        {
            _editor.MapEventMarkersVisible = mi.IsChecked == true;
        }
    }

    private bool _syncingMapEventNamesMenu;

    private void OnToggleMapEventNames(object sender, RoutedEventArgs e)
    {
        if (_syncingMapEventNamesMenu)
        {
            return;
        }

        if (sender is System.Windows.Controls.MenuItem mi)
        {
            _editor.MapEventNamesVisible = mi.IsChecked == true;
        }
    }

    private void SyncMapEventNamesMenu()
    {
        if (MnuShowEventNames is null || MnuShowEventNames.IsChecked == _editor.MapEventNamesVisible)
        {
            return;
        }

        _syncingMapEventNamesMenu = true;
        try
        {
            MnuShowEventNames.IsChecked = _editor.MapEventNamesVisible;
        }
        finally
        {
            _syncingMapEventNamesMenu = false;
        }
    }

    private void OnPreviewToolHotkey(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key is Key.LeftShift or Key.RightShift)
        {
            _editor.RefreshShapePreview();
        }

        if (Keyboard.FocusedElement is System.Windows.Controls.MenuItem)
        {
            return;
        }

        if (Keyboard.FocusedElement is System.Windows.Controls.TextBox || _editor.IsPrefabSearchFocused)
        {
            if (Keyboard.Modifiers == ModifierKeys.None && e.Key == Key.Escape && _editor.TryHandlePrefabEscape())
            {
                e.Handled = true;
            }

            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.None && e.Key == Key.Escape && _editor.TryHandlePrefabEscape())
        {
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.None && e.Key == Key.Escape && _editor.TryCancelShapeGesture())
        {
            e.Handled = true;
            return;
        }

        if (_editor.ContainsFocus && EditorTextInputFocus.ShouldIgnoreToolHotkeys(_editor.ActiveControl))
        {
            return;
        }

        if (e.Key == Key.Escape && Keyboard.Modifiers == ModifierKeys.None && _editor.CancelQuickNpcPlacement(userInitiated: true))
        {
            e.Handled = true;
            return;
        }

        if (EditorToolHotkeys.TryResolveWpf(e.Key, Keyboard.Modifiers, out var tool))
        {
            _editor.SelectEditorTool(tool);
            e.Handled = true;
            return;
        }

        if (Keyboard.Modifiers == ModifierKeys.None && e.Key is Key.OemOpenBrackets or Key.Oem6)
        {
            _editor.CycleSelectedPrefabFacingForTest(next: e.Key == Key.Oem6);
            e.Handled = true;
            return;
        }

        if (TryForwardEditorShortcut(e.Key, Keyboard.Modifiers))
        {
            e.Handled = true;
        }
    }

    private bool TryForwardEditorShortcut(Key key, ModifierKeys modifiers)
    {
        var alt = (modifiers & ModifierKeys.Alt) != 0;
        var ctrl = (modifiers & ModifierKeys.Control) != 0;
        var shift = (modifiers & ModifierKeys.Shift) != 0;
        if (alt)
        {
            return false;
        }

        Keys code;
        if (!ctrl && key is Key.Q or Key.H or Key.V or Key.I or Key.Delete)
        {
            code = key switch
            {
                Key.Q => Keys.Q,
                Key.H => Keys.H,
                Key.V => Keys.V,
                Key.I => Keys.I,
                _ => Keys.Delete,
            };
        }
        else if (ctrl && key is Key.C or Key.X or Key.V)
        {
            code = key switch
            {
                Key.C => Keys.C,
                Key.X => Keys.X,
                _ => Keys.V,
            };
        }
        else
        {
            return false;
        }

        var keyData = code;
        if (ctrl)
        {
            keyData |= Keys.Control;
        }

        if (shift)
        {
            keyData |= Keys.Shift;
        }

        return _editor.TryProcessCmdKeyForTest(keyData);
    }

    private static bool ActiveLayerMenu(ExecutedRoutedEventArgs e)
        => e.Parameter is string value && string.Equals(value, "layer", StringComparison.Ordinal);

    private void OnTileHoverStatusChanged(string text)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => OnTileHoverStatusChanged(text));
            return;
        }

        TileStatusText.Text = text;
        TileStatusText.ToolTip = text;
        TileStatusText.Foreground = text.Contains("à corriger", StringComparison.Ordinal)
            ? StatusWarnBrush
            : StatusBrush;
    }

    private static readonly SolidColorBrush StatusBrush = CreateFrozenBrush(0xEB, 0xEE, 0xF5);
    private static readonly SolidColorBrush StatusWarnBrush = CreateFrozenBrush(0xFF, 0xBA, 0x5C);

    private static SolidColorBrush CreateFrozenBrush(byte red, byte green, byte blue)
    {
        var brush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(red, green, blue));
        brush.Freeze();
        return brush;
    }

    private void OnPreviewShapeModifier(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key is Key.LeftShift or Key.RightShift)
        {
            _editor.RefreshShapePreview();
        }
    }
}
