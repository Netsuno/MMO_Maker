# Référence — Social & échanges (protocole v11 **prévu**)

Source gel : [`../progress/phase-10-beta-release/SOCIAL_PROTOCOL_FREEZE.md`](../progress/phase-10-beta-release/SOCIAL_PROTOCOL_FREEZE.md).

> **État : absent du runtime.** Premier commit produit qui ajoute Party/Guild ou 80–86 doit passer `FrogWireProtocol.Version` à **11**.

## SocialRequest (80) — prévu

| | |
| --- | --- |
| **Rôle** | Enveloppe multiplexée C→S (groupe / guilde / amis / blocage) |
| **Entrées** | `kind` u8 + `action` u8 + `request_id` Guid + payload |
| **Sorties** | Voir SocialResult / Snapshot / Event |
| **Erreurs** | À définir à l’implémentation (gel § règles métier) |
| **État** | Prévu P10-1 — **absent** |

## SocialResult (81) — prévu

| | |
| --- | --- |
| **Rôle** | Réponse à une SocialRequest |
| **Entrées** | — (S→C) |
| **Sorties** | `kind` + `action` + `request_id` + succès bool + message UTF-8 borné + ids |
| **État** | Prévu P10-1 — **absent** |

## SocialSnapshot (82) — prévu

| | |
| --- | --- |
| **Rôle** | État liste (un domaine à la fois : groupe **ou** guilde **ou** amis **ou** blocage) |
| **Sorties** | Snapshot côté client |
| **État** | Prévu P10-1 — **absent** |

## SocialEvent (83) — prévu

| | |
| --- | --- |
| **Rôle** | Poussée (invitation, expiration, départ, transfert chef, présence) |
| **État** | Prévu P10-1 — **absent** |

## TradeRequest / TradeResult / TradeSnapshot (84–86) — prévus

| | |
| --- | --- |
| **Rôle** | Échange P2P objets + or (transaction unique, revision) |
| **Entrées** | `action` + `trade_id` + `request_id` + payload |
| **Sorties** | Résultat + snapshot offre + drapeaux confirm |
| **État** | Prévu P10-2 — **absent** (boutique/banque ≠ trade joueur) |
