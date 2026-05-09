namespace Frog.Core.Enums;

/// <summary>
/// Identifiants de paquets minimaux partagés Client/Serveur pour le Sprint 1.
/// </summary>
public enum PacketId : byte
{
    Hello = 1,
    LoginRequest = 2,
    LoginResult = 3,
    MapRequest = 4,
    MapData = 5,
    RegisterRequest = 6,
    RegisterResult = 7,
    MoveRequest = 8,
    PositionUpdate = 9,
    PlayerLeave = 10,
    HeartbeatRequest = 11,
    HeartbeatAck = 12,
    LogoutRequest = 13,
    LogoutAck = 14,
    ChatSend = 15,
    ChatMessage = 16,
    MeleeAttackRequest = 17,
    MeleeAttackResult = 18,
    /// <summary>La carte monde demandée correspond déjà à la révision + empreinte en cache du client (<see cref="MapRequest"/> avec hint).</summary>
    MapAlreadySynced = 19,
    /// <summary>Données perso JSON (<c>frog_character.payload</c>) après connexion réussie.</summary>
    CharacterPayload = 20,
    /// <summary>Demande la liste des personnages du compte (session authentifiée).</summary>
    CharacterListRequest = 21,
    /// <summary>Réponse JSON : tableau d’objets { id, name } (<see cref="Frog.Core.Protocol.CharacterListWireEntry"/>).</summary>
    CharacterListResult = 22,
    /// <summary>Choisir le personnage actif (UUID <c>frog_character.id</c>).</summary>
    CharacterSelectRequest = 23,
    /// <summary>Résultat sélection perso (même forme courte que <see cref="LoginResult"/>).</summary>
    CharacterSelectResult = 24,
    /// <summary>Créer un personnage additionnel (nom affichage).</summary>
    CharacterCreateRequest = 25,
    /// <summary>Résultat création perso (même forme que <see cref="LoginResult"/> ; message = UUID si succès).</summary>
    CharacterCreateResult = 26,
    /// <summary>Mise à jour des 6 stats (STR…LUCK) du perso actif ; corps = 6 octets 1–99.</summary>
    CharacterStatsUpdateRequest = 27,
    /// <summary>Résultat mise à jour stats (même forme que <see cref="LoginResult"/>).</summary>
    CharacterStatsUpdateResult = 28,
    /// <summary>Demande les événements carte du <c>CurrentMapId</c> session (corps vide ; MariaDB facultatif).</summary>
    MapEventsRequest = 29,
    /// <summary>Réponse JSON tableau <see cref="Frog.Core.Protocol.MapEventWireEntry"/> + <c>mapId</c> dans l’en-tête corps.</summary>
    MapEventsResult = 30,
    /// <summary>Interaction sur la tuile courante du joueur (corps vide).</summary>
    InteractRequest = 31,
    /// <summary>Résultat interaction (même forme que <see cref="LoginResult"/>).</summary>
    InteractResult = 32,
    /// <summary>Centre joueur en pixels monde (Int32 LE × 2). Session authentifiée ; serveur valide vitesse + collisions puis diffuse <see cref="PositionUpdate"/>.</summary>
    PositionSyncRequest = 33,
    Error = 255
}
