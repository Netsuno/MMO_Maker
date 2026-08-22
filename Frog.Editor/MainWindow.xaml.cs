using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Forms;
using System.Windows.Input;
using Frog.Editor.Config;
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

    public static readonly RoutedUICommand CmdPublishMapToMariaDb = new(
        "Publier vers MariaDB… (héritage)",
        nameof(CmdPublishMapToMariaDb),
        typeof(MainWindow));

    public static readonly RoutedUICommand CmdLaunchFrogClient = new(
        "Lancer le client…",
        nameof(CmdLaunchFrogClient),
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

    public static readonly RoutedUICommand CmdOpenTileset = new(
        "Charger une image tuiles…",
        nameof(CmdOpenTileset),
        typeof(MainWindow));

    public static readonly RoutedUICommand CmdValidateMap = new(
        "Valider la carte…",
        nameof(CmdValidateMap),
        typeof(MainWindow));

    public static readonly RoutedUICommand CmdBrowseMapEvents = new(
        "Événements carte (MariaDB, héritage)…",
        nameof(CmdBrowseMapEvents),
        typeof(MainWindow));

    public static readonly RoutedUICommand CmdRefreshMapEventMarkers = new(
        "Actualiser marqueurs événements (MariaDB, héritage)",
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

        CommandBindings.Add(new CommandBinding(CmdNewMap, (_, _) => _editor.CreateNewMap()));
        CommandBindings.Add(new CommandBinding(CmdOpenMap, (_, _) => _editor.LoadMap()));
        CommandBindings.Add(new CommandBinding(CmdSaveMap, (_, _) => _editor.SaveMap(), (_, e) => e.CanExecute = _editor.CanExecuteSaveOrPublish()));
        CommandBindings.Add(new CommandBinding(CmdPublishMap, (_, _) => _editor.PublishMap(), (_, e) => e.CanExecute = _editor.CanExecuteSaveOrPublish()));
        CommandBindings.Add(new CommandBinding(CmdExportMap, (_, _) => _editor.ExportMapToFile()));
        CommandBindings.Add(new CommandBinding(CmdPublishMapToMariaDb, (_, _) => _editor.PublishMapToMariaDb()));
        CommandBindings.Add(new CommandBinding(CmdEditWarp, (_, _) => _editor.EditSelectedWarpDestination()));
        CommandBindings.Add(new CommandBinding(CmdLaunchFrogClient, (_, _) => _editor.LaunchFrogGameClient()));
        CommandBindings.Add(new CommandBinding(CmdQuit, (_, _) => System.Windows.Application.Current.Shutdown()));
        CommandBindings.Add(new CommandBinding(CmdUndo, (_, _) => _editor.DoUndo(), (_, e) => e.CanExecute = _editor.UndoHistory.CanUndo));
        CommandBindings.Add(new CommandBinding(CmdRedo, (_, _) => _editor.DoRedo(), (_, e) => e.CanExecute = _editor.UndoHistory.CanRedo));
        CommandBindings.Add(new CommandBinding(CmdOpenTileset, (_, _) => _editor.OpenTileset()));
        CommandBindings.Add(new CommandBinding(CmdValidateMap, (_, _) => _editor.ValidateMap()));
        CommandBindings.Add(new CommandBinding(CmdBrowseMapEvents, (_, _) => _editor.BrowseMapEventsFromMariaDb()));
        CommandBindings.Add(new CommandBinding(CmdRefreshMapEventMarkers, (_, _) => _editor.RefreshMapEventMarkersFromMariaDb()));
        CommandBindings.Add(new CommandBinding(CmdRefreshCatalog, async (_, _) => await _editor.RefreshMapCatalogAsync()));
        CommandBindings.Add(new CommandBinding(CmdResetView, (_, _) => _editor.ResetMapView()));
        CommandBindings.Add(new CommandBinding(CmdZoomIn, (_, _) => _editor.EditorZoomIn()));
        CommandBindings.Add(new CommandBinding(CmdZoomOut, (_, _) => _editor.EditorZoomOut()));

        Loaded += OnMainWindowLoaded;
        SizeChanged += (_, _) => _editor.NotifyWpfShellLayout();
        Closing += OnMainWindowClosing;
        Closed += OnMainWindowClosed;
    }

    private async void OnMainWindowClosing(object? sender, System.ComponentModel.CancelEventArgs e)
    {
        if (!await _editor.ConfirmCloseAsync().ConfigureAwait(true))
        {
            e.Cancel = true;
        }
    }

    private async void OnMainWindowLoaded(object sender, RoutedEventArgs e)
    {
        RestoreShellColumnWidths();
        CommandManager.InvalidateRequerySuggested();
        _editor.NotifyWpfShellLayout();
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
            _editor.RefreshMapEventMarkersFromMariaDb();
        }
        if (MnuShowEventMarkers is not null)
        {
            MnuShowEventMarkers.IsChecked = _editor.MapEventMarkersVisible;
        }
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

    private void OnToggleMapEventMarkers(object sender, RoutedEventArgs e)
    {
        if (sender is System.Windows.Controls.MenuItem mi)
        {
            _editor.MapEventMarkersVisible = mi.IsChecked == true;
        }
    }

    private void OnTileHoverStatusChanged(string text)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() => TileStatusText.Text = text);
            return;
        }

        TileStatusText.Text = text;
    }
}
