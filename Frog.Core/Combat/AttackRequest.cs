using Frog.Core.Enums;

namespace Frog.Core.Combat;

/// <summary>
/// Demande d'attaque — corps du paquet 17 existant + extras optionnels.
/// L'octet de style est absent pour la mêlée historique.
/// <see cref="ApplyStatus"/> suit cet octet (arme empoisonnée ou coup étourdissant) ; absent = aucun effet.
/// </summary>
public readonly record struct AttackRequest(
    string TargetName,
    CombatTargetKind Kind,
    Direction Facing,
    Guid TargetId,
    AttackStyle Style = AttackStyle.Melee,
    StatusEffectKind ApplyStatus = StatusEffectKind.None);
