# État du projet — MMO Maker

- Dernière mise à jour : 2026-08-22
- Branche / commit : `cursor/phase0-baseline-audit-02c7` / `3353b56`
- **Phase 3 — Shell éditeur : READY FOR REVIEW**
- Prochaine phase **non commencée** : Phase 4 — Map Editor MVP
- Rapport : [`docs/progress/phase-03-editor-shell/REVIEW_REQUEST.md`](progress/phase-03-editor-shell/REVIEW_REQUEST.md)
- Environnement audit : Ubuntu 24.04, .NET 8.0.424, PostgreSQL 16.15

## Vérifié comme fonctionnel

- Build C# 12 + tests unitaires `Frog.Tests` (109)
- Intégration PostgreSQL (7) dont `ListSummaries`
- `MapWorkspaceSession` + catalogue via `IMapRepository`
- Shell : menu, barre d’outils, arbre monde, canvas, tilesets/couches/propriétés, statut
- Architecture : Editor → Application/Persistence ; pas de DB dans Forms

## Implémenté, mais non vérifié

- Smoke UI Windows de l’éditeur (agent Linux) — **gate Phase 3 partiel**
- Client WinForms ; E2E TCP
- Runtime MariaDB optionnel (héritage)

## Différé (hors chemin critique)

- `Frog.Legacy` / fixtures `.fcc` (expérimental)

## Bloqueurs

- Smoke UI Windows requis pour clôturer pleinement le gate Phase 3 (« UI réactive »)

## Commandes de validation

```bash
dotnet restore Frog.Creator.sln && dotnet build Frog.Creator.sln -c Release --no-restore
dotnet test Frog.Tests/Frog.Tests.csproj -c Release --no-build
export FROG_POSTGRES_TEST_CONNECTION_STRING='Host=127.0.0.1;Port=5432;Database=frog_test;Username=frog_test;Password=frog_test_local_only'
dotnet test tests/Frog.Persistence.IntegrationTests/Frog.Persistence.IntegrationTests.csproj -c Release
```

## Prochaine phase proposée (non commencée)

- Phase 4 : Map Editor MVP (peinture, collision, warp, undo, save/publish PG)
