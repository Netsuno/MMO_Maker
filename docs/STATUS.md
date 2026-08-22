# État du projet — MMO Maker

- Dernière mise à jour : 2026-08-22
- Branche : `cursor/phase0-baseline-audit-02c7`
- Head : `22d19b4570eaf552e5ce162243a83020ce86e2eb`
- **Phase 4 : ACCEPTED**
  - Commit accepté : `22d19b4570eaf552e5ce162243a83020ce86e2eb`
  - CI acceptée : https://github.com/Netsuno/MMO_Maker/actions/runs/32585827562
- **Phase 5 : IN PROGRESS** — Client/server playtest
- Phase 6 : non commencée

## Vérifié Phase 4 (immuable)

| Suite | Passed | Failed | Total |
| --- | ---: | ---: | ---: |
| Frog.Tests | 129 | 0 | 129 |
| Frog.Persistence.IntegrationTests | 16 | 0 | 16 |
| Frog.Editor.WindowsSmokeTests | 7×3 | 0 | 7×3 |

## En cours (Phase 5)

Playtest d’une carte explicitement publiée PostgreSQL : validate → save/publish → serveur local → client → mouvement / collision / warp autoritatifs → cleanup processus.

## Différé

- Phase 6+
- `Frog.Legacy` / nouvelles fonctions MariaDB (gelé)

## Commandes de validation

```bash
dotnet restore Frog.Creator.sln && dotnet build Frog.Creator.sln -c Release --no-restore
dotnet test Frog.Tests/Frog.Tests.csproj -c Release --no-build
export FROG_POSTGRES_TEST_CONNECTION_STRING='Host=127.0.0.1;Port=5432;Database=frog_test;Username=frog_test;Password=frog_test_local_only'
dotnet test tests/Frog.Persistence.IntegrationTests/Frog.Persistence.IntegrationTests.csproj -c Release --no-build
# Windows :
FROG_EDITOR_FORCE_IN_MEMORY=1 dotnet test tests/Frog.Editor.WindowsSmokeTests/Frog.Editor.WindowsSmokeTests.csproj -c Release --no-build
```
