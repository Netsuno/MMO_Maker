using System;
using System.Windows.Forms;

using Frog.Editor.Forms;
using Frog.Editor.Ui;

namespace Frog.Editor
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            ApplicationConfiguration.Initialize();
            EditorChrome.ApplyGlobalToolstripTheme();
            Application.Run(new MainForm());
        }
    }
}

