using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Frog.Core.Maps;

namespace Frog.Editor.Panels;

public partial class LayersProjectPanel : System.Windows.Controls.UserControl
{
    private bool _suppressEvents;

    public event EventHandler<int>? LayerSelected;

    public event EventHandler<(int index, bool visible)>? LayerVisibilityChanged;

    public event EventHandler<(int index, bool locked)>? LayerLockChanged;

    public event EventHandler<(int index, float opacity)>? LayerPreviewOpacityChanged;

    public event EventHandler<bool>? DimOthersChanged;

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
            var list = new ObservableCollection<LayerListRow>(rows);
            LayersListView.ItemsSource = list;
            LayerStrip.ItemsSource = new ObservableCollection<LayerListRow>(list.OrderBy(row => row.Index));
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

            SyncPaintTargets();
        }
        finally
        {
            _suppressEvents = false;
        }
    }

    public void SetDimOthersSilently(bool value)
    {
        _suppressEvents = true;
        try
        {
            DimOthersCheck.IsChecked = value;
        }
        finally
        {
            _suppressEvents = false;
        }
    }

    private void SyncPaintTargets()
    {
        if (LayersListView.ItemsSource is not IEnumerable<LayerListRow> rows)
        {
            return;
        }

        var selected = GetSelectedLayerIndex();
        foreach (var row in rows)
        {
            row.IsPaintTarget = row.Index == selected;
        }
    }

    private void LayersListView_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        SyncPaintTargets();
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

    private void LayerLockButton_Click(object sender, RoutedEventArgs e)
    {
        if (_suppressEvents || sender is not FrameworkElement { DataContext: LayerListRow row })
        {
            return;
        }

        LayerLockChanged?.Invoke(this, (row.Index, !row.Locked));
    }

    private void LayerStrip_Click(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement { DataContext: LayerListRow row })
        {
            return;
        }

        LayersListView.SelectedItem = row;
        LayersListView.ScrollIntoView(row);
    }

    private void LayerOpacitySlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_suppressEvents || sender is not Slider slider || slider.DataContext is not LayerListRow row)
        {
            return;
        }

        if (!slider.IsMouseCaptureWithin && !slider.IsKeyboardFocusWithin)
        {
            return;
        }

        var percent = (int)Math.Round(slider.Value);
        if (percent == row.OpacityPercent)
        {
            return;
        }

        row.OpacityPercent = percent;
        LayerPreviewOpacityChanged?.Invoke(this, (row.Index, LayerPreviewOpacity.FromPercent(percent)));
    }

    private void DimOthersCheck_Click(object sender, RoutedEventArgs e)
    {
        if (_suppressEvents)
        {
            return;
        }

        DimOthersChanged?.Invoke(this, DimOthersCheck.IsChecked == true);
    }

    private void MenuAddLayer_Click(object sender, RoutedEventArgs e) => AddLayerRequested?.Invoke();

    private void MenuRemoveLayer_Click(object sender, RoutedEventArgs e) => RemoveLayerRequested?.Invoke();

    private void MenuRenameLayer_Click(object sender, RoutedEventArgs e) => RenameLayerRequested?.Invoke(this, EventArgs.Empty);

    private void MenuChangeEngineType_Click(object sender, RoutedEventArgs e) => ChangeEngineTypeRequested?.Invoke();

    private void MenuToggleLock_Click(object sender, RoutedEventArgs e) => ToggleLockRequested?.Invoke();

    internal string TitleForTest => PanelTitleText.Text;

    internal string HintForTest => PanelHintText.Text;

    internal string DimOthersCaptionForTest => DimOthersCheck.Content as string ?? string.Empty;

    internal string DimOthersToolTipForTest => DimOthersCheck.ToolTip as string ?? string.Empty;

    internal bool DimOthersForTest => DimOthersCheck.IsChecked == true;

    internal string OpacityHeaderForTest => OpacityHeaderText.Text;

    internal string OpacityHeaderToolTipForTest => OpacityHeaderText.ToolTip as string ?? string.Empty;

    internal int StripCountForTest => LayerStrip.Items.Count;

    internal IReadOnlyList<string> StripCaptionsForTest =>
        LayerStrip.Items.OfType<LayerListRow>().Select(static row => row.StripCaption).ToArray();

    internal void SelectStripForTest(int layerIndex)
    {
        if (LayersListView.ItemsSource is not IEnumerable<LayerListRow> rows)
        {
            return;
        }

        var row = rows.FirstOrDefault(candidate => candidate.Index == layerIndex);
        if (row is null)
        {
            return;
        }

        LayersListView.SelectedItem = row;
        LayersListView.ScrollIntoView(row);
    }

    internal void ToggleLockForTest(int layerIndex)
    {
        if (LayersListView.ItemsSource is not IEnumerable<LayerListRow> rows)
        {
            return;
        }

        var row = rows.FirstOrDefault(candidate => candidate.Index == layerIndex);
        if (row is null)
        {
            return;
        }

        LayerLockChanged?.Invoke(this, (row.Index, !row.Locked));
    }

    internal void SetOpacityPercentForTest(int layerIndex, int percent)
    {
        if (LayersListView.ItemsSource is not IEnumerable<LayerListRow> rows)
        {
            return;
        }

        var row = rows.FirstOrDefault(candidate => candidate.Index == layerIndex);
        if (row is null)
        {
            return;
        }

        row.OpacityPercent = percent;
        LayerPreviewOpacityChanged?.Invoke(this, (row.Index, LayerPreviewOpacity.FromPercent(percent)));
    }

    internal void ToggleVisibleForTest(int layerIndex)
    {
        if (LayersListView.ItemsSource is not IEnumerable<LayerListRow> rows)
        {
            return;
        }

        var row = rows.FirstOrDefault(candidate => candidate.Index == layerIndex);
        if (row is null)
        {
            return;
        }

        row.Visible = !row.Visible;
        LayerVisibilityChanged?.Invoke(this, (row.Index, row.Visible));
    }

    internal void ToggleDimOthersForTest()
    {
        DimOthersCheck.IsChecked = DimOthersCheck.IsChecked != true;
        DimOthersChanged?.Invoke(this, DimOthersCheck.IsChecked == true);
    }
}
