using Frog.Core.Enums;
using Frog.Core.Models;

namespace Frog.Application.Maps;

/// <summary>Cartes modernes de démonstration pour le shell éditeur (hors compatibilité FRoG).</summary>
public static class DemoMapFactory
{
    /// <summary>Identité stable de la carte démo en mémoire / seed PostgreSQL.</summary>
    public static readonly Guid DefaultMapId = Guid.Parse("11111111-1111-1111-1111-111111111101");

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
