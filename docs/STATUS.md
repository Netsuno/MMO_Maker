# État du projet — MMO Maker

- Dernière mise à jour : 2026-08-22
- Branche : `cursor/phase0-baseline-audit-02c7`
- Gate Phase 3 accepté : `20eedc1`
- Plage revue Phase 4 : `20eedc1..HEAD`
- **Phase 4 — gate data safety : READY FOR REVIEW**

## Vérifié comme fonctionnel (head courant)

- Build Release `Frog.Creator.sln`
- **129** tests unitaires `Frog.Tests`
- **16** tests intégration PostgreSQL (`Frog.Persistence.IntegrationTests`) — **100 % vert**
- **7** smoke Windows (`Frog.Editor.WindowsSmokeTests`) — open/save, fermeture dirty, undo canvas
- Contrat création : `MapId = null` → id généré ; mise à jour si existant
- Séparation brouillon/publication, concurrence atomique, prompts dirty

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
FROG_EDITOR_FORCE_IN_MEMORY=1 dotnet test tests/Frog.Editor.WindowsSmokeTests/Frog.Editor.WindowsSmokeTests.csproj -c Release --no-build
```

## Prochaine phase proposée (non commencée)

Phase 5 — Playtest depuis l’éditeur (carte publiée PostgreSQL → serveur/client).
