# Phase 6 — Essential Content Editors (Gate Resubmission)

## Status

**GATE REACHED — WAITING FOR REVIEW**

## Branch tip (evidence HEAD)

`8fdffc5` — fix(phase-06): address review blockers — UI smokes, previews, init, dirty guards

## CI

Pending — see `TEST_RESULTS.md` after green run on tip.

## Review blocker fixes

| Blocker | Fix |
| --- | --- |
| 1 — Game Data smokes not UI | Replaced session-direct smokes with `GameDataSmokeUiDriver` driving `MainWindow.CmdGameData`, real panels/buttons, search/status filters, close/reopen |
| 2 — Tileset/NPC dirty on bind | Added `_binding` guards + preview refresh; dirty-state regression smoke |
| 3 — Visual previews missing | `AssetPreviewControl` + `ProjectAssetPathResolver` on tilesets, NPCs, items, spells, resources |
| 4 — Spawn filters incomplete | Map + resource catalog filters on `ResourceSpawnEditorPanel` |
| 5 — Init blocks UI / leaks DbContext | `GameDataInitializationService` — single async migrate, loading overlay, cancellation, `EditorPostgreSqlScope` disposal |

## UI scenarios verified (Windows smoke)

- Tilesets: New → edit path/preview → Save draft → Publish → search/filter → close/reopen
- NPCs, items, spells, classes, shops: full create/save/publish via real controls
- Resources/spawns: resource publish + spawn tab with map/resource filters + save/publish
- Dirty-state: clean after open/save/publish; dirty after edit; cancel navigation keeps record
- Init leak: open/close Game Data ×3 without failure
- Asset preview: valid/missing/corrupt/traversal/refresh/disposal

## PostgreSQL

All existing integration tests retained (31 scenarios).

## Phase 7

Not started.
