# Phase 6 — Test Results (Gate Resubmission)

## HEAD

`8fdffc5`

## CI run

_TBD — update after green CI on tip_

## Expected counts (tip)

| Suite | Expected |
| --- | ---: |
| Frog.Tests (unit) | 244 |
| PostgreSQL integration | 31 |
| Windows smoke | 23 × 3 |

New smoke tests: `GameDataDirtyStateSmokeTests`, `GameDataInitializationLeakSmokeTests`, `GameDataAssetPreviewSmokeTests`

## Local verification (pre-CI)

- `dotnet build Frog.Creator.sln -c Release` — 0 errors, 0 warnings
- `dotnet test Frog.Tests` — 244 passed (includes 4 `ProjectAssetPathResolverTests`)

## UI smoke scenarios

See `PHASE_REPORT.md` — all seven slices exercised through `MainWindow` → Données de jeu command.

## Preview verification

- Unit: path resolver (valid, missing, traversal, absolute rejection)
- Smoke: `AssetPreviewControl` loaded/missing/corrupt/rejected/refresh states
- Runtime screenshots: preview controls visible in Game Data panels during smoke (programmatic `AssetPreviewState.Loaded` assertion)
