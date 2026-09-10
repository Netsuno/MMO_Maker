# Phase 03 — résultats de tests

## Environnement local (agent Linux)

- Date : 2026-08-22
- SDK : 8.0.424
- PostgreSQL : 16.15

## Commandes

```bash
dotnet build Frog.Creator.sln -c Release
dotnet test Frog.Tests/Frog.Tests.csproj -c Release --no-build
export FROG_POSTGRES_TEST_CONNECTION_STRING='Host=127.0.0.1;Port=5432;Database=frog_test;Username=frog_test;Password=frog_test_local_only'
dotnet test tests/Frog.Persistence.IntegrationTests/Frog.Persistence.IntegrationTests.csproj -c Release --no-build
```

## Résultats locaux

| Suite | Passed | Failed | Total |
| --- | ---: | ---: | ---: |
| Frog.Tests | 110 | 0 | 110 |
| Frog.Persistence.IntegrationTests | 7 | 0 | 7 |

## Smoke Windows (CI)

Projet : `tests/Frog.Editor.WindowsSmokeTests`  
Job : `.github/workflows/ci.yml` → `build-and-test` (windows-latest)  
Env : `FROG_EDITOR_FORCE_IN_MEMORY=1`

```powershell
$env:FROG_EDITOR_FORCE_IN_MEMORY = "1"
dotnet test tests/Frog.Editor.WindowsSmokeTests/Frog.Editor.WindowsSmokeTests.csproj -c Release --no-build -v n
```

**Lien run GitHub Actions :** https://github.com/Netsuno/MMO_Maker/actions/runs/32575250906

| Test | Résultat CI |
| --- | --- |
| `MainWindow_OpensDemoMap_WithInMemoryRepository` | **PASS** (1/1, ~5 s) |

Assertions couvertes : fenêtre sans exception, zones gauche/centre/droite, catalogue démo, carte assignée au canvas (20×15, ≥3 couches), dispatcher réactif, fermeture propre, timeout 45 s.

Secours manuel : `scripts/windows-editor-smoke.ps1`

## Migrations PostgreSQL

- `20260822040506_InitialMapPersistence`
- `20260822130000_ModernMapIdentity` (retrait `legacy_id`, FK `target_map_id`)

## Non exécuté localement

- Smoke UI Windows (Linux — validé sur runner `windows-latest` CI)
