# Référence — Éditeur

## Publier le contenu (PostgreSQL)

| | |
| --- | --- |
| **Rôle** | Pousser brouillons (cartes, catalogues) vers le monde publié |
| **Entrées** | Contenu éditeur + overlay connexion **locale** (jamais dans git) |
| **Sorties** | Snapshots publiés consommés par le serveur / client |
| **Erreurs** | Connexion refusée ; validation schéma |
| **Guide** | [CREATOR_QUICKSTART](../progress/phase-10-beta-release/guides/CREATOR_QUICKSTART.md) (brouillon) |
| **État** | From-source **oui** ; lancement depuis paquet publié **non prouvé** (P10-6) |

## Événements carte — commandes typées

Catalogue : [`../progress/phase-08-quests-events-advanced-creation/COMMAND_CATALOG.md`](../progress/phase-08-quests-events-advanced-creation/COMMAND_CATALOG.md).

Exemples : `show_text`, `start_dialogue`, `give_item` / `take_item`, `start_quest` / `turn_in_quest`, `teleport`, `call_common_event`…

| | |
| --- | --- |
| **Rôle** | Interpréteur serveur autoritaire des pages d’événement |
| **Entrées** | Discriminateur + paramètres typés (schéma par commande) |
| **Sorties** | Effets session/personnage ; parfois visible client |
| **Erreurs** | Validation paramètres ; budget steps ; idempotence |
| **État** | Livré Phase 8 — **pas** d’admin mute/kick via ces commandes |

## Menu héritage MariaDB

| | |
| --- | --- |
| **Rôle** | Ancien chemin « Publier vers MariaDB » |
| **État** | UI encore visible ; **ne pas** l’utiliser pour la bêta PG |

## À remplir

- Playtest depuis binaires livrés
- Outils opérateur dans l’éditeur (si un jour)
