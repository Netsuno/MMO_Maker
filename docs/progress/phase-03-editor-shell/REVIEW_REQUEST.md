# Demande de revue — Phase 3 (corrections gate)

## Contexte

Phase 3 avait été **rejetée temporairement** pour :
1. absence de smoke UI Windows ;
2. usage de `LegacyId` / `legacy_id` dans le chemin actif.

Les deux points sont adressés dans la plage `3fc6530..HEAD`.

## Critères PRD

| Critère | État |
| --- | --- |
| Shell (arbre / canvas / panneaux / status) | OK |
| Catalogue via Application | OK — `MapId` Guid |
| Carte démo dans le shell | OK (logique + smoke CI) |
| Smoke Windows déterministe | OK — `Frog.Editor.WindowsSmokeTests` |
| Repository mémoire (sans PG/MariaDB) | OK |
| Identité moderne (pas LegacyId actif) | OK — migration `ModernMapIdentity` |
| Pas de DB dans Forms | OK |

## Preuves

- [`PHASE_REPORT.md`](PHASE_REPORT.md)
- [`TEST_RESULTS.md`](TEST_RESULTS.md) — 110 + 7 locaux ; lien CI smoke
- [`CHANGE_SUMMARY.md`](CHANGE_SUMMARY.md)
- [`KNOWN_ISSUES.md`](KNOWN_ISSUES.md)

## Question de gate

**Accepter Phase 3** et autoriser Phase 4 (Map Editor MVP) ?

Confirmer que le job CI Windows est vert (smoke inclus).

**Phase 4 non commencée.**
