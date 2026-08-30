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
    /// <summary>Données perso en JSON UTF-8 (agrégat assemblé depuis MariaDB : stats / flags / KV).</summary>
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

    /// <summary>Fusion JSON des drapeaux monde côté wire ; persistance MariaDB : table <c>character_world_flag</c> (UInt16 LE + UTF-8 objet, booléens uniquement).</summary>
    WorldFlagsPatchRequest = 34,

    /// <summary>Résultat patch drapeaux (même forme courte que <see cref="LoginResult"/>).</summary>
    WorldFlagsPatchResult = 35,

    /// <summary>Reconnexion via jeton opaque émis au login (UTF-8, longueur UInt16 LE).</summary>
    ReconnectRequest = 36,

    /// <summary>Résultat reconnexion (même forme que <see cref="LoginResult"/>).</summary>
    ReconnectResult = 37,

    /// <summary>État inventaire + équipement (binaire).</summary>
    InventorySnapshot = 38,
    EquipRequest = 39,
    EquipResult = 40,
    UnequipRequest = 41,
    UnequipResult = 42,
    DropItemRequest = 43,
    DropItemResult = 44,
    PickupItemRequest = 45,
    PickupItemResult = 46,
    GroundItemsSnapshot = 47,
    SpellCastRequest = 48,
    SpellCastResult = 49,
    /// <summary>HP/MP/niveau/XP/or/mort du perso actif.</summary>
    CombatState = 50,
    ShopBuyRequest = 51,
    ShopBuyResult = 52,
    ShopSellRequest = 53,
    ShopSellResult = 54,
    BankDepositRequest = 55,
    BankDepositResult = 56,
    BankWithdrawRequest = 57,
    BankWithdrawResult = 58,
    BankSnapshot = 59,
    RespawnRequest = 60,
    RespawnResult = 61,
    ExperienceGain = 62,
    DeathNotify = 63,
    /// <summary>Demande le catalogue contenu publié (classes/items/spells/shops/NPCs).</summary>
    PublishedCatalogRequest = 64,
    /// <summary>Réponse JSON <see cref="Frog.Core.Protocol.PublishedCatalogWire"/>.</summary>
    PublishedCatalogResult = 65,

    /// <summary>État dialogue typé (speaker, texte, choix + token session).</summary>
    DialogueStatePush = 66,

    /// <summary>Choix dialogue avec token session opaque.</summary>
    DialogueChoiceRequest = 67,

    /// <summary>Résultat choix dialogue.</summary>
    DialogueChoiceResult = 68,

    /// <summary>Journal quêtes (JSON <see cref="Frog.Core.Protocol.QuestJournalEntryWire"/>).</summary>
    QuestJournalSnapshot = 69,

    /// <summary>Turn-in quête avec requestId idempotent.</summary>
    QuestTurnInRequest = 70,

    /// <summary>Résultat turn-in quête.</summary>
    QuestTurnInResult = 71,

    /// <summary>Craft recette avec requestId idempotent.</summary>
    CraftRequest = 72,

    /// <summary>Résultat craft.</summary>
    CraftResult = 73,

    /// <summary>Région/météo/éclairage carte courante.</summary>
    EnvironmentStatePush = 74,

    /// <summary>Acquisition d'un métier publié.</summary>
    AcquireProfessionRequest = 75,

    /// <summary>Résultat acquisition métier.</summary>
    AcquireProfessionResult = 76,

    Error = 255
}
