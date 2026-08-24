# Phase 6 — Test Results (Second Fix Pass)

## Implementation SHA (reviewed code)

`1a09213` — Expand Game Data UI smoke tests for P6-B6 review blocker

## Branch tip (documentation may trail)

See `docs/STATUS.md` for current branch tip and CI URL after final push.

## CI

Pending — update after green run on final tip.

## Verified counts (expected on tip)

| Suite | Expected |
| --- | ---: |
| Frog.Tests (unit) | 244 |
| PostgreSQL integration | 37 |
| Windows smoke | 26 × 3 consecutive passes |

New PostgreSQL tests: `PostgresGameDataScopeLifecycleTests` (4), `PostgresGameDataConcurrencyTests` (2)

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
| Shops | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | — |
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
- Scope lifecycle: single migrate per scope, dispose, drain, repeated open/close, failed connection cleanup
- Concurrency: parallel reads on shared gate; rapid spawn filter changes

## Phase 7

Not started.
