# État du projet FRoG

- Dernière mise à jour : 2026-08-22 04:08 UTC
- Branche / commit : `cursor/phase0-baseline-audit-02c7`
- Phase active : **Phase 4 verte** — prochaine : brancher l’éditeur / CLI d’import
- Environnement : Ubuntu 24.04, .NET SDK 8.0.424, PostgreSQL 16.15

## Vérifié comme fonctionnel

- Suite unitaire `Frog.Tests` : **104** pass
- PostgreSQL : base isolée par migrations, santé, round-trip carte (accents UTF-8), conflit de révision, rollback, import idempotent : **6/6**
- `Frog.Legacy.LegacyFccMapReader` + fixtures `.fcc`
- Frontières d’architecture (Core / Application / Persistence)

## Implémenté, mais non vérifié

- Éditeur / client WinForms ; E2E TCP
- MariaDB héritée (plus source de vérité — ADR-0002)

## En cours

- Aucune

## Bloqueurs

- WinForms non exécutable sur cet agent Linux

## Commandes de validation

```bash
dotnet restore Frog.Creator.sln
dotnet build Frog.Creator.sln -c Release --no-restore
dotnet test Frog.Tests/Frog.Tests.csproj -c Release --no-build
export FROG_POSTGRES_TEST_CONNECTION_STRING='Host=127.0.0.1;Port=5432;Database=frog_test;Username=frog_test;Password=frog_test_local_only'
dotnet test tests/Frog.Persistence.IntegrationTests/Frog.Persistence.IntegrationTests.csproj -c Release
```

## Résultat de la dernière validation

- Build : **PASS**
- Tests unitaires : **104 réussis, 0 échoués, 0 ignorés**
- Intégration PostgreSQL : **PASS (6)**
- E2E : **NOT RUN**

## Prochaine tâche

- CLI `Frog.LegacyImporter` (`inspect` / `validate` / `import` / `report`) branché sur `IMapRepository`, ou ouverture d’une carte importée dans l’éditeur via ports applicatifs.

## Référence

- ADR-0002, `docs/DATA_MODEL.md`, `docs/TESTING.md`
