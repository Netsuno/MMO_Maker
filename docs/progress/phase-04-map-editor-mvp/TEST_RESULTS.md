# Phase 04 — résultats de tests

## Commandes

```bash
dotnet build Frog.Creator.sln -c Release
dotnet test Frog.Tests/Frog.Tests.csproj -c Release --no-build
export FROG_POSTGRES_TEST_CONNECTION_STRING='Host=127.0.0.1;Port=5432;Database=frog_test;Username=frog_test;Password=frog_test_local_only'
dotnet test tests/Frog.Persistence.IntegrationTests/Frog.Persistence.IntegrationTests.csproj -c Release --no-build
# Windows CI :
FROG_EDITOR_FORCE_IN_MEMORY=1 dotnet test tests/Frog.Editor.WindowsSmokeTests/Frog.Editor.WindowsSmokeTests.csproj -c Release --no-build
```

## Résultats locaux (agent Linux)

| Suite | Passed | Failed | Total |
| --- | ---: | ---: | ---: |
| Frog.Tests | 128 | 0 | 128 |
| Frog.Persistence.IntegrationTests | 13 | 0 | 13 |

## Smoke Windows (CI)

| Test | Attendu |
| --- | --- |
| `MainWindow_OpensAndSavesDemoMap_WithInMemoryRepository` | PASS (shell + commande Save réelle) |

**Note :** le smoke Windows contient **1 test** (open + save via chemin commande éditeur).

## Scénarios data safety couverts

| Scénario | Test |
| --- | --- |
| Démo mémoire : save bloqué (`NotDurable`) | `MapPersistenceModeTests.DemoRepository_BlocksSave_WithNotDurable` |
| Test mémoire : save éphémère autorisé | `MapPersistenceModeTests.TestRepository_AllowsEphemeralSave` |
| Init démo locale sans catalogue persistant | `MapWorkspaceSessionTests.Initialize_OpensLocalDemo_WhenDemoRepository` |
| Draft → publish → edit draft → publish précédent inchangé | `MapPersistenceModeTests.DraftPublish_*`, `PostgresMapRepositoryTests.Publish_KeepsPreviousPublishedSnapshotImmutable` |
| Concurrence 2 DbContext même `ExpectedRevision` | `PostgresMapRepositoryTests.Save_ConcurrentDbContexts_ExactlyOneSucceeds` |
| Warp destination hors limites → `ValidationFailed` | `MapPersistenceModeTests.WarpOutOfBounds_*`, PG équivalent |
| Édition + reload sans perte modèle | `MapEditOperationsTests.SaveAndReload_PreservesEditedModel` |
| Couche verrouillée non modifiable | `MapEditOperationsTests.PaintTile_DoesNotModifyLockedLayer` |
| Smoke : `SaveMapAsync` (pas `SaveCurrentAsync` direct) | `EditorMainWindowSmokeTests` |

## Confirmation CI Phase 3

- [x] Gate Phase 3 accepté (`20eedc1`) — smoke Windows Phase 3 vert avant Phase 4

## Non exécuté localement

- Smoke Windows (Linux — CI `windows-latest`)
