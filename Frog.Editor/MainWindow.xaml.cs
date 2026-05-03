using System.Windows;
using System.Windows.Forms;
using Frog.Editor.Forms;

namespace Frog.Editor;

public partial class MainWindow : Window
{
    private readonly MainForm _editor;

    public MainWindow()
    {
        InitializeComponent();
        _editor = new MainForm(embedAsWpfChild: true)
        {
            TopLevel = false,
            FormBorderStyle = FormBorderStyle.None,
            Dock = DockStyle.Fill,
        };
        _editor.FormClosed += (_, _) =>
        {
            if (Dispatcher.HasShutdownStarted)
            {
                return;
            }

            System.Windows.Application.Current.Shutdown();
        };
        _editor.TileHoverStatusChanged += OnTileHoverStatusChanged;
        _editor.UndoRedoStateChanged += OnUndoRedoStateChanged;
        EditorHost.Child = _editor;
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

    private void OnUndoRedoStateChanged(bool canUndo, bool canRedo)
    {
        if (!Dispatcher.CheckAccess())
        {
            Dispatcher.Invoke(() =>
            {
                MnuUndo.IsEnabled = canUndo;
                MnuRedo.IsEnabled = canRedo;
            });
            return;
        }

        MnuUndo.IsEnabled = canUndo;
        MnuRedo.IsEnabled = canRedo;
    }

    private void MenuNewMap_Click(object sender, RoutedEventArgs e) => _editor.CreateNewMap();

    private void MenuOpenMap_Click(object sender, RoutedEventArgs e) => _editor.LoadMap();

    private void MenuSaveMap_Click(object sender, RoutedEventArgs e) => _editor.SaveMap();

    private void MenuQuit_Click(object sender, RoutedEventArgs e) => System.Windows.Application.Current.Shutdown();

    private void MenuUndo_Click(object sender, RoutedEventArgs e) => _editor.DoUndo();

    private void MenuRedo_Click(object sender, RoutedEventArgs e) => _editor.DoRedo();

    private void MenuOpenTileset_Click(object sender, RoutedEventArgs e) => _editor.OpenTileset();

    private void MenuValidateMap_Click(object sender, RoutedEventArgs e) => _editor.ValidateMap();

    private void MenuResetView_Click(object sender, RoutedEventArgs e) => _editor.ResetMapView();
}
