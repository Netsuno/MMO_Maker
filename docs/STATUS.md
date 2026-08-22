# État du projet — MMO Maker

- Dernière mise à jour : 2026-08-22
- Branche : `cursor/phase0-baseline-audit-02c7`
- Commit de preuve (suites) : `af5ed14c628b51570d03bed47f75049fa88aaed9`
- Gate Phase 3 accepté : `20eedc1`
- Plage revue Phase 4 : `20eedc1..HEAD`
- **Phase 4 — gate data safety : READY FOR REVIEW**
- CI (preuve) : https://github.com/Netsuno/MMO_Maker/actions/runs/32585645096 (exit 0)
- Smoke ×3 (workflow) : https://github.com/Netsuno/MMO_Maker/actions/runs/32585353371 (`bad59a4`, exit 0)

## Vérifié comme fonctionnel (head courant)

| Suite | Passed | Failed | Total | Host |
| --- | ---: | ---: | ---: | --- |
| Frog.Tests | 129 | 0 | 129 | Windows CI |
| Frog.Persistence.IntegrationTests | 16 | 0 | 16 | Ubuntu CI + PostgreSQL |
| Frog.Editor.WindowsSmokeTests | 7 | 0 | 7 | Windows CI ×3 consecutive |

- Hôte de test smoke : terminé normalement (pas de crash, exit 0)
- Smoke Windows exécuté **3 fois de suite** dans le même job CI (7/7 × 3)

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
