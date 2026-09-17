# Phase 8 — Known Issues

## Deferred (not Phase 8 gate requirements unless required by a vertical slice)

- Generalized loot-table editors
- Roles/permissions beyond event command authority scopes
- Arbitrary user scripting runtime (requires separate threat model)
- Phase 9 packaging, admin moderation, load certification

## Legacy (P8-1 retirement target)

- `MariaDbMapEventStore`, `MapEventsMariaDbReader`, `MapEventsMariaDbWriter` — fichiers héritage conservés ; plus utilisés par l’éditeur ni la composition serveur PG.
- `script_key` on legacy catalog — metadata only; must not execute.
- `WorldFlagsPatchRequest` — rejected in PostgreSQL production; client demo button removed in prior pass.
- Event `wait` resume across disconnect — deferred; waits pause page execution (not permanent server termination).
- Parallel runner heartbeat re-entry — autorun fires once per map visit; parallel uses dedup tracker.

## Screenshot evidence note

R2-6: CI verifies SHA-256 of Phase 8 smoke PNGs against this committed `SCREENSHOT_MANIFEST.md` (`scripts/verify-phase8-screenshot-manifest.ps1`) and fails on file/hash mismatch. Client captures wait for the claimed UI state; `02`–`05` are panel surfaces so `01`≠`02` and `03`≠`04`. `01`/`06` screenshot the gameplay TabControl (not the log clock). Editor screenshot tests use deterministic content GUIDs. Do not skip or weaken the ×3 Phase 8 smokes.

## Smoke coverage note

`MainForm_NonCooperativeInit` UI smoke was removed: blocking workspace init across `WaitAsync` + shared STA `PumpUntil` deadlocked the editor smoke host (CI hang 2m / blame-hang abort). P8-I1 remains covered by cooperative init-cancel theory tests, `MainForm_RealClose_WhileSavePending_*`, and `MainForm_NonCooperativeSave_*`.

## Phase 9

Not started.
