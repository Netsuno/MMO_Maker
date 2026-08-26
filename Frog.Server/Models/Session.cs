using System.Collections.Generic;
using Frog.Application.Gameplay;
using Frog.Server.Services;

namespace Frog.Server.Models;

public sealed class Session
{
    public required Guid Id { get; init; }
    public required string Username { get; init; }
    public Guid AccountId { get; set; }
    public Guid? AuthSessionId { get; set; }
    public DateTime ConnectedUtc { get; init; } = DateTime.UtcNow;
    public DateTime LastActivityUtc { get; set; } = DateTime.UtcNow;
    /// <summary>Tuile contenant le <b>centre</b> joueur (<c>floor(PixelX / tailleTuile)</c>) — collisions discrètes, warps, interactions.</summary>
    public int PositionX { get; set; }

    /// <summary>Même convention que <see cref="PositionX"/> sur l’axe vertical tuiles.</summary>
    public int PositionY { get; set; }

    /// <summary>Coordonnée X du centre joueur en pixels monde (constantes partagées Frog.Core WorldMetrics).</summary>
    public int PixelX { get; set; }

    /// <summary>Vérif absolue verticale : centre joueur en pixels monde.</summary>
    public int PixelY { get; set; }

    /// <summary>Carte monde courante (monde unique au debut ; instances plus tard).</summary>
    public int CurrentMapId { get; set; } = 1;

    /// <summary>Dernière acceptation <see cref="Frog.Server.Services.MovementService.TryApplyReportedPixelPosition"/> (anti-triche vitesse).</summary>
    public DateTime LastPositionSyncUtc { get; set; }

    /// <summary><c>frog_character.id</c> (UUID texte) du perso actif.</summary>
    public string? CharacterId { get; set; }

    /// <summary>UUID du personnage actif (Phase 7 gameplay).</summary>
    public Guid? CharacterGuid { get; set; }

    public int Level { get; set; } = 1;
    public long Experience { get; set; }
    public int Hp { get; set; }
    public int MaxHp { get; set; }
    public int Mp { get; set; }
    public int MaxMp { get; set; }
    public int Gold { get; set; }
    public bool IsDead { get; set; }
    public CharacterStats? Stats { get; set; }
    public Guid? ClassId { get; set; }
    public Guid? StartingSpellId { get; set; }
    public Guid? EquippedWeaponItemId { get; set; }
    public Guid? EquippedArmorItemId { get; set; }
    public long LastExperienceGain { get; set; }

    /// <summary>Fin de recharge par sort (UTC).</summary>
    public Dictionary<Guid, DateTime> SpellCooldownsUtc { get; } = new();

    public DateTime LastMeleeUtc { get; set; }

    public HashSet<Guid> KnownSpellIds { get; } = new();

    /// <summary>Cartes pour lesquelles un événement <c>page</c> a déjà été joué cette session (réarmé en quittant la carte).</summary>
    public HashSet<int> PageTriggerSatisfiedMapIds { get; } = new();

    /// <summary>Dernier <c>InteractResult</c> auto-tuile par <c>placementId</c> (réinitialisé au changement de case carte).</summary>
    public Dictionary<long, DateTime> MapEventAutoTileLastFiredUtc { get; } = new();

    /// <summary>Limite <see cref="Frog.Core.Enums.PacketId.MoveRequest"/> + <see cref="Frog.Core.Enums.PacketId.PositionSyncRequest"/> par seconde.</summary>
    public MovementPacketRateGate MovementPacketRateGate { get; } = new();
}
