using System.Windows.Forms;

namespace Frog.Editor.Interop;

/// <summary>Expose une fenêtre WPF comme <see cref="IWin32Window"/> pour les boîtes de dialogue WinForms.</summary>
internal sealed class Win32Window : System.Windows.Forms.IWin32Window
{
    public Win32Window(IntPtr handle) => Handle = handle;

    public IntPtr Handle { get; }
}
