# Tests — FRoG Creator

## Commande unique (build + tests unitaires)

```bash
dotnet restore Frog.Creator.sln
dotnet build Frog.Creator.sln -c Release --no-restore
dotnet test Frog.Tests/Frog.Tests.csproj -c Release --no-build
```

Sur Linux, `EnableWindowsTargeting` est déjà dans `Directory.Build.props`. Langage : **C# 12**.

## PostgreSQL (intégration)

Démarrer une instance (Docker ou équivalent local) :

```bash
docker compose up -d postgres
# ou PostgreSQL 16 local
export FROG_POSTGRES_TEST_CONNECTION_STRING='Host=127.0.0.1;Port=5432;Database=frog_test;Username=frog_test;Password=frog_test_local_only'
dotnet test tests/Frog.Persistence.IntegrationTests/Frog.Persistence.IntegrationTests.csproj -c Release
```

Chaque collection de tests crée une base `frog_it_*` via migrations, puis la détruit.  
Sans la variable d’environnement, les faits PostgreSQL sont ignorés (raison + date dans l’attribut).

Identifiants Compose (`frog` / `frog_dev_only`) et de test ci-dessus : **développement uniquement**.

## Intégration MariaDB (héritage, optionnelle)

```bash
export MARIADB_TEST_CONNECTION_STRING='Server=...;Port=3306;Database=...;User Id=...;Password=...'
dotnet test Frog.Tests/Frog.Tests.csproj -c Release --filter Category=MariaDb
```

## Niveaux

| Niveau | Emplacement | État |
| --- | --- | --- |
| Unitaire | `Frog.Tests` | Actif |
| Intégration PostgreSQL | `tests/Frog.Persistence.IntegrationTests` | Env-gated + job CI Ubuntu |
| Intégration MariaDB | Trait `MariaDb` | Env-gated, déprécié |
| UI / E2E | — | Absent |
