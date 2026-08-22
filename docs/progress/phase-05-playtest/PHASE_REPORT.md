# Phase 5 — Client/server playtest — PHASE REPORT

## Status

**PHASE 5 GATE REACHED — WAITING FOR REVIEW** (corrections after temporary rejection of `baaf79c`)

- Phase 4 accepted: `22d19b4570eaf552e5ce162243a83020ce86e2eb`
- Rejected tip (CI green, product incomplete): `baaf79c846f1151f7e7a5f544812756635f1fcfd`
- Green corrections head: `af71e6f24c1650a2e945a667c2e8a022acc1b2cb`
- CI: https://github.com/Netsuno/MMO_Maker/actions/runs/32596037772
- Branch: `cursor/phase0-baseline-audit-02c7` (PR #2 only)
- Phase 6: **not started**

## Corrections delivered

1. Shared `PlaytestChildEnvironment.Sanitize` for **server and client**; child-process probe proves DB vars absent; secrets never printed.
2. Brand-new unsaved maps: validate → save → MapId → publish → load published snapshot → launch (unit + PG).
3. Recursive warp closure (BFS + visited): A→B→C, cycles, shared targets, unpublished transitive fail; E2E crosses two consecutive warps over TCP.
4. Selectable spawn (`PlaytestSpawnDialog` / canvas hover default / smoke override) + `PlaytestSpawnValidator` (bounds, blocked, 1×1, edges).
5. Lifecycle: async log drain, Hello readiness on `127.0.0.1`, owned-PID-only kill, await exit, temp cleanup, bind force.
6. E2E uses real framed TCP for move / block / warps / map blobs — **no** direct `MovementService` asserts.
7. Loopback framing/protocol tests on real `ClientSession` parser.
8. Visual: schematics labeled honestly; manual WPF capture **NOT RUN** on Linux agent; real process launch proven in automated tests.

## CI counts (`af71e6f`)

| Suite | Result |
| --- | --- |
| Frog.Tests | 165/165 |
| PostgreSQL integration | 18/18 |
| Windows smoke | 9/9 × 3 |

## Evidence pack

Sibling files in this directory. PNGs are schematics only — see `KNOWN_ISSUES.md` / `TEST_RESULTS.md`.
