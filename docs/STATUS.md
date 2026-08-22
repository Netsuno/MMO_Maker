# État du projet — MMO Maker

- Dernière mise à jour : 2026-08-22
- Branche : `cursor/phase0-baseline-audit-02c7`
- Commit d’implémentation Phase 3 (immuable) : `3fc6530`
- Plage revue gate Phase 3 : `3fc6530..HEAD`
- **Phase 3 — Shell éditeur : READY FOR REVIEW (corrections gate)**
- Prochaine phase **non commencée** : Phase 4 — Map Editor MVP
- Rapport : [`docs/progress/phase-03-editor-shell/REVIEW_REQUEST.md`](progress/phase-03-editor-shell/REVIEW_REQUEST.md)

## Vérifié comme fonctionnel

- Build C# 12 + **110** tests unitaires `Frog.Tests`
- Intégration PostgreSQL (**7**) incl. migration `ModernMapIdentity`
- Identité carte moderne : `MapId` (Guid), `LoadByIdAsync`, warps `TargetMapId` + FK
- Smoke UI Windows automatisé : `tests/Frog.Editor.WindowsSmokeTests` (job CI Windows)
- Shell éditeur + catalogue via `MapWorkspaceSession` / `IMapRepository`

## Implémenté, mais non vérifié localement (agent Linux)

- Exécution manuelle du smoke Windows (automatisé en CI)

## Différé (hors chemin critique)

- `Frog.Legacy` / fixtures `.fcc` (expérimental ; warps int → Guid runtime helper)

## Commandes de validation

```bash
dotnet restore Frog.Creator.sln && dotnet build Frog.Creator.sln -c Release --no-restore
dotnet test Frog.Tests/Frog.Tests.csproj -c Release --no-build
export FROG_POSTGRES_TEST_CONNECTION_STRING='Host=127.0.0.1;Port=5432;Database=frog_test;Username=frog_test;Password=frog_test_local_only'
dotnet test tests/Frog.Persistence.IntegrationTests/Frog.Persistence.IntegrationTests.csproj -c Release --no-build
# Windows uniquement :
export FROG_EDITOR_FORCE_IN_MEMORY=1
dotnet test tests/Frog.Editor.WindowsSmokeTests/Frog.Editor.WindowsSmokeTests.csproj -c Release --no-build
```

## Prochaine phase proposée (non commencée)

- Phase 4 : Map Editor MVP (peinture, collision, warp, undo, save/publish PG)
