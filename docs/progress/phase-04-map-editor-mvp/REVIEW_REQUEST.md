# Demande de revue — Phase 4 Map Editor MVP

## Contexte

Phase 3 acceptée. Phase 4 livre l’éditeur de cartes MVP : peinture, collisions, warps, undo/redo, sauvegarde/publication PostgreSQL.

## Critères

| Critère | État |
| --- | --- |
| Peinture / gomme / remplissage / rectangle | OK — canvas existant |
| Undo / redo | OK — `MapUndoController` |
| Collision (`Block`) | OK — palette + overlay |
| Warp + destination configurable | OK — dialogue + défaut carte courante |
| Enregistrement PostgreSQL | OK — `SaveCurrentAsync` Draft |
| Publication PostgreSQL | OK — statut `Published` |
| Tests unitaires + PG + smoke save | OK — 114 + 10 + 2 smoke |
| Pas de nouvelle feature MariaDB | OK |

## Preuves

- [`PHASE_REPORT.md`](PHASE_REPORT.md)
- [`TEST_RESULTS.md`](TEST_RESULTS.md)
- [`CHANGE_SUMMARY.md`](CHANGE_SUMMARY.md)
- [`KNOWN_ISSUES.md`](KNOWN_ISSUES.md)

## Question de gate

**Accepter Phase 4** et autoriser Phase 5 (playtest) ?

**Phase 5 non commencée.**
