# Phase 04 — Map Editor MVP (gate data safety)

## Identification

- Date : 2026-08-22
- Branche : `cursor/phase0-baseline-audit-02c7`
- Gate Phase 3 accepté : `20eedc1`
- Plage revue Phase 4 : `20eedc1..HEAD`
- PR : #2

## Verdict proposé

**READY FOR REVIEW** — corrections gate data safety livrées ; Phase 5 non commencée.

## Livré (corrections gate)

| Exigence | Livrable |
| --- | --- |
| Mode mémoire vs PostgreSQL | `MapRepositoryCapabilities`, `AllowsSave`, `NotDurable`, labels UI sans faux « PostgreSQL » |
| Brouillon / publication séparés | Snapshots immuables PG + in-memory, historique, `LoadPublishedByIdAsync` |
| Concurrence optimiste atomique | `Revision` token EF + `ExecuteUpdate` conditionnel → `Conflict` |
| Erreurs PostgreSQL | `PersistenceFailed`, mutex save, état occupé UI, carte conservée en mémoire |
| Dirty + prompts | Save/Discard/Cancel avant changement carte, nouvelle carte, ouverture fichier, fermeture |
| Warps | Limites selon carte cible, `ValidationFailed` hors limites |
| Tests édition | `MapEditOperations` + tests crayon/rectangle/fill/gomme/block/warp/couche |
| Smoke Windows | 1 test — chemin commande `SaveMapAsync` (dialogs injectables) |

## Non commencé (Phase 5+)

- Playtest serveur depuis carte publiée PG
- Nouvelles fonctions MariaDB

## Décision requise

Accepter Phase 4 (gate data safety) et autoriser Phase 5, ou demander ajustements.
