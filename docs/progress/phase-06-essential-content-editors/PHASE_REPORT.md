# Phase 6 — Essential Content Editors (Third Fix Pass)

## Status

**GATE REACHED — WAITING FOR REVIEW**

## Implementation SHA

`b3fe913` — P6-C1…C5 third fix pass (code under review)

Committed documents report this implementation SHA. Later documentation-only commits may advance the branch tip without changing this reviewed code baseline.

## Third-pass blocker fixes

| Blocker | Fix |
| --- | --- |
| P6-C1 WinForms async lifecycle | `GameDataPanelLifecycle` with UI-preserving awaits, tracked ops, cancel/close sync cleanup, force-close for smokes |
| P6-C2 PostgreSQL scope evidence | `EditorPostgreSqlScope` in Persistence; lifecycle tests on real PG counters; init-failure dispose via migrate override |
| P6-C3 False-positive UI asserts | Protected-delete waits for lifecycle idle + exact name; seeded search/status; invalid publish checks validation + revision; separate Spawn matrix |
| P6-C4 Screenshots | Full-editor UI captures ≥800×500 (not solid preview tiles) under `screenshots/` |
| P6-C5 Gate metadata | Docs report Implementation SHA; PR body carries final tip + CI after docs land |

## Schema / migrations

No new migrations.

## Phase 7

Not started.
