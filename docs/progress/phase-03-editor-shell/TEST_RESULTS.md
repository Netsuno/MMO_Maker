# Phase 03 — résultats de tests

## Environnement

- Ubuntu 24.04, .NET SDK 8.0.424, PostgreSQL 16.15
- Date : 2026-08-22 04:42 UTC

## Commandes

```bash
dotnet build Frog.Creator.sln -c Release
dotnet test Frog.Tests/Frog.Tests.csproj -c Release --no-build
export FROG_POSTGRES_TEST_CONNECTION_STRING='Host=127.0.0.1;Port=5432;Database=frog_test;Username=frog_test;Password=frog_test_local_only'
dotnet test tests/Frog.Persistence.IntegrationTests/Frog.Persistence.IntegrationTests.csproj -c Release --no-build
```

## Résultats

| Suite | Passed | Failed | Skipped |
| --- | ---: | ---: | ---: |
| Frog.Tests | 109 | 0 | 0 |
| Frog.Persistence.IntegrationTests | 7 | 0 | 0 |

## Non exécuté

- Smoke UI Windows (`Frog.Editor` WinExe) — agent Linux
- E2E client-serveur
