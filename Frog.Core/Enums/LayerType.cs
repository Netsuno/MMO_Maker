// #TODO (FR) : Refléter l’éditeur VB6 (Ground, Mask, Mask2, Fringe, Fringe2, Attributes).
namespace Frog.Core.Enums;

/// <summary>Types de couches attendus par l’éditeur et le rendu client.</summary>
public enum LayerType : byte
{
    Ground = 0,
    Mask = 1,
    Mask2 = 2,
    Fringe = 3,
    Fringe2 = 4,
    Attributes = 5
}
