# Phase 6 — Test Results (Gate Resubmission)

## HEAD

`e29b188`

## CI run

https://github.com/Netsuno/MMO_Maker/actions/runs/32693883134 — **success**

## Verified counts (CI tip)

| Suite | Count |
| --- | ---: |
| Frog.Tests (unit) | 244 |
| PostgreSQL integration | 31 |
| Windows smoke | 23 × 3 consecutive passes |

New smoke tests: `GameDataDirtyStateSmokeTests`, `GameDataInitializationLeakSmokeTests`, `GameDataAssetPreviewSmokeTests`

## Build

0 errors, 0 warnings (Release)

## UI smoke scenarios (real controls)

All seven slices via `MainWindow` → Données de jeu (`GameDataSmokeUiDriver`):

1. **Tilesets** — New, edit path/preview, Save draft, Publish, search/status filter, close/reopen
2. **NPCs** — New, sprite path/preview, Save, Publish
3. **Items** — New, icon path/preview, Save, Publish
4. **Spells** — New, icon path/preview, Save, Publish
5. **Classes** — Prerequisite spell publish, New class, Save, Publish
6. **Shops** — Prerequisite item publish, New shop, Save, Publish
7. **Resources/spawns** — Yield item, resource publish, spawn tab map/resource filters, Save, Publish

Additional UI regressions:

- **Dirty state** — clean after open/save/publish; dirty after edit; cancel navigation preserves edits and list selection
- **Init leak** — open/close Game Data ×3 without failure
- **Asset preview** — valid/missing/corrupt/traversal/refresh/disposal (`AssetPreviewControl`)

## Preview verification

- Unit: `ProjectAssetPathResolverTests` (valid, missing, traversal, absolute rejection)
- Smoke: `GameDataAssetPreviewSmokeTests` — programmatic `AssetPreviewState` assertions
- Runtime: preview controls wired on tileset, NPC, item, spell, resource panels during smokes

## PostgreSQL

All 31 integration scenarios retained and passing on CI.

## Phase 7

Not started.
