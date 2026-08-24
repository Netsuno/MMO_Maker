# Phase 6 — Known Issues (Gate Resubmission)

## Fixed regressions (this gate)

- Tileset/NPC panels marked dirty on `BindForm()` without user input
- Game Data smoke tests bypassed UI (session save/publish direct calls)
- Missing visual asset previews on applicable editor panels
- Resource spawn catalog exposed status filter only
- Synchronous per-factory `Database.Migrate()` on UI thread; no DbContext disposal
- Raw `MessageBox.Show` in Game Data panels blocked Windows smokes (modal hang)
- Dirty-state cancel navigation smoke timeout / incorrect expectations
- Tileset list selection not reverted when declining unsaved navigation

## Remaining known issues

- Preview screenshots in docs rely on smoke programmatic verification; no checked-in PNG artifacts yet
- Class/shop UI smokes use minimal field sets (no protected-deletion UI scenarios unless referenced content exists)
- NPC/Item/Spell/Class/Shop panels do not yet revert list selection on unsaved navigation cancel (tileset only)

## Implemented but not separately verified

- PostgreSQL durable path for `GameDataInitializationService` single-migrate (covered by existing PG integration suite)

## Phase 7

Not started.
