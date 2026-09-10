# Tests — MMO Maker

## Unitaires (toujours)

```bash
dotnet restore Frog.Creator.sln
dotnet build Frog.Creator.sln -c Release --no-restore
dotnet test Frog.Tests/Frog.Tests.csproj -c Release --no-build
```

C# 12 (`LangVersion` 12.0). Linux : `EnableWindowsTargeting` déjà dans `Directory.Build.props`.

## PostgreSQL (obligatoire pour toute tranche données / CI)

```bash
docker compose up -d postgres
# ou instance locale
export FROG_POSTGRES_TEST_CONNECTION_STRING='Host=127.0.0.1;Port=5432;Database=frog_test;Username=frog_test;Password=frog_test_local_only'
dotnet test tests/Frog.Persistence.IntegrationTests/Frog.Persistence.IntegrationTests.csproj -c Release
```

CI : job `postgres-integration` (Ubuntu + Postgres 16). Identifiants = développement uniquement.

## MariaDB (héritage, non bloquant)

```bash
export MARIADB_TEST_CONNECTION_STRING='...'
dotnet test Frog.Tests/Frog.Tests.csproj -c Release --filter Category=MariaDb
```

## Legacy `.fcc` (expérimental)

Les tests `LegacyFcc*` restent dans `Frog.Tests` pour non-régression du code différé.  
**Ils ne valident pas** une exigence produit (ADR-0003).

## UI / E2E

Smoke Windows requis à partir de la Phase 3 (shell éditeur). Non exécuté sur agents Linux.
