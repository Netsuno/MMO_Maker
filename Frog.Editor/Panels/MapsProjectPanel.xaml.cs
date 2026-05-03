using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace Frog.Editor.Panels;

public partial class MapsProjectPanel : System.Windows.Controls.UserControl
{
    private static readonly System.Windows.Media.Brush CurrentMapBrush =
        new SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 190, 255));

    /// <summary>La tuile « carte courante » (Tag <c>current</c>) est sélectionnée.</summary>
    public event EventHandler? CurrentMapNodeSelected;

    public MapsProjectPanel()
    {
        InitializeComponent();
        ProjectTree.ProjectItemSelectionChanged += OnProjectTreeSelectionChanged;
    }

    private void OnProjectTreeSelectionChanged(object sender, RoutedPropertyChangedEventArgs<object?> e)
    {
        if (ProjectTree.SelectedItem is TreeViewItem item && item.Tag as string == "current")
        {
            CurrentMapNodeSelected?.Invoke(this, EventArgs.Empty);
        }
    }

    /// <summary>Reconstruit l’arbre comme l’ancien <c>TreeView</c> WinForms (<see cref="Frog.Editor.Forms.MainForm.SyncMapsTree"/>).</summary>
    public void RefreshFromMap(string? mapName)
    {
        ProjectTree.Items.Clear();
        var root = new TreeViewItem
        {
            Header = "Cartes du projet",
            IsExpanded = true,
            Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(235, 238, 245)),
        };

        if (!string.IsNullOrEmpty(mapName))
        {
            var child = new TreeViewItem
            {
                Header = $"001  {mapName}",
                Tag = "current",
                Foreground = CurrentMapBrush,
            };
            root.Items.Add(child);
            ProjectTree.Items.Add(root);
            child.IsSelected = true;
            child.Focus();
        }
        else
        {
            ProjectTree.Items.Add(root);
            root.IsSelected = true;
        }
    }

    /// <summary>Met à jour uniquement le libellé de la carte courante (renommage).</summary>
    public void UpdateCurrentMapDisplayName(string mapName)
    {
        foreach (var o in ProjectTree.Items)
        {
            if (o is not TreeViewItem root)
            {
                continue;
            }

            foreach (var c in root.Items)
            {
                if (c is TreeViewItem child && child.Tag as string == "current")
                {
                    child.Header = $"001  {mapName}";
                    return;
                }
            }
        }
    }
}
