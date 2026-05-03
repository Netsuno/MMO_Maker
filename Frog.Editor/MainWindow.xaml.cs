using System.Windows;
using System.Windows.Forms;
using System.Windows.Input;
using Frog.Editor.Forms;

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
        "Enregistrer",
        nameof(CmdSaveMap),
        typeof(MainWindow),
        new InputGestureCollection { new KeyGesture(Key.S, ModifierKeys.Control) });

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
        CommandBindings.Add(new CommandBinding(CmdSaveMap, (_, _) => _editor.SaveMap()));
        CommandBindings.Add(new CommandBinding(CmdQuit, (_, _) => System.Windows.Application.Current.Shutdown()));
        CommandBindings.Add(new CommandBinding(CmdUndo, (_, _) => _editor.DoUndo(), (_, e) => e.CanExecute = _editor.UndoHistory.CanUndo));
        CommandBindings.Add(new CommandBinding(CmdRedo, (_, _) => _editor.DoRedo(), (_, e) => e.CanExecute = _editor.UndoHistory.CanRedo));
        CommandBindings.Add(new CommandBinding(CmdOpenTileset, (_, _) => _editor.OpenTileset()));
        CommandBindings.Add(new CommandBinding(CmdValidateMap, (_, _) => _editor.ValidateMap()));
        CommandBindings.Add(new CommandBinding(CmdResetView, (_, _) => _editor.ResetMapView()));
        CommandBindings.Add(new CommandBinding(CmdZoomIn, (_, _) => _editor.EditorZoomIn()));
        CommandBindings.Add(new CommandBinding(CmdZoomOut, (_, _) => _editor.EditorZoomOut()));

        Loaded += OnMainWindowLoaded;
        SizeChanged += (_, _) => _editor.NotifyWpfShellLayout();
        Closed += (_, _) => _editor.Close();
    }

    private void OnMainWindowLoaded(object sender, RoutedEventArgs e)
    {
        CommandManager.InvalidateRequerySuggested();
        _editor.NotifyWpfShellLayout();
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
