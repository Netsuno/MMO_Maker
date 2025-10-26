using System;
using System.Windows.Forms;

using Frog.Editor.Forms;

namespace Frog.Editor
{
    internal static class Program
    {
        [STAThread]
        private static void Main()
        {
            ApplicationConfiguration.Initialize();
            Application.Run(new MainForm());
        }
    }
}

