# Phase 6 — Known Issues (Gate Resubmission)

## Fixed regressions (this gate)

- Tileset/NPC panels marked dirty on `BindForm()` without user input
- Game Data smoke tests bypassed UI (session save/publish direct calls)
- Missing visual asset previews on applicable editor panels
- Resource spawn catalog exposed status filter only
- Synchronous per-factory `Database.Migrate()` on UI thread; no DbContext disposal

## Remaining known issues

- Preview screenshots in docs rely on smoke programmatic verification; no checked-in PNG artifacts yet
- Class/shop UI smokes use minimal field sets (no protected-deletion UI scenarios in this pass unless referenced content exists)

## Implemented but not separately verified

- PostgreSQL durable path for `GameDataInitializationService` single-migrate (covered by existing PG integration suite; not re-run in this local pass)

## Phase 7

Not started.
