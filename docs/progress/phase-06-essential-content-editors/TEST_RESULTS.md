# Phase 6 — Test Results (Second Fix Pass)

## Branch tip

`3d8ff73044f72963836ad8cb0bf43c1870811f50`

## Implementation SHA (reviewed code)

`3d8ff73` — includes P6-B1…B8 fixes (gate reentrancy, preview clone, smoke delete selection)

Documentation-only commits after this SHA, if any, are noted separately in `docs/STATUS.md`.

## CI

https://github.com/Netsuno/MMO_Maker/actions/runs/32785847198 — **success** on branch tip `3d8ff73`

## Verified counts (branch tip)

| Suite | Count |
| --- | ---: |
| Frog.Tests (unit) | 244 |
| PostgreSQL integration | 36 |
| Windows smoke | 26 × 3 consecutive passes |

New PostgreSQL tests: `PostgresGameDataScopeLifecycleTests` (3), `PostgresGameDataConcurrencyTests` (2)

New Windows smoke tests: `GameDataInitialCategorySmokeTests`, `GameDataSpawnFilterSmokeTests`, expanded `GameDataAssetPreviewSmokeTests`

## Build

0 errors, 0 warnings (Release)

## UI smoke matrix (real controls via `GameDataSmokeUiDriver`)

| Editor | Create | Edit | Duplicate | Save | Publish | Invalid publish | Search/filter | Close/reopen | Cancel dirty nav | Delete | Protected delete |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Tilesets | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | — |
| NPCs | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | — |
| Items | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | shop listing |
| Spells | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | class reference |
| Classes | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | — |
| Shops | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | item listing |
| Resources/spawns | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | spawn reference |

Additional regressions: initial tileset category visible after init; spawn map/resource “Toutes…” filters; preview GC retention; repeated open/close (in-memory UI).

## Preview screenshots

`docs/progress/phase-06-essential-content-editors/screenshots/`

- `tileset-preview-smoke.png`
- `npc-preview-smoke.png`
- `item-preview-smoke.png`
- `spell-preview-smoke.png`
- `resource-preview-smoke.png`

Regenerated/overwritten during Windows smoke `AssetPreview_GameDataPanels_SaveSmokeScreenshots`.

## PostgreSQL scenarios

- Existing 31 integration scenarios retained
- Scope lifecycle: single migrate per scope, dispose, drain, repeated open/close
- Concurrency: parallel reads on shared gate; rapid spawn filter changes

## Phase 7

Not started.
