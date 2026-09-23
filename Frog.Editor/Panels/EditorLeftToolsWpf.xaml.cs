using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Frog.Application.Prefabs;
using Frog.Core.Enums;
using Frog.Core.Models;
using Frog.Editor.Assets;
using Frog.Editor.Enums;

namespace Frog.Editor.Panels;

public partial class EditorLeftToolsWpf : System.Windows.Controls.UserControl
{
    private static readonly SolidColorBrush PlaceIdleBackground = Freeze(0x32, 0x35, 0x3E);
    private static readonly SolidColorBrush PlaceIdleBorder = Freeze(0x58, 0x5C, 0x68);
    private static readonly SolidColorBrush PlaceActiveBackground = Freeze(0x5A, 0x43, 0x22);
    private static readonly SolidColorBrush PlaceActiveBorder = Freeze(0xE8, 0xB8, 0x6D);

    private bool _suspendTool;
    private bool _suspendTileType;
    private bool _suspendPrefab;
    private bool _placeMode;
    private string? _selectedId;
    private PrefabFacing _selectedFacing = PrefabFacing.South;
    private string? _actionMessage;
    private int _visiblePrefabCount;
    private PrefabCatalog _catalog = new();

    public event Action<EditorTool>? ToolChanged;
    public event Action<TileType>? TileTypeChanged;
    public event Action<string, PrefabFacing>? PrefabSelectionChanged;
    public event Action? PipetteRequested;
    public event Action? PrefabDuplicateRequested;
    public event Action? PrefabEscapeRequested;

    public EditorLeftToolsWpf()
    {
        InitializeComponent();
        PreviewKeyDown += OnPanelPreviewKeyDown;
        foreach (EditorTool t in Enum.GetValues<EditorTool>())
        {
            ComboTool.Items.Add(new ToolItem(t, ToolLabel(t)));
        }

        ComboTool.SelectedIndex = 0;
        SetSpawnDisplay(null, null);
        BindPrefabCatalog(BuiltInPrefabCatalog.Create(), BuiltInPrefabCatalog.SofaId, PrefabFacing.South);

        foreach (var (type, label) in TileChoices)
        {
            ComboTileType.Items.Add(new TileTypeItem(type, label));
        }

        ComboTileType.SelectedIndex = 0;
    }

    private static readonly (TileType Type, string Label)[] TileChoices =
    {
        (TileType.Ground, "Terrain"),
        (TileType.Block, "Blocage"),
        (TileType.Warp, "Warp"),
        (TileType.Resource, "Ressource"),
        (TileType.Script, "Script"),
    };

    private static string ToolLabel(EditorTool t) => EditorToolHotkeys.DisplayWithShortcut(t);

    public bool IsPrefabSearchFocused =>
        PrefabFilter?.IsKeyboardFocused == true || PrefabFilter?.IsKeyboardFocusWithin == true;

    public string SelectedPrefabSummary
    {
        get
        {
            var name = CurrentPrefabLabel();
            var facing = FacingLabel(_selectedFacing);
            return $"{name} · {facing}";
        }
    }

    public void SetSpawnDisplay(int? tileX, int? tileY)
    {
        if (SpawnStatus is null)
        {
            return;
        }

        SpawnStatus.Text = tileX is int x && tileY is int y
            ? $"Départ playtest : ({x}, {y})"
            : "Départ playtest : non défini";
    }

    public void SetSelectedTool(EditorTool tool)
    {
        for (var i = 0; i < ComboTool.Items.Count; i++)
        {
            if (ComboTool.Items[i] is ToolItem it && it.Tool == tool)
            {
                _suspendTool = true;
                try
                {
                    ComboTool.SelectedIndex = i;
                }
                finally
                {
                    _suspendTool = false;
                }

                return;
            }
        }
    }

    public void SetSelectedTileType(TileType type)
    {
        for (var i = 0; i < ComboTileType.Items.Count; i++)
        {
            if (ComboTileType.Items[i] is TileTypeItem it && it.Type == type)
            {
                _suspendTileType = true;
                try
                {
                    ComboTileType.SelectedIndex = i;
                }
                finally
                {
                    _suspendTileType = false;
                }

                return;
            }
        }
    }

    private void OnPlaceSpawnClick(object sender, RoutedEventArgs e)
    {
        SetSelectedTool(EditorTool.Spawn);
        ToolChanged?.Invoke(EditorTool.Spawn);
    }

    private void OnPipetteClick(object sender, RoutedEventArgs e) => PipetteRequested?.Invoke();

    public void BindPrefabCatalog(PrefabCatalog catalog, string? selectedId, PrefabFacing facing)
    {
        if (ListPrefabs is null || ComboPrefabFacing is null)
        {
            return;
        }

        _catalog = catalog ?? new PrefabCatalog();
        _selectedFacing = facing;
        _selectedId = ResolveSelectedId(selectedId);

        _suspendPrefab = true;
        try
        {
            ComboPrefabFacing.Items.Clear();
            ComboPrefabFacing.Items.Add(new FacingChoice(PrefabFacing.South, "Sud"));
            ComboPrefabFacing.Items.Add(new FacingChoice(PrefabFacing.West, "Ouest"));
            ComboPrefabFacing.Items.Add(new FacingChoice(PrefabFacing.East, "Est"));
            ComboPrefabFacing.Items.Add(new FacingChoice(PrefabFacing.North, "Nord"));
            SelectPrefabFacing(facing);
            ApplyPrefabFilter();
        }
        finally
        {
            _suspendPrefab = false;
        }
    }

    public void SetPrefabSelection(string prefabId, PrefabFacing facing)
    {
        _suspendPrefab = true;
        try
        {
            _selectedId = ResolveSelectedId(prefabId);
            _selectedFacing = facing;
            SelectPrefabFacing(facing);
            ApplyPrefabFilter();
        }
        finally
        {
            _suspendPrefab = false;
        }
    }

    public void SetPrefabPlaceMode(bool active)
    {
        _placeMode = active;
        _actionMessage = null;
        if (BtnPlacePrefab is not null)
        {
            BtnPlacePrefab.Background = active ? PlaceActiveBackground : PlaceIdleBackground;
            BtnPlacePrefab.BorderBrush = active ? PlaceActiveBorder : PlaceIdleBorder;
            BtnPlacePrefab.Content = active ? "Placement en cours (P)" : "Placer un objet (P)";
        }

        RefreshPrefabStatus();
    }

    public void SetCanDuplicateLastPrefab(bool can)
    {
        if (BtnDuplicatePrefab is null)
        {
            return;
        }

        BtnDuplicatePrefab.IsEnabled = can;
        BtnDuplicatePrefab.ToolTip = can
            ? "Pose une copie du dernier objet, décalée de son empreinte."
            : "Placez d’abord un objet sur la carte.";
    }

    public void SetPrefabActionMessage(string? message)
    {
        _actionMessage = string.IsNullOrWhiteSpace(message) ? null : message.Trim();
        RefreshPrefabStatus();
    }

    /// <summary>Échap dans le champ de recherche : vide le filtre, sans quitter le mode placement.</summary>
    public bool TryConsumePrefabFilterEscape()
    {
        if (!IsPrefabSearchFocused || PrefabFilter is null || string.IsNullOrEmpty(PrefabFilter.Text))
        {
            return false;
        }

        PrefabFilter.Text = string.Empty;
        return true;
    }

    private void OnPanelPreviewKeyDown(object sender, System.Windows.Input.KeyEventArgs e)
    {
        if (e.Key != Key.Escape || Keyboard.Modifiers != ModifierKeys.None)
        {
            return;
        }

        if (!IsPrefabSearchFocused && !_placeMode)
        {
            return;
        }

        PrefabEscapeRequested?.Invoke();
        e.Handled = true;
    }

    private string? ResolveSelectedId(string? selectedId)
    {
        if (ContainsId(selectedId))
        {
            return selectedId;
        }

        foreach (var prefab in _catalog.Prefabs)
        {
            if (prefab is not null && !string.IsNullOrWhiteSpace(prefab.Id))
            {
                return prefab.Id;
            }
        }

        return null;
    }

    private bool ContainsId(string? id)
    {
        if (string.IsNullOrWhiteSpace(id))
        {
            return false;
        }

        foreach (var prefab in _catalog.Prefabs)
        {
            if (prefab is not null && string.Equals(prefab.Id, id, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    private void ApplyPrefabFilter()
    {
        if (ListPrefabs is null)
        {
            return;
        }

        var filter = PrefabFilter?.Text ?? string.Empty;
        if (PrefabFilterHint is not null)
        {
            PrefabFilterHint.Visibility = string.IsNullOrEmpty(filter) ? Visibility.Visible : Visibility.Collapsed;
        }

        var entries = new List<PrefabListEntry>();
        var total = 0;
        foreach (var prefab in _catalog.Prefabs)
        {
            if (prefab is null || string.IsNullOrWhiteSpace(prefab.Id))
            {
                continue;
            }

            total++;
            var label = string.IsNullOrWhiteSpace(prefab.DisplayName) ? prefab.Id : prefab.DisplayName.Trim();
            if (!PrefabListFilter.Matches(filter, prefab.Id, label))
            {
                continue;
            }

            entries.Add(new PrefabListEntry(prefab.Id, label, ThumbnailFor(prefab)));
        }

        ListPrefabs.ItemsSource = entries;
        PrefabListEntry? match = null;
        foreach (var entry in entries)
        {
            if (string.Equals(entry.Id, _selectedId, StringComparison.Ordinal))
            {
                match = entry;
                break;
            }
        }

        ListPrefabs.SelectedItem = match;
        if (match is not null)
        {
            ListPrefabs.ScrollIntoView(match);
        }

        if (PrefabMatchCount is not null)
        {
            PrefabMatchCount.Text = string.IsNullOrWhiteSpace(filter)
                ? $"{total} objets"
                : $"{entries.Count} / {total}";
        }

        _visiblePrefabCount = entries.Count;
        if (PrefabFilterEmpty is not null)
        {
            PrefabFilterEmpty.Visibility = entries.Count == 0 && !string.IsNullOrWhiteSpace(filter)
                ? Visibility.Visible
                : Visibility.Collapsed;
        }

        RefreshPreview();
        RefreshPrefabStatus();
    }

    private void SelectPrefabFacing(PrefabFacing facing)
    {
        for (var i = 0; i < ComboPrefabFacing.Items.Count; i++)
        {
            if (ComboPrefabFacing.Items[i] is FacingChoice it && it.Facing == facing)
            {
                ComboPrefabFacing.SelectedIndex = i;
                return;
            }
        }

        if (ComboPrefabFacing.Items.Count > 0)
        {
            ComboPrefabFacing.SelectedIndex = 0;
        }
    }

    private void RefreshPreview()
    {
        if (PrefabPreview is null || PrefabPreviewHost is null)
        {
            return;
        }

        ImageSource? preview = null;
        if (TryCurrentDefinition(out var definition)
            && PrefabPlacementService.TryResolveVariant(definition, _selectedFacing, out var variant))
        {
            preview = PrefabSpriteCache.TryCreatePreview(variant.SpriteFileName);
        }

        PrefabPreview.Source = preview;
        PrefabPreviewHost.Visibility = preview is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private void RefreshPrefabStatus()
    {
        if (PrefabStatus is null)
        {
            return;
        }

        var name = CurrentPrefabLabel();
        var facing = ComboPrefabFacing?.SelectedItem is FacingChoice choice
            ? choice.Label
            : FacingLabel(_selectedFacing);
        if (PrefabSelectedName is not null)
        {
            PrefabSelectedName.Text = name;
        }

        if (PrefabSelectedMeta is not null)
        {
            var id = string.IsNullOrWhiteSpace(_selectedId) ? "—" : _selectedId;
            PrefabSelectedMeta.Text = $"{id} · {facing}";
        }

        if (!string.IsNullOrWhiteSpace(_actionMessage))
        {
            PrefabStatus.Text = _placeMode
                ? $"{_actionMessage}  ·  Clic pour placer — Échap pour annuler"
                : _actionMessage;
            return;
        }

        PrefabStatus.Text = _placeMode
            ? "Clic pour placer — Échap pour annuler"
            : "P pour placer";
    }

    private string CurrentPrefabLabel()
    {
        if (TryCurrentDefinition(out var definition))
        {
            return string.IsNullOrWhiteSpace(definition.DisplayName) ? definition.Id : definition.DisplayName.Trim();
        }

        return "—";
    }

    private bool TryCurrentDefinition(out PrefabDefinition definition)
    {
        definition = null!;
        if (string.IsNullOrWhiteSpace(_selectedId))
        {
            return false;
        }

        return PrefabPlacementService.TryGetDefinition(_catalog, _selectedId, out definition);
    }

    private static ImageSource? ThumbnailFor(PrefabDefinition prefab)
    {
        if (!PrefabPlacementService.TryResolveVariant(prefab, PrefabFacing.South, out var variant))
        {
            return null;
        }

        return PrefabSpriteCache.TryCreatePreview(variant.SpriteFileName);
    }

    private static string FacingLabel(PrefabFacing facing) =>
        facing switch
        {
            PrefabFacing.West => "Ouest",
            PrefabFacing.East => "Est",
            PrefabFacing.North => "Nord",
            _ => "Sud",
        };

    private void OnPlacePrefabClick(object sender, RoutedEventArgs e)
    {
        SetSelectedTool(EditorTool.Prefab);
        ToolChanged?.Invoke(EditorTool.Prefab);
        RaisePrefabSelection();
    }

    private void OnDuplicatePrefabClick(object sender, RoutedEventArgs e) =>
        PrefabDuplicateRequested?.Invoke();

    private void PrefabFilter_OnTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_suspendPrefab)
        {
            return;
        }

        _suspendPrefab = true;
        try
        {
            ApplyPrefabFilter();
        }
        finally
        {
            _suspendPrefab = false;
        }
    }

    private void ListPrefabs_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suspendPrefab || ListPrefabs.SelectedItem is not PrefabListEntry entry)
        {
            return;
        }

        CommitPrefabChoice(entry.Id, enterPlaceMode: true);
    }

    private void ListPrefabs_OnPreviewMouseLeftButtonUp(object sender, MouseButtonEventArgs e)
    {
        if (ItemsControl.ContainerFromElement(ListPrefabs, e.OriginalSource as DependencyObject) is not ListBoxItem)
        {
            return;
        }

        if (ListPrefabs.SelectedItem is not PrefabListEntry entry)
        {
            return;
        }

        CommitPrefabChoice(entry.Id, enterPlaceMode: true);
    }

    private void ComboPrefabFacing_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suspendPrefab)
        {
            return;
        }

        if (ComboPrefabFacing.SelectedItem is FacingChoice facing)
        {
            _selectedFacing = facing.Facing;
        }

        _actionMessage = null;
        RefreshPreview();
        RaisePrefabSelection();
        SetSelectedTool(EditorTool.Prefab);
        ToolChanged?.Invoke(EditorTool.Prefab);
    }

    private void CommitPrefabChoice(string prefabId, bool enterPlaceMode)
    {
        _selectedId = prefabId;
        _actionMessage = null;
        if (ComboPrefabFacing.SelectedItem is FacingChoice facing)
        {
            _selectedFacing = facing.Facing;
        }

        RefreshPreview();
        RaisePrefabSelection();
        if (!enterPlaceMode)
        {
            return;
        }

        SetSelectedTool(EditorTool.Prefab);
        ToolChanged?.Invoke(EditorTool.Prefab);
    }

    private void RaisePrefabSelection()
    {
        RefreshPrefabStatus();
        if (!string.IsNullOrWhiteSpace(_selectedId))
        {
            PrefabSelectionChanged?.Invoke(_selectedId, _selectedFacing);
        }
    }

    private void ComboTool_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suspendTool || ComboTool.SelectedItem is not ToolItem it)
        {
            return;
        }

        ToolChanged?.Invoke(it.Tool);
    }

    private void ComboTileType_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suspendTileType || ComboTileType.SelectedItem is not TileTypeItem it)
        {
            return;
        }

        TileTypeChanged?.Invoke(it.Type);
    }

    private static SolidColorBrush Freeze(byte r, byte g, byte b)
    {
        var brush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(r, g, b));
        brush.Freeze();
        return brush;
    }

    internal string StatusTextForTest => PrefabStatus.Text;
    internal string SelectedNameForTest => PrefabSelectedName.Text;
    internal string SelectedMetaForTest => PrefabSelectedMeta.Text;
    internal int VisiblePrefabCountForTest => _visiblePrefabCount;
    internal bool HasPrefabPreviewForTest =>
        PrefabPreviewHost.Visibility == Visibility.Visible && PrefabPreview.Source is not null;
    internal bool PlaceModeForTest => _placeMode;
    internal bool CanDuplicateForTest => BtnDuplicatePrefab.IsEnabled;

    internal void SetFilterForTest(string text) => PrefabFilter.Text = text;

    private sealed record ToolItem(EditorTool Tool, string Label)
    {
        public override string ToString() => Label;
    }

    private sealed record TileTypeItem(TileType Type, string Label)
    {
        public override string ToString() => Label;
    }

    private sealed class PrefabListEntry
    {
        public PrefabListEntry(string id, string label, ImageSource? thumbnail)
        {
            Id = id;
            Label = label;
            Thumbnail = thumbnail;
        }

        public string Id { get; }
        public string Label { get; }
        public ImageSource? Thumbnail { get; }
        public Visibility ThumbnailVisibility => Thumbnail is null ? Visibility.Collapsed : Visibility.Visible;
    }

    private sealed record FacingChoice(PrefabFacing Facing, string Label)
    {
        public override string ToString() => Label;
    }
}
