# Phase 5 — Client/server playtest — PHASE REPORT

## Status

**PHASE 5 GATE REACHED — WAITING FOR REVIEW**

- Base accepted (Phase 4): `22d19b4570eaf552e5ce162243a83020ce86e2eb`
- Branch: `cursor/phase0-baseline-audit-02c7` (PR #2)
- Phase 6: **not started**

## Objective delivered

Reliable playtest of an **explicitly published** PostgreSQL map:

1. Validate → save dirty draft → publish (or pin existing published revision).
2. Never playtest unsaved in-memory-only changes (`IsDirty` forces draft save).
3. Editor starts local test server + client with correlated IDs.
4. Server loads **published** `MapId` + revision via playtest manifest (`.fmap` blobs).
5. Unpublished drafts are never sent to server/client (no PG connection string on playtest processes).
6. Configurable spawn tile applied on login in playtest mode.
7. Server remains authoritative for movement, collision, warps.
8. Client receives map/runtime data only through the TCP protocol.
9. Correlated logs: `PlaytestCorrelationId` / MapId / revision in editor + server scopes.
10. Editor stops only processes it started; cleanup on cancel, failure, and form close.

## Architecture

| Layer | Responsibility |
| --- | --- |
| `Frog.Application.Playtest` | Preparer, orchestrator, manifest, runtime id allocator |
| `Frog.Persistence.PostgreSql` | `LoadPublishedByIdAndRevisionAsync` (published only) |
| `Frog.Server` | Manifest → `PlaytestMapBlobStore`; spawn options; **no PG** |
| `Frog.Editor` | Menu Playtest / Stop; process launcher; UI wiring only |
| `Frog.Client` | CLI `--playtest --host --port --correlation` (no DB) |

## Local verification (pre-CI)

| Suite | Result |
| --- | ---: |
| `Frog.Tests` | **145/145** PASS |
| `Frog.Persistence.IntegrationTests` | **17/17** PASS |
| Windows smoke | CI-only (Linux agent) — expected **9** tests ×3 |

## Evidence pack

See sibling files in this directory + schematic screenshots:

- `playtest-launch.png`
- `playtest-client-running.png`
