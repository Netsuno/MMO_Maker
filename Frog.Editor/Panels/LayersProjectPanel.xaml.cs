using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace Frog.Editor.Panels;

public partial class LayersProjectPanel : System.Windows.Controls.UserControl
{
    private bool _suppressEvents;

    public event EventHandler<int>? LayerSelected;

    public event EventHandler<(int index, bool visible)>? LayerVisibilityChanged;

    public event EventHandler? RenameLayerRequested;

    public Action? AddLayerRequested;

    public Action? RemoveLayerRequested;

    public Action? ChangeEngineTypeRequested;

    public Action? ToggleLockRequested;

    public LayersProjectPanel()
    {
        InitializeComponent();
    }

    public int GetSelectedLayerIndex()
    {
        if (LayersListView.SelectedItem is LayerListRow row)
        {
            return row.Index;
        }

        return -1;
    }

    public void ApplyRows(IReadOnlyList<LayerListRow> rows, int selectedIndex)
    {
        _suppressEvents = true;
        try
        {
            LayersListView.ItemsSource = new ObservableCollection<LayerListRow>(rows);
            if (selectedIndex >= 0 && rows.Count > 0)
            {
                var pick = rows.FirstOrDefault(r => r.Index == selectedIndex) ?? rows[0];
                LayersListView.SelectedItem = pick;
                LayersListView.ScrollIntoView(pick);
            }
            else
            {
                LayersListView.SelectedItem = null;
            }
        }
        finally
        {
            _suppressEvents = false;
        }
    }

    private void LayersListView_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suppressEvents)
        {
            return;
        }

        if (LayersListView.SelectedItem is LayerListRow row)
        {
            LayerSelected?.Invoke(this, row.Index);
        }
    }

    private void LayersListView_OnMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (GetSelectedLayerIndex() >= 0)
        {
            RenameLayerRequested?.Invoke(this, EventArgs.Empty);
        }
    }

    private void LayerVisibleCheckBox_Click(object sender, RoutedEventArgs e)
    {
        if (_suppressEvents)
        {
            return;
        }

        if (sender is not System.Windows.Controls.CheckBox cb || cb.DataContext is not LayerListRow row)
        {
            return;
        }

        var vis = cb.IsChecked == true;
        row.Visible = vis;
        LayerVisibilityChanged?.Invoke(this, (row.Index, vis));
    }

    private void MenuAddLayer_Click(object sender, RoutedEventArgs e) => AddLayerRequested?.Invoke();

    private void MenuRemoveLayer_Click(object sender, RoutedEventArgs e) => RemoveLayerRequested?.Invoke();

    private void MenuRenameLayer_Click(object sender, RoutedEventArgs e) => RenameLayerRequested?.Invoke(this, EventArgs.Empty);

    private void MenuChangeEngineType_Click(object sender, RoutedEventArgs e) => ChangeEngineTypeRequested?.Invoke();

    private void MenuToggleLock_Click(object sender, RoutedEventArgs e) => ToggleLockRequested?.Invoke();
}
