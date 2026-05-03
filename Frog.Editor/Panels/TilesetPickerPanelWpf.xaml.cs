using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using Frog.Editor.Assets;

namespace Frog.Editor.Panels;

public partial class TilesetPickerPanelWpf : System.Windows.Controls.UserControl
{
    private bool _suspendTabSync;
    private bool _suspendListSync;

    public event Action<int>? SelectedTilesetChanged;
    public event Action? LoadTilesetsRequested;
    public event Action<System.Drawing.Rectangle>? StampSelectionChanged;

    public TilesetPickerPanelWpf()
    {
        InitializeComponent();
        Palette.StampSelectionChanged += r => StampSelectionChanged?.Invoke(r);
    }

    public void SetPaletteTileset(int id) => Palette.SetTileset(id);

    public void ApplyEntries(IReadOnlyList<(int Id, string Label)> entries, int? preferredSelectId)
    {
        ListTilesets.Items.Clear();
        foreach (var (id, label) in entries)
        {
            ListTilesets.Items.Add(new TilesetRow(id, label));
        }

        if (entries.Count == 0)
        {
            SyncTabFromListIndex(-1);
            return;
        }

        var pick = preferredSelectId;
        var ix = -1;
        if (pick is { } wantId)
        {
            for (var i = 0; i < ListTilesets.Items.Count; i++)
            {
                if (ListTilesets.Items[i] is TilesetRow row && row.Id == wantId)
                {
                    ix = i;
                    break;
                }
            }
        }

        if (ix < 0)
        {
            ix = ListTilesets.Items.Count - 1;
        }

        _suspendListSync = true;
        try
        {
            ListTilesets.SelectedIndex = ix;
        }
        finally
        {
            _suspendListSync = false;
        }

        SyncTabFromListIndex(ix);
    }

    public int? TryGetSelectedTilesetId() =>
        ListTilesets.SelectedItem is TilesetRow row ? row.Id : null;

    private void TabSlots_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suspendTabSync || TabSlots.SelectedIndex < 0)
        {
            return;
        }

        var ix = TabSlots.SelectedIndex;
        if (ix >= ListTilesets.Items.Count)
        {
            return;
        }

        _suspendListSync = true;
        try
        {
            ListTilesets.SelectedIndex = ix;
        }
        finally
        {
            _suspendListSync = false;
        }
    }

    private void ListTilesets_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suspendListSync || ListTilesets.SelectedItem is not TilesetRow row)
        {
            return;
        }

        Palette.SetTileset(row.Id);
        SelectedTilesetChanged?.Invoke(row.Id);
        SyncTabFromListIndex(ListTilesets.SelectedIndex);
    }

    private void SyncTabFromListIndex(int listIndex)
    {
        _suspendTabSync = true;
        try
        {
            if (listIndex < 0 || ListTilesets.Items.Count == 0)
            {
                TabSlots.SelectedIndex = 0;
                return;
            }

            var ix = Math.Max(0, listIndex);
            TabSlots.SelectedIndex = Math.Min(ix, TabSlots.Items.Count - 1);
        }
        finally
        {
            _suspendTabSync = false;
        }
    }

    private void BtnLoadTileset_OnClick(object sender, RoutedEventArgs e) => LoadTilesetsRequested?.Invoke();

    private sealed class TilesetRow
    {
        public TilesetRow(int id, string label)
        {
            Id = id;
            Label = label;
        }

        public int Id { get; }
        public string Label { get; }

        public override string ToString() => $"{Id}: {Label}";
    }
}
