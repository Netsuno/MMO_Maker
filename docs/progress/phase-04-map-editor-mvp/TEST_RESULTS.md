# Phase 04 — résultats de tests

## Commandes (head courant)

```bash
dotnet build Frog.Creator.sln -c Release
dotnet test Frog.Tests/Frog.Tests.csproj -c Release --no-build
export FROG_POSTGRES_TEST_CONNECTION_STRING='Host=127.0.0.1;Port=5432;Database=frog_test;Username=frog_test;Password=frog_test_local_only'
dotnet test tests/Frog.Persistence.IntegrationTests/Frog.Persistence.IntegrationTests.csproj -c Release --no-build
# Windows CI :
FROG_EDITOR_FORCE_IN_MEMORY=1 dotnet test tests/Frog.Editor.WindowsSmokeTests/Frog.Editor.WindowsSmokeTests.csproj -c Release --no-build
```

## Résultats locaux (agent Linux, Release)

| Suite | Passed | Failed | Total |
| --- | ---: | ---: | ---: |
| Frog.Tests | 129 | 0 | 129 |
| Frog.Persistence.IntegrationTests | 16 | 0 | 16 |

## Smoke Windows (CI `windows-latest`)

| Test | Rôle |
| --- | --- |
| `MainWindow_OpensAndSavesDemoMap_WithInMemoryRepository` | Shell + commande `SaveMap()` + état occupé |
| `MainWindow_DirtyCloseCancel_KeepsWindowOpen` | Fermeture dirty → Cancel |
| `MainWindow_DirtyCloseDiscard_ClosesWindow` | Fermeture dirty → Discard |
| `MainWindow_DirtyCloseSaveSuccess_ClosesWindow` | Fermeture dirty → Save OK |
| `MainWindow_DirtyCloseSaveFailed_KeepsWindowOpenAndDirty` | Save échoué → fenêtre ouverte + dirty |
| `MapCanvas_UndoRedo_RestoresPaintedTileBlockWarpLayerAndMapName` | Chemin canvas réel + undo/redo |
| `MapCanvas_LockedLayer_DoesNotAcceptPaint` | Couche verrouillée |

**Total smoke : 7 tests**

**Lien run GitHub Actions :** _(à remplir après push CI)_

## Scénarios gate couverts

| Scénario | Test |
| --- | --- |
| Init PG base vide (`MapId = null`) | `InitializeSession_OnEmptyDatabase_SeedsDemoSavePublishAndReload` |
| Fixtures warp valides (cible créée d’abord) | Tous les `CreateSampleMap` PG |
| `PersistenceFailed` + rollback | `Save_RollsBack_WhenFailureOccursBeforeCommit` |
| Même repository : create → publish → draft → republish | `Save_SameRepositoryInstance_PublishSequenceHasCorrectRevisions` |
| Concurrence 2 DbContext | `Save_ConcurrentDbContexts_ExactlyOneSucceeds` |
| Fermeture WPF dirty | `EditorCloseSmokeTests` (4 cas) |
| Canvas utilise `MapEditOperations` | `MapCanvasUndoSmokeTests` |

## Confirmation CI Phase 3

- [x] Gate Phase 3 accepté (`20eedc1`)

## Non exécuté localement

- Smoke Windows (Linux — CI `windows-latest`)
