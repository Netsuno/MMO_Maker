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

    /// <summary>Demande d’ouverture d’une carte du catalogue (<see cref="Guid"/>).</summary>
    public event EventHandler<Guid>? CatalogMapOpenRequested;

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

        if (item.Tag is Guid mapId)
        {
            CatalogMapOpenRequested?.Invoke(this, mapId);
        }
    }

    /// <summary>Reconstruit l’arbre monde à partir du catalogue applicatif.</summary>
    public void RefreshCatalog(IReadOnlyList<MapCatalogEntry> catalog, Guid? selectedMapId, string? localDraftName)
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
            var isCurrent = selectedMapId is Guid id && id == entry.MapId;
            var child = new TreeViewItem
            {
                Header = FormatEntry(entry),
                Tag = entry.MapId,
                Foreground = isCurrent ? CurrentMapBrush : DefaultMapBrush,
            };
            root.Items.Add(child);
            if (isCurrent)
            {
                toSelect = child;
            }
        }

        if (selectedMapId is null && !string.IsNullOrEmpty(localDraftName))
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

    /// <summary>Compatibilité : une seule carte locale sans catalogue.</summary>
    public void RefreshFromMap(string? mapName)
    {
        RefreshCatalog(Array.Empty<MapCatalogEntry>(), selectedMapId: null, localDraftName: mapName);
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

                if (child.Tag is Guid && child.IsSelected)
                {
                    var text = child.Header?.ToString() ?? string.Empty;
                    var sep = text.IndexOf("  ·  ", StringComparison.Ordinal);
                    if (sep > 0)
                    {
                        child.Header = FormatShortId((Guid)child.Tag) + "  " + mapName + text[sep..];
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
        return $"{FormatShortId(entry.MapId)}  {entry.Name}  ·  r{entry.Revision} ({status})";
    }

    private static string FormatShortId(Guid mapId) =>
        mapId.ToString("N")[..8].ToUpperInvariant();
}
