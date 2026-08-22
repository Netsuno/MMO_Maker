# Demande de revue — Phase 5 Client/server playtest

## Contexte

Phase 4 acceptée sur `22d19b4`. Phase 5 implémente le playtest d’une carte **explicitement publiée** (PostgreSQL), serveur/client locaux, protocole uniquement, cleanup processus.

## Critères

| Critère | État |
| --- | --- |
| Unit/E2E/protocol `Frog.Tests` 145/145 | OK (local) |
| PostgreSQL 17/17 | OK (local) |
| Windows smoke 9×3 | CI Windows (voir URL ci-dessous) |
| Draft jamais chargé par le serveur playtest | OK (unit + PG + E2E) |
| Shutdown propre / pas d’orphan listener | OK (E2E) |
| Phase 6 non commencée | OK |
| `git status` clean après push | OK |

## Plage de commits

- Début (après Phase 4) : `22d19b4570eaf552e5ce162243a83020ce86e2eb`
- Head vert : _voir SHA poussé + CI_

## Preuves

- [`PHASE_REPORT.md`](PHASE_REPORT.md)
- [`TEST_RESULTS.md`](TEST_RESULTS.md)
- [`CHANGE_SUMMARY.md`](CHANGE_SUMMARY.md)
- [`KNOWN_ISSUES.md`](KNOWN_ISSUES.md)
- Screenshots schématiques : `playtest-launch.png`, `playtest-client-running.png`

## Trois risques principaux

Voir [`KNOWN_ISSUES.md`](KNOWN_ISSUES.md).

## Question de gate

**Accepter Phase 5** et autoriser Phase 6 ?
