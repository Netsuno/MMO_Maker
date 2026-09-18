# Référence — Actions joueur

Fiches pour les opérations gameplay déjà sur le fil (Phases 7–8) ou à venir.

## BankDepositRequest / BankWithdrawRequest (55–58)

| | |
| --- | --- |
| **Rôle** | Déposer / retirer vers la banque du personnage |
| **Entrées** | Slot / quantité (wire binaire — voir codecs banque) |
| **Sorties** | Result + `BankSnapshot` (59) |
| **Erreurs** | Fonds insuffisants ; slot invalide |
| **Guide** | — |
| **État** | Livré |

## ShopBuyRequest / ShopSellRequest (51–54)

| | |
| --- | --- |
| **Rôle** | Achat / vente PNJ boutique |
| **Entrées** | Identifiants boutique / item / qty |
| **Sorties** | Result + inventaire / or mis à jour |
| **Erreurs** | Stock / or insuffisant |
| **État** | Livré — **≠** échange joueur (84–86) |

## QuestTurnInRequest / QuestTurnInResult (70 / 71)

| | |
| --- | --- |
| **Rôle** | Rendre une quête (idempotent) |
| **Entrées** | Quête + `requestId` |
| **Sorties** | Résultat + journal (`QuestJournalSnapshot` 69) |
| **Erreurs** | Objectifs incomplets ; replay |
| **État** | Livré |

## Craft (opcodes craft Phase 8)

| | |
| --- | --- |
| **Rôle** | Craft recette avec `requestId` idempotent |
| **Entrées** | Recette + requestId |
| **Sorties** | Résultat + inventaire |
| **Erreurs** | Ingrédients / métier manquants |
| **État** | Livré (détail wire : enum `PacketId` suite craft) |

## DialogueChoiceRequest (67 / 68)

| | |
| --- | --- |
| **Rôle** | Choisir une option de dialogue |
| **Entrées** | Token session opaque + index choix |
| **Sorties** | `DialogueStatePush` / result |
| **Erreurs** | Token invalide / expiré |
| **État** | Livré |

## À remplir (au fil des lots)

- Groupes / guildes / amis / blocage — P10-1
- Trade P2P — P10-2
- Aide / rebind / version UI — P10-3a livré (`HelpForm`, `OptionsForm`, badge 10.3.0)
