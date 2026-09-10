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
| Carte démo dans le shell | OK (logique + smoke CI PASS) |
| Smoke Windows déterministe | OK — CI run 32575250906 |
| Repository mémoire (sans PG/MariaDB) | OK |
| Identité moderne (pas LegacyId actif) | OK — migration `ModernMapIdentity` |
| Pas de DB dans Forms | OK |

## Preuves

- [`PHASE_REPORT.md`](PHASE_REPORT.md)
- [`TEST_RESULTS.md`](TEST_RESULTS.md) — 110 + 7 locaux ; smoke CI PASS
- [`CHANGE_SUMMARY.md`](CHANGE_SUMMARY.md)
- [`KNOWN_ISSUES.md`](KNOWN_ISSUES.md)

## Question de gate

**Accepter Phase 3** et autoriser Phase 4 (Map Editor MVP) ?

**Phase 4 non commencée.**
