namespace Frog.Core.Enums;

/// <summary>Type de cible d’un sort ou d’une compétence.</summary>
public enum TargetType : byte
{
    Self = 1,
    SingleEnemy = 2,
    SingleAlly = 3,
    AoE = 4,
}
