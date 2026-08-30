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

Client screenshots `01`/`02` and `03`/`04` historically shared SHA-256 digests (identical pixel buffers from earlier CI tip `7137c17`). Manifest retained for gate archaeology; regenerate unique captures on a green tip when visual evidence is re-audited. Functional gate evidence is the PostgreSQL E2E + Windows smoke ×3, not screenshot uniqueness alone.

## Smoke coverage note

`MainForm_NonCooperativeInit` UI smoke was removed: blocking workspace init across `WaitAsync` + shared STA `PumpUntil` deadlocked the editor smoke host (CI hang 2m / blame-hang abort). P8-I1 remains covered by cooperative init-cancel theory tests, `MainForm_RealClose_WhileSavePending_*`, and `MainForm_NonCooperativeSave_*`.

## Phase 9

Not started.
