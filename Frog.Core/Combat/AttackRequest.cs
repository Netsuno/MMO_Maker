using Frog.Core.Enums;

namespace Frog.Core.Combat;

/// <summary>Demande d'attaque — corps du paquet 17 existant + extras optionnels (style en dernier).</summary>
public readonly record struct AttackRequest(
    string TargetName,
    CombatTargetKind Kind,
    Direction Facing,
    Guid TargetId,
    AttackStyle Style = AttackStyle.Melee);
