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

R2-6: CI verifies SHA-256 of Phase 8 smoke PNGs against committed `SCREENSHOT_MANIFEST.md` and fails on file/hash mismatch. All 12 committed rows use **exact-sha**. Client `01`/`06` screenshot the gameplay TabControl (no log clock). Editor screenshot tests use deterministic content GUIDs and focus Save before capture. Client `02`–`05` are panel surfaces so `01`≠`02` and `03`≠`04`. Capture tip `fa8c44f` is the screenshot pin; implementation tip `3c36417f320858e950d65e7de120b9f749e74769` is separate (prior Interact pin `09e68df` is historical only). Do not skip or weaken the ×3 Phase 8 smokes.

## Non-cooperative close proof

The old `MainForm_NonCooperativeInit` **UI smoke** (blocking workspace init across `WaitAsync` + shared STA `PumpUntil`) was removed after it deadlocked the editor smoke host (CI hang 2m / blame-hang abort). That hang is historical only.

Non-cooperative initialization proof is **not** gone: `EditorMainFormCloseCoordinatorTests.NonCooperativeInit_*` cover timeout, scope retention, retry, and no-deadlock window-alive semantics on the pure coordinator (no STA pump). UI coverage includes `MainForm_NonCooperativeSave_*`, cooperative `MainForm_RealClose_WhileInitializationPending_*` / `MainForm_RealClose_WhileSavePending_*`, and dispose-once / ActiveScopeCount→0.

## Historical stubs (not Phase 8 gate)

Phase 8 work does not claim the whole repository is placeholder-free. Pre-existing stubs/debt remain out of this gate (e.g. unused `Frog.Client/Models/*` and some `Frog.Client/Services/*` `// TODO` placeholders documented under Phase 7; MariaDB map-event legacy files listed above).

## Phase 9

À la clôture de Phase 8 : not started.

**Mise à jour 2026-09-18 :** Phase 9 **ACCEPTED** (merge `f74b34c`, PR #7). Résidus (TLS, packaging client/éditeur, LOAD, restore sanctions, P9-S) → Phase 10 [`../phase-10-beta-release/`](../phase-10-beta-release/).
