# Frog.Client — UI (`MainShellForm`) inventaire

← [Guides](README.md) · [SocialHub](UI-CLIENT-SocialHub.md) · [Référence Client](../../../reference/client/README.md)

Tip miroir : `d6e59759` (Social #27). Inventaire des **contrôles** pour les guides UI (chaque bouton/panneau). Enrichir avec captures + DA.

## Connexion

| Contrôle | Libellé UI | Rôle |
| --- | --- | --- |
| `_txtHost` | (host) | Adresse serveur |
| `_txtUser` / `_txtPass` | demo / ••• | Identifiants |
| `_btnConnect` | **Connecter** | TCP connect |
| `_btnDisconnect` | **Déconnecter** | Ferme TCP |
| `_btnLogin` | **Login** | `SendLoginAsync` |
| `_btnRegister` | **Inscription** | `SendRegisterAsync` |
| `_btnReconnect` | **Reconnecter (jeton)** | `SendReconnectAsync` |
| `_lblAuthStatus` | Jeton: … | État jeton |

## Personnages / map

| Contrôle | Libellé UI | Rôle |
| --- | --- | --- |
| `_btnCharRefresh` | **Liste persos** | Liste |
| `_btnCharCreate` | **Créer perso** | Création |
| `_txtNewCharName` | Nouveau perso | Nom |
| `_btnEnterGame` | **Entrer dans le jeu** | Select + entrée |
| `_btnMap` | **Demander map** | MapRequest |
| `_btnLogout` | **Logout** | Logout |
| `_btnSwitchCharacter` | **Changer de personnage** | Retour liste |
| `_btnBackDisconnect` | **Retour à la connexion…** | Session fermée |

## Onglets

| Tab | Titre | Contenu |
| --- | --- | --- |
| `_tabChat` | **Chat** | Chat + whisper |
| `_tabGameplay` | **Gameplay** | Combat, boutique, banque, ramasser, stats |
| `_tabPhase8` | **Quêtes** | Panneaux Phase 8 (dialogue, journal, craft, environnement) |
| `_tabSocial` | **Social** | `SocialHubPanel` — Amis / Groupe / Guilde (+ échafaudage Courrier/HdV/Coffre/Instance hors lot) |

## Chat / combat

| Contrôle | Libellé | Rôle |
| --- | --- | --- |
| `_btnSendChat` | **Envoyer chat** | Envoi message |
| `_txtWhisperTo` | Cible whisper | Whisper |
| `_btnMelee` | **Mêlée** | Attaque |
| `_btnSpell` | **Sort** | Sort |
| `_btnRespawn` | **Respawn** | Respawn (souvent masqué) |

## Économie

| Contrôle | Libellé | Rôle |
| --- | --- | --- |
| `_btnShopBuy` | **Acheter** | Boutique |
| `_btnShopSell` | **Vendre slot** | Vente |
| `_btnBankDepositItem` | **Banque dépôt** | Dépôt item |
| `_btnBankWithdrawItem` | **Banque retrait** | Retrait item |
| `_btnBankDepositGold` | **Dépôt or** | Or → banque |
| `_btnBankWithdrawGold` | **Retrait or** | Or ← banque |
| `_btnPickup` | **Ramasser** | Sol |
| `_btnStatsApply` | **Appliquer stats** | Stats |

## Dock chat — ouverture Social

| Contrôle | Libellé UI | Rôle |
| --- | --- | --- |
| bouton dock | **Amis** | Ouvre overlay Social → onglet Amis |
| bouton dock | **Groupe** | Ouvre overlay Social → onglet Groupe |
| bouton dock | **Guilde** | Ouvre overlay Social → onglet Guilde |

Canaux filtres dock : **Général** / **Local** / **Whisper** / **Groupe** / **Guilde** (≠ boutons Social).

## Panneaux (Controls/)

`SocialHubPanel` — guide dédié : [UI-CLIENT-SocialHub.md](UI-CLIENT-SocialHub.md).

Autres : `ChatPanel`, `CraftPanel`, `DialoguePanel`, `EnvironmentPanel`, `EquipmentPanel`, `InventoryPanel`, `QuestJournalPanel`, `MiniMap`, `StatusBar`, `TradeForm` (slash `/trade`) — fiches à enrichir.
