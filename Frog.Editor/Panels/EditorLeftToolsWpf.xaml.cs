using System.Windows;
using System.Windows.Controls;
using Frog.Core.Enums;
using Frog.Editor.Enums;

namespace Frog.Editor.Panels;

public partial class EditorLeftToolsWpf : System.Windows.Controls.UserControl
{
    private bool _suspendTool;
    private bool _suspendTileType;

    public event Action<EditorTool>? ToolChanged;
    public event Action<TileType>? TileTypeChanged;

    public EditorLeftToolsWpf()
    {
        InitializeComponent();
        foreach (EditorTool t in Enum.GetValues<EditorTool>())
        {
            ComboTool.Items.Add(new ToolItem(t, ToolLabel(t)));
        }

        ComboTool.SelectedIndex = 0;
        SetSpawnDisplay(null, null);

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
}
