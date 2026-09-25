namespace Frog.Core.Constants;

/// <summary>Bornes du scaffolding combat MVP (paquets 17/18, protocole 11).</summary>
public static class CombatMvpLimits
{
    public const string DummyName = "Mannequin";

    public static readonly Guid DummyId = new("8f0c1a2b-3d4e-4f50-a6b7-c8d9e0f10203");

    public const int DummyMaxHp = 40;

    public const int DummyLevel = 1;

    public const int DummyVit = 0;

    /// <summary>kind (1) + facing (1) + targetId (16) après le nom UTF-8 de <c>MeleeAttackRequest</c>.</summary>
    public const int AttackExtrasBytes = 1 + 1 + 16;

    /// <summary>Trailer additif après le message de <c>MeleeAttackResult</c>.</summary>
    public const int DamageEventTrailerBytes = 16 + 16 + 1 + 4 + 4 + 4 + 1;

    public const int FloatingNumberLifetimeMs = 800;

    public const byte DamageFlagHit = 1;

    public const byte DamageFlagKilled = 2;

    /// <summary>Bit libre déjà présent dans l'octet de flags du trailer 18. Pas un nouvel opcode.</summary>
    public const byte DamageFlagCrit = 4;

    /// <summary>Même octet de flags : coup à distance. Les clients qui ignorent le bit restent valides.</summary>
    public const byte DamageFlagRanged = 8;
}
