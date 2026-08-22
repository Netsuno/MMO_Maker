# Demande de revue — Phase 5 (corrections)

## Contexte

Phase 5 temporarily rejected at `baaf79c` despite green CI. Corrections on the **same** branch/PR only.

## Plage de commits

- After rejected tip: `baaf79c846f1151f7e7a5f544812756635f1fcfd`
- Green head: `af71e6f24c1650a2e945a667c2e8a022acc1b2cb`
- CI: https://github.com/Netsuno/MMO_Maker/actions/runs/32596037772
- PR: #2 — `cursor/phase0-baseline-audit-02c7`

## CI counts

| Suite | Passed | Failed | Total |
| --- | ---: | ---: | ---: |
| Frog.Tests | 165 | 0 | 165 |
| Frog.Persistence.IntegrationTests | 18 | 0 | 18 |
| Frog.Editor.WindowsSmokeTests | 9×3 | 0 | 9×3 |

## Checklist corrections

- [x] DB secrets stripped from server **and** client child envs + probe test
- [x] Brand-new unsaved map (unit + PG)
- [x] Recursive warp graph + E2E two consecutive warps (TCP)
- [x] Selectable/validated spawn
- [x] Lifecycle/logging/Hello readiness/owned kill/temp cleanup/`127.0.0.1`
- [x] TCP E2E without direct MovementService
- [x] Real framing/protocol loopback tests
- [x] Visual honesty: schematics ≠ screenshots; manual **NOT RUN**

## Trois risques

See `KNOWN_ISSUES.md`.

## Phase 6

**Not started.**
