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
| Frog.Tests | 114 | 0 | 114 |
| Frog.Persistence.IntegrationTests | 10 | 0 | 10 |

## Smoke Windows (CI)

| Test | Attendu |
| --- | --- |
| `MainWindow_OpensDemoMap_WithInMemoryRepository` | PASS |
| `MainWindow_SaveDraft_InMemoryRepository` | PASS |

**Lien run GitHub Actions :** *(complété après push — PR #2)*

## Nouveaux tests Phase 4

- `MapWorkspaceSessionTests` : save draft, publish, conflict, validation warp
- `PostgresMapRepositoryTests.Save_PublishedStatus_PersistsInDatabase`
- `PostgresMapRepositoryTests.Save_SecondUpdate_*`
- Smoke : enregistrement brouillon mémoire

## Non exécuté localement

- Smoke Windows (Linux — CI `windows-latest`)
