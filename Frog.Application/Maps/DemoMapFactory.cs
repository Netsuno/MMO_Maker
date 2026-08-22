using Frog.Core.Enums;
using Frog.Core.Models;

namespace Frog.Application.Maps;

/// <summary>Cartes modernes de démonstration pour le shell éditeur (hors compatibilité FRoG).</summary>
public static class DemoMapFactory
{
    public const int DefaultLegacyId = 1;
    public const string DefaultName = "Carte démo";

    public static Map CreateStarter(string? name = null, int width = 20, int height = 15)
    {
        if (width <= 0 || height <= 0)
        {
            throw new ArgumentOutOfRangeException(nameof(width), "Dimensions démo doivent être > 0.");
        }

        var map = new Map
        {
            Name = string.IsNullOrWhiteSpace(name) ? DefaultName : name.Trim(),
            Width = width,
            Height = height,
        };
        map.Layers.Add(new Layer { LayerType = LayerType.Ground, DisplayName = "Sol", Visible = true });
        map.Layers.Add(new Layer { LayerType = LayerType.Fringe, DisplayName = "Frange", Visible = true });
        map.Layers.Add(new Layer { LayerType = LayerType.Attributes, DisplayName = "Attributs", Visible = true });
        return map;
    }
}
