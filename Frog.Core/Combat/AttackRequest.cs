using Frog.Core.Enums;

namespace Frog.Core.Combat;

/// <summary>Demande d'attaque mêlée — corps du paquet 17 existant + extras optionnels.</summary>
public readonly record struct AttackRequest(
    string TargetName,
    CombatTargetKind Kind,
    Direction Facing,
    Guid TargetId);
