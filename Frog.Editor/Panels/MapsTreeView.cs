using System.Windows;
using System.Windows.Controls;

namespace Frog.Editor.Panels;

/// <summary>Arbre projet : notifie les changements de sélection (WPF n’expose pas d’événement public sur <see cref="TreeView"/>).</summary>
internal sealed class MapsTreeView : System.Windows.Controls.TreeView
{
    public event RoutedPropertyChangedEventHandler<object?>? ProjectItemSelectionChanged;

    protected override void OnSelectedItemChanged(RoutedPropertyChangedEventArgs<object?> e)
    {
        base.OnSelectedItemChanged(e);
        ProjectItemSelectionChanged?.Invoke(this, e);
    }
}
