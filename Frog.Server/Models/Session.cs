using System.Collections.Generic;

namespace Frog.Server.Models;

public sealed class Session
{
    public required Guid Id { get; init; }
    public required string Username { get; init; }
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

    /// <summary><c>frog_character.id</c> (UUID texte) du perso par défaut, rempli après login.</summary>
    public string? CharacterId { get; set; }

    /// <summary>Cartes pour lesquelles un événement <c>page</c> a déjà été joué cette session (réarmé en quittant la carte).</summary>
    public HashSet<int> PageTriggerSatisfiedMapIds { get; } = new();
}
