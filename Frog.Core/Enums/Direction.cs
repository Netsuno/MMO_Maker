// #TODO (FR) : Confirmer le mapping réseau (valeurs stables pour sérialisation sur 1 octet).
namespace Frog.Core.Enums;

/// <summary>Directions cardinales utilisées pour l’affichage et le protocole.</summary>
public enum Direction : byte
{
    Down = 0,
    Left = 1,
    Right = 2,
    Up = 3
}
