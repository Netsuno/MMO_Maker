# Phase 7 — PHASE_REPORT (CHANGES REQUESTED → P7-G1…G6 remediations)

## Status

**Phase 7: CHANGES REQUESTED** — P7-G1…G6 remediations applied; CI green on implementation tip; **waiting for re-review**.

| Workstream | Status |
| --- | --- |
| P7-FIX-1…5 / P7-R1…R7 (prior) | DONE — preserved |
| P7-G1 Token leak + reject client stats | DONE |
| P7-G2 17-step PG E2E decoded asserts | DONE |
| P7-G3 EF cancel cleanup + drop double equip write | DONE |
| P7-G4 Request-id uniqueness + combat race guarantees | DONE |
| P7-G5 Named UI + strict success smokes | DONE |
| P7-G6 Graceful packaged shutdown + evidence | DONE |

## Identity

| Item | Value |
| --- | --- |
| Branch | `cursor/phase0-baseline-audit-02c7` |
| PR | #2 |
| Phase 6 accepted implementation | `99b782f8f205c0161c0bba8838d041714e39947e` |
| Phase 6 accepted evidence tip | `f4db56592346d9bf0cad9ca153aaeff11ee65de8` |
| Phase 7 implementation SHA | `c1803132522d8dfb31e3a1284755341eb2d243b2` |
| CI (implementation) | https://github.com/Netsuno/MMO_Maker/actions/runs/33036286537 |
| Phase 8 | **Not started** |

## What changed (this pass)

### P7-G1
- Client logs `Login OK` / `Reconnect OK` only; token stored privately; `SanitizeSecrets` redacts base64url-like tokens.
- Stats editor UI hidden; `CharacterStatsUpdateRequest` rejected unless playtest or `AllowInMemoryFallback`.
- Smoke asserts token absent from log; production-composition negative test in `Frog.Tests`.

### P7-G2
- Full gameplay PG E2E: character list, map id, melee/spell success, exact XP, rate-limit Error, buy/sell deltas, bank item+gold round-trip, respawn pose, post-restart exact persistence.

### P7-G3
- Economy + inventory-transfer repos: catch all exceptions including cancellation; rollback with `CancellationToken.None`; clear ChangeTracker.
- Same-gate contamination tests; removed post-commit `PersistEquipmentAsync` double write.

### P7-G4
- Economy PK `(character_id, request_id)`; operation+fingerprint must match for replay; different op/payload rejected.
- Concurrent identical requests from separate DbContexts → replay/conflict (not unhandled unique violation).
- Combat race asserts exact XP amount, monster gone, persisted XP; player melee under per-defender lock.

### P7-G5
- Inventory/equipment/bank/ground show published names; ground pickup UI; NPC target combo; stats editor remains hidden.
- Smoke facts require success paths and typed state (no “or refused”).

### P7-G6
- Packaged server: SIGTERM + `FROG_SHUTDOWN_FILE` graceful stop; exit code 0; sessions drain without `pg_terminate_backend`; Kill only on failure timeout.
- Evidence dossier + screenshot hashes regenerated from CI artifact on tip `c180313`.

## Suite counts on tip

271 → **272** unit; 66 → **97** PG; editor 35×3; gameplay **5×3**.

## Phase 8

Not started.
