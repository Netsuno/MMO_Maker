# État du projet — MMO Maker

- Dernière mise à jour : 2026-08-22
- Branche : `cursor/phase0-baseline-audit-02c7`
- Commit d’implémentation Phase 3 (immuable) : `3fc6530`
- Plage revue Phase 4 : `3fc6530..HEAD`
- **Phase 4 — Map Editor MVP : READY FOR REVIEW**
- Rapport : [`docs/progress/phase-04-map-editor-mvp/REVIEW_REQUEST.md`](progress/phase-04-map-editor-mvp/REVIEW_REQUEST.md)

## Vérifié comme fonctionnel

- Build C# 12 + **114** tests unitaires `Frog.Tests`
- Intégration PostgreSQL (**10**) incl. publication et second save
- Smoke UI Windows (**2**) : ouverture shell + save brouillon mémoire
- Éditeur : peinture, collision, warp, undo/redo, save/publish PostgreSQL

## Différé (hors chemin critique)

- Playtest serveur depuis carte PG (Phase 5)
- `Frog.Legacy` / MariaDB héritage (gelé)

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

Phase 5 — Playtest depuis l’éditeur (carte publiée PostgreSQL → serveur/client).
