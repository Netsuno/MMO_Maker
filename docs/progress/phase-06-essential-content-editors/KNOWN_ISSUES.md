# Phase 6 — Known Issues (Third Fix Pass)

## Fixed in this pass

- WinForms panel async lifecycle (context, tracking, cancel/close, dispose)
- PostgreSQL scope lifecycle evidence via `EditorPostgreSqlScope` + init-failure dispose
- Protected-delete / search-status / invalid-publish false positives
- Incomplete Resource Spawn UI matrix
- Solid-color preview-only screenshots replaced with full-editor UI frames
- Stale gate metadata patterns (docs use Implementation SHA)

## Remaining known issues

- Windows `GameDataInitializationLeakSmokeTests` still uses in-memory repositories (PostgreSQL lifecycle covered by integration tests)
- Shop listing / class spell protected-delete smokes use minimal published prerequisites
- Sync-context identity under the shared WPF/WinForms STA host is not asserted as a hard failure; lifecycle tests assert STA thread id via barriers instead

## Phase 7

Not started.
