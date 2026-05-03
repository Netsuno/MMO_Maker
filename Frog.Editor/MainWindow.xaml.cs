using System.Windows;
using System.Windows.Forms;
using Frog.Editor.Forms;

namespace Frog.Editor;

public partial class MainWindow : Window
{
    public MainWindow()
    {
        InitializeComponent();
        var editor = new MainForm(embedAsWpfChild: true)
        {
            TopLevel = false,
            FormBorderStyle = FormBorderStyle.None,
            Dock = DockStyle.Fill,
        };
        editor.FormClosed += (_, _) =>
        {
            if (Dispatcher.HasShutdownStarted)
            {
                return;
            }

            System.Windows.Application.Current.Shutdown();
        };
        EditorHost.Child = editor;
    }
}
