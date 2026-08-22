# Phase 04 — résultats de tests

## Identification

- Date d’exécution : **2026-08-22**
- Commit : `bad59a48e9546004c216cc809ff987d4c62ac08e`
- Branche : `cursor/phase0-baseline-audit-02c7`
- CI (exit 0) : https://github.com/Netsuno/MMO_Maker/actions/runs/32585353371
- Hôte de test : terminé normalement (pas de crash dispatcher / exit 0)

## Commandes (identiques à la CI)

```bash
dotnet build Frog.Creator.sln -c Release
dotnet test Frog.Tests/Frog.Tests.csproj -c Release --no-build
export FROG_POSTGRES_TEST_CONNECTION_STRING='Host=127.0.0.1;Port=5432;Database=frog_test;Username=frog_test;Password=frog_test_local_only'
dotnet test tests/Frog.Persistence.IntegrationTests/Frog.Persistence.IntegrationTests.csproj -c Release --no-build
# Windows CI (×3 consecutive) :
FROG_EDITOR_FORCE_IN_MEMORY=1 dotnet test tests/Frog.Editor.WindowsSmokeTests/Frog.Editor.WindowsSmokeTests.csproj -c Release --no-build
```

## Résultats CI (head `bad59a4`)

| Suite | Passed | Failed | Total |
| --- | ---: | ---: | ---: |
| Frog.Tests | 129 | 0 | 129 |
| Frog.Persistence.IntegrationTests | 16 | 0 | 16 |
| Frog.Editor.WindowsSmokeTests (pass 1/3) | 7 | 0 | 7 |
| Frog.Editor.WindowsSmokeTests (pass 2/3) | 7 | 0 | 7 |
| Frog.Editor.WindowsSmokeTests (pass 3/3) | 7 | 0 | 7 |

Workflow global : **SUCCESS** (jobs `build-and-test` + `postgres-integration`).

## Smoke Windows — 7 scénarios

| Test | Résultat |
| --- | --- |
| `MainWindow_OpensAndSavesDemoMap_WithInMemoryRepository` | PASS |
| `MainWindow_DirtyCloseCancel_KeepsWindowOpen` | PASS |
| `MainWindow_DirtyCloseDiscard_ClosesWindow` | PASS |
| `MainWindow_DirtyCloseSaveSuccess_ClosesWindow` | PASS |
| `MainWindow_DirtyCloseSaveFailed_KeepsWindowOpenAndDirty` | PASS |
| `MapCanvas_UndoRedo_RestoresPaintedTileBlockWarpLayerAndMapName` | PASS |
| `MapCanvas_LockedLayer_DoesNotAcceptPaint` | PASS |

## Confirmation CI Phase 3

- [x] Gate Phase 3 accepté (`20eedc1`)
