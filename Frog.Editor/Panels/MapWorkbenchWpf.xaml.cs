using System.Windows.Forms;

namespace Frog.Editor.Panels;

/// <summary>
/// Enveloppe WPF du plan de travail carte : le rendu tuile reste dans WinForms jusqu’à migration complète.
/// </summary>
public partial class MapWorkbenchWpf : System.Windows.Controls.UserControl
{
    public MapWorkbenchWpf()
    {
        InitializeComponent();
    }

    public void AttachWinFormsSurface(Control surface)
    {
        MapSurfaceHost.Child = surface;
    }
}
