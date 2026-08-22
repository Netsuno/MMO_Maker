# Tests — FRoG Creator

## Commande unique (build + tests)

Sur **Windows** (CI et postes de développement) :

```bash
dotnet restore Frog.Creator.sln
dotnet build Frog.Creator.sln -c Release --no-restore
dotnet test Frog.Tests/Frog.Tests.csproj -c Release --no-build
```

Sur **Linux** (agents Cloud) : `Directory.Build.props` active déjà `EnableWindowsTargeting` pour restaurer/compiler `net8.0-windows`. Les applications WinForms ne s’exécutent pas ici ; seuls build et tests unitaires sont attendus.

```bash
dotnet restore Frog.Creator.sln
dotnet build Frog.Creator.sln -c Release --no-restore
dotnet test Frog.Tests/Frog.Tests.csproj -c Release --no-build
```

Langage : **C# 12** (`LangVersion` 12.0). Ne pas utiliser `LangVersion=preview` pour masquer des `Span` dans des méthodes async.

## Intégration MariaDB (optionnelle)

```bash
export MARIADB_TEST_CONNECTION_STRING='Server=...;Port=3306;Database=...;User Id=...;Password=...'
dotnet test Frog.Tests/Frog.Tests.csproj -c Release --filter Category=MariaDb
```

Sans cette variable, `MariaDbSchemaIntegrationTests` se termine sans assertion (no-op).

## PostgreSQL

Non configuré dans ce dépôt (écart PRD — voir `docs/BASELINE_AUDIT.md`).

## Niveaux

| Niveau | Emplacement | État |
| --- | --- | --- |
| Unitaire | `Frog.Tests` | Actif |
| Intégration DB | Trait `MariaDb` | Env-gated |
| UI / E2E | — | Absent |
