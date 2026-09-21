using System.Windows;
using System.Windows.Controls;
using Frog.Application.Prefabs;
using Frog.Core.Enums;
using Frog.Core.Models;
using Frog.Editor.Enums;

namespace Frog.Editor.Panels;

public partial class EditorLeftToolsWpf : System.Windows.Controls.UserControl
{
    private bool _suspendTool;
    private bool _suspendTileType;
    private bool _suspendPrefab;

    public event Action<EditorTool>? ToolChanged;
    public event Action<TileType>? TileTypeChanged;
    public event Action<string, PrefabFacing>? PrefabSelectionChanged;
    public event Action? PipetteRequested;

    public EditorLeftToolsWpf()
    {
        InitializeComponent();
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
        if (ComboPrefab is null || ComboPrefabFacing is null)
        {
            return;
        }

        _suspendPrefab = true;
        try
        {
            ComboPrefab.Items.Clear();
            foreach (var prefab in catalog.Prefabs)
            {
                if (prefab is null || string.IsNullOrWhiteSpace(prefab.Id))
                {
                    continue;
                }

                ComboPrefab.Items.Add(new PrefabChoice(prefab.Id, string.IsNullOrWhiteSpace(prefab.DisplayName) ? prefab.Id : prefab.DisplayName));
            }

            SelectPrefabId(selectedId);
            ComboPrefabFacing.Items.Clear();
            ComboPrefabFacing.Items.Add(new FacingChoice(PrefabFacing.South, "Sud"));
            ComboPrefabFacing.Items.Add(new FacingChoice(PrefabFacing.West, "Ouest"));
            ComboPrefabFacing.Items.Add(new FacingChoice(PrefabFacing.East, "Est"));
            ComboPrefabFacing.Items.Add(new FacingChoice(PrefabFacing.North, "Nord"));
            SelectPrefabFacing(facing);
            RefreshPrefabStatus();
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
            SelectPrefabId(prefabId);
            SelectPrefabFacing(facing);
            RefreshPrefabStatus();
        }
        finally
        {
            _suspendPrefab = false;
        }
    }

    private void SelectPrefabId(string? selectedId)
    {
        for (var i = 0; i < ComboPrefab.Items.Count; i++)
        {
            if (ComboPrefab.Items[i] is PrefabChoice it && string.Equals(it.Id, selectedId, StringComparison.Ordinal))
            {
                ComboPrefab.SelectedIndex = i;
                return;
            }
        }

        if (ComboPrefab.Items.Count > 0)
        {
            ComboPrefab.SelectedIndex = 0;
        }
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

    private void RefreshPrefabStatus()
    {
        if (PrefabStatus is null)
        {
            return;
        }

        var name = ComboPrefab.SelectedItem is PrefabChoice prefab ? prefab.Label : "—";
        var facing = ComboPrefabFacing.SelectedItem is FacingChoice f ? f.Label : "—";
        PrefabStatus.Text = $"Prefab : {name} · {facing}";
    }

    private void OnPlacePrefabClick(object sender, RoutedEventArgs e)
    {
        SetSelectedTool(EditorTool.Prefab);
        ToolChanged?.Invoke(EditorTool.Prefab);
        RaisePrefabSelection();
    }

    private void ComboPrefab_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suspendPrefab)
        {
            return;
        }

        RaisePrefabSelection();
        SetSelectedTool(EditorTool.Prefab);
        ToolChanged?.Invoke(EditorTool.Prefab);
    }

    private void ComboPrefabFacing_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_suspendPrefab)
        {
            return;
        }

        RaisePrefabSelection();
        SetSelectedTool(EditorTool.Prefab);
        ToolChanged?.Invoke(EditorTool.Prefab);
    }

    private void RaisePrefabSelection()
    {
        RefreshPrefabStatus();
        if (ComboPrefab.SelectedItem is PrefabChoice prefab && ComboPrefabFacing.SelectedItem is FacingChoice facing)
        {
            PrefabSelectionChanged?.Invoke(prefab.Id, facing.Facing);
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

    private sealed record ToolItem(EditorTool Tool, string Label)
    {
        public override string ToString() => Label;
    }

    private sealed record TileTypeItem(TileType Type, string Label)
    {
        public override string ToString() => Label;
    }

    private sealed record PrefabChoice(string Id, string Label)
    {
        public override string ToString() => Label;
    }

    private sealed record FacingChoice(PrefabFacing Facing, string Label)
    {
        public override string ToString() => Label;
    }
}
