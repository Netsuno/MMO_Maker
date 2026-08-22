using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using Frog.Application.Maps;

namespace Frog.Editor.Panels;

public partial class MapsProjectPanel : System.Windows.Controls.UserControl
{
    private static readonly System.Windows.Media.Brush CurrentMapBrush =
        new SolidColorBrush(System.Windows.Media.Color.FromRgb(100, 190, 255));

    private static readonly System.Windows.Media.Brush DefaultMapBrush =
        new SolidColorBrush(System.Windows.Media.Color.FromRgb(235, 238, 245));

    /// <summary>La tuile « carte courante » (Tag <c>current</c>) est sélectionnée.</summary>
    public event EventHandler? CurrentMapNodeSelected;

    /// <summary>Demande d’ouverture d’une carte du catalogue (legacy id).</summary>
    public event EventHandler<int>? CatalogMapOpenRequested;

    public MapsProjectPanel()
    {
        InitializeComponent();
        ProjectTree.ProjectItemSelectionChanged += OnProjectTreeSelectionChanged;
    }

    private void OnProjectTreeSelectionChanged(object sender, RoutedPropertyChangedEventArgs<object?> e)
    {
        if (ProjectTree.SelectedItem is not TreeViewItem item)
        {
            return;
        }

        if (item.Tag as string == "current")
        {
            CurrentMapNodeSelected?.Invoke(this, EventArgs.Empty);
            return;
        }

        if (item.Tag is int legacyId)
        {
            CatalogMapOpenRequested?.Invoke(this, legacyId);
        }
    }

    /// <summary>Reconstruit l’arbre monde à partir du catalogue applicatif.</summary>
    public void RefreshCatalog(IReadOnlyList<MapCatalogEntry> catalog, int? selectedLegacyId, string? localDraftName)
    {
        ProjectTree.Items.Clear();
        var root = new TreeViewItem
        {
            Header = "Monde",
            IsExpanded = true,
            Foreground = DefaultMapBrush,
        };

        TreeViewItem? toSelect = null;
        foreach (var entry in catalog)
        {
            var isCurrent = selectedLegacyId is int id && id == entry.LegacyId;
            var child = new TreeViewItem
            {
                Header = FormatEntry(entry),
                Tag = entry.LegacyId,
                Foreground = isCurrent ? CurrentMapBrush : DefaultMapBrush,
            };
            root.Items.Add(child);
            if (isCurrent)
            {
                toSelect = child;
            }
        }

        if (selectedLegacyId is null && !string.IsNullOrEmpty(localDraftName))
        {
            var draft = new TreeViewItem
            {
                Header = $"•  {localDraftName} (brouillon local)",
                Tag = "current",
                Foreground = CurrentMapBrush,
            };
            root.Items.Add(draft);
            toSelect = draft;
        }

        ProjectTree.Items.Add(root);
        if (toSelect is not null)
        {
            toSelect.IsSelected = true;
        }
        else
        {
            root.IsSelected = true;
        }
    }

    /// <summary>Reconstruit l’arbre comme l’ancien <c>TreeView</c> WinForms (<see cref="Frog.Editor.Forms.MainForm.SyncMapsTree"/>).</summary>
    public void RefreshFromMap(string? mapName)
    {
        var local = string.IsNullOrEmpty(mapName)
            ? Array.Empty<MapCatalogEntry>()
            : Array.Empty<MapCatalogEntry>();
        RefreshCatalog(local, selectedLegacyId: null, localDraftName: mapName);
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
                if (c is not TreeViewItem child)
                {
                    continue;
                }

                if (child.Tag as string == "current")
                {
                    child.Header = $"•  {mapName} (brouillon local)";
                    return;
                }

                if (child.Tag is int && child.IsSelected)
                {
                    var text = child.Header?.ToString() ?? string.Empty;
                    var pipe = text.IndexOf(' ', text.IndexOf(' ') + 1);
                    // Conserve le préfixe "001  " si présent.
                    if (text.Length >= 5 && char.IsDigit(text[0]))
                    {
                        child.Header = text[..5] + mapName;
                    }
                    else
                    {
                        child.Header = mapName;
                    }

                    return;
                }
            }
        }
    }

    private static string FormatEntry(MapCatalogEntry entry)
    {
        var status = entry.Status == MapPublishStatus.Published ? "publié" : "brouillon";
        return $"{entry.LegacyId:D3}  {entry.Name}  ·  r{entry.Revision} ({status})";
    }
}
