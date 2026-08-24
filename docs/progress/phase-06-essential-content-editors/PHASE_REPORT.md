# Phase 6 — Essential Content Editors (Gate Resubmission)

## Status

**GATE REACHED — WAITING FOR REVIEW**

## Branch tip (evidence HEAD)

`465d6c6` — fix(phase-06): correct dirty-state cancel navigation smoke regression

## CI

https://github.com/Netsuno/MMO_Maker/actions/runs/32693724623 — success

| Suite | Count |
| --- | ---: |
| Frog.Tests | 244 |
| PostgreSQL | 31 |
| Windows smoke | 23 × 3 |

## Review blocker fixes

| Blocker | Fix |
| --- | --- |
| 1 — Game Data smokes not UI | `GameDataSmokeUiDriver` drives `MainWindow.CmdGameData`, real panels/buttons, search/status filters, close/reopen for all 7 slices |
| 2 — Tileset/NPC dirty on bind | `_binding` guards on Tileset/NPC panels; `GameDataDirtyStateSmokeTests` |
| 3 — Visual previews missing | `AssetPreviewControl` + `ProjectAssetPathResolver` on tilesets, NPCs, items, spells, resources |
| 4 — Spawn filters incomplete | Map + resource catalog filters on `ResourceSpawnEditorPanel` |
| 5 — Init blocks UI / leaks DbContext | `GameDataInitializationService` — single async migrate, loading overlay, cancellation, `EditorPostgreSqlScope` disposal |

## Post-review CI fixes (this tip)

- Restored `ConfigureInMemoryRepository` in smoke helper (stack overflow)
- Routed all Game Data `MessageBox.Show` through `GameDataUiMessageBox` (smoke hook; prevents modal hang)
- WinForms `DoEvents` in `StaTestRunner.PumpUntil` for Game Data message delivery
- Tileset list selection revert on unsaved navigation cancel; corrected dirty-state smoke expectations

## UI scenarios verified (Windows smoke)

See `TEST_RESULTS.md` for per-slice detail. Summary:

- Create, edit, duplicate path, Save draft, Publish via real buttons for all seven categories
- Search and status filters exercised (spawn map/resource filters on resource tab)
- Close/reopen verifies persisted catalog state
- Dirty-state regression: clean after open/save/publish; dirty after control edit; cancel keeps record synchronized
- Init open/close ×3 without DbContext leak
- Asset preview states: loaded, missing, corrupt, traversal rejected, refresh, disposal

## PostgreSQL scenarios

All existing integration tests retained (31 scenarios) — draft/publish round trips, reference validation, published catalog consumers.

## Fixed regressions

- Tileset/NPC marked dirty on `BindForm()` without user input
- Session-direct smoke saves bypassing UI
- Missing visual previews
- Resource spawn map/resource filters not exposed
- Synchronous per-factory migrate on UI thread; DbContext lifetime leak
- Modal MessageBox blocking Windows smokes
- Dirty-state cancel navigation test timeout / incorrect expectations

## Remaining known issues

See `KNOWN_ISSUES.md`

## Phase 7

Not started.
