# Référence — Social & échanges (protocole v11)

Source gel : [`../progress/phase-10-beta-release/SOCIAL_PROTOCOL_FREEZE.md`](../progress/phase-10-beta-release/SOCIAL_PROTOCOL_FREEZE.md).

> **État runtime (cette branche) :** v11. P10-1 **DONE** tip `dca2185` (80–83). P10-2 **livré** (84–86). Pas READY bêta.

## SocialRequest (80)

| | |
| --- | --- |
| **Rôle** | Enveloppe multiplexée C→S (groupe / guilde / amis / blocage) |
| **Entrées** | `kind` u8 + `action` u8 + `request_id` Guid + payload |
| **Sorties** | Voir SocialResult / Snapshot / Event |
| **Erreurs** | Messages FR bornés (cible invalide, bloqué, capacité, rate-limit) |
| **État** | Livré P10-1 (`dca2185`) |

## SocialResult (81)

| | |
| --- | --- |
| **Rôle** | Réponse à une SocialRequest |
| **Entrées** | — (S→C) |
| **Sorties** | `kind` + `action` + `request_id` + succès bool + message UTF-8 borné + ids |
| **État** | Livré P10-1 |

## SocialSnapshot (82)

| | |
| --- | --- |
| **Rôle** | État liste (un domaine à la fois : groupe **ou** guilde **ou** amis **ou** blocage) |
| **Sorties** | Snapshot côté client |
| **État** | Livré P10-1 |

## SocialEvent (83)

| | |
| --- | --- |
| **Rôle** | Poussée (invitation, expiration, départ, transfert chef, présence) |
| **État** | Livré P10-1 |

## TradeRequest / TradeResult / TradeSnapshot (84–86)

| | |
| --- | --- |
| **Rôle** | Échange P2P objets + or (une TX PostgreSQL, `revision`, replay `trade.commit`) |
| **Entrées** | `action` (1 Invite … 7 Unconfirm) + `trade_id` + `request_id` + payload |
| **Sorties** | Résultat + snapshot offre (noms, or, piles, confirms, révision) |
| **Erreurs** | Trop loin, bloqué, or/objets insuffisants, inventaire plein, révision incorrecte |
| **État** | Livré P10-2 (boutique/banque ≠ trade joueur) |
