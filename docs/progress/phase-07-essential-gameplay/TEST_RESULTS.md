# Phase 7 — Test Results

## Environment

| Item | Value |
| --- | --- |
| OS | Linux (local agent) / Windows (CI smoke) |
| .NET SDK | 8.0.x |
| PostgreSQL | Via `FROG_POSTGRES_TEST_CONNECTION_STRING` (CI job + local when set) |
| Commit | tip of `cursor/phase0-baseline-audit-02c7` |

## Commands

```bash
dotnet build -c Release
dotnet test Frog.Tests/Frog.Tests.csproj -c Release
dotnet test tests/Frog.Persistence.IntegrationTests/Frog.Persistence.IntegrationTests.csproj -c Release
# Windows CI only:
dotnet test tests/Frog.Editor.WindowsSmokeTests/Frog.Editor.WindowsSmokeTests.csproj -c Release
```

## Results (local verification 2026-08-26)

| Suite | Status | Passed | Failed | Skipped |
| --- | --- | ---: | ---: | ---: |
| Frog.Tests (unit + protocol + E2E headless) | PASS | 270 | 0 | 0 |
| PostgreSQL integration | PASS | 39 | 0 | 0 |
| Windows smoke ×3 | CI | — | — | — |
| Architecture (in Frog.Tests) | PASS | included | 0 | 0 |

### Phase 7 test classes

- `Phase7AuthTests` (10)
- `Phase7CharacterTests` (3)
- `Phase7InventoryTests` (3)
- `Phase7CombatTests` (3)
- `Phase7ShopBankTests` (2)
- `Phase7ProgressionTests` (3)
- `Phase7E2EGameplayTests` (2) — full TCP E2E + concurrent pickup

### NOT RUN locally

- Windows smoke UI — reported from GitHub Actions only (NOT RUN on Linux agent).
