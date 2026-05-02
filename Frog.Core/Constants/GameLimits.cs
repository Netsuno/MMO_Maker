// #TODO (FR) : Confirmer les limites avec VB6 (tailles de cartes, couches, max objets/PNJ).
namespace Frog.Core.Constants;

/// <summary>Limites globales prévues par le moteur. À ajuster après import VB6.</summary>
public static class GameLimits
{
    public const int MaxLayers = 6;        // Ground, Mask, Mask2, Fringe, Fringe2, Attributes
    public const int MaxMapWidth = 256;    // À confirmer selon VB6
    public const int MaxMapHeight = 256;   // À confirmer
    public const int MaxItems = 2048;      // À confirmer
    public const int MaxNpcs = 1024;       // À confirmer
}
