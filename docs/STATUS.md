# État du projet — MMO Maker

- Dernière mise à jour : 2026-08-22
- Branche : `cursor/phase0-baseline-audit-02c7`
- Head : `e507a0481dd29c36524be4c854a458c84c70439c`
- **Phase 4 : ACCEPTED**
  - Commit accepté : `22d19b4570eaf552e5ce162243a83020ce86e2eb`
  - CI acceptée : https://github.com/Netsuno/MMO_Maker/actions/runs/32585827562
- **Phase 5 : GATE REACHED — WAITING FOR REVIEW**
  - Commit vert : `e507a0481dd29c36524be4c854a458c84c70439c`
  - CI : https://github.com/Netsuno/MMO_Maker/actions/runs/32590792046
- Phase 6 : non commencée

## Vérifié Phase 5

| Suite | Passed | Failed | Total |
| --- | ---: | ---: | ---: |
| Frog.Tests | 145 | 0 | 145 |
| Frog.Persistence.IntegrationTests | 17 | 0 | 17 |
| Frog.Editor.WindowsSmokeTests | 9×3 | 0 | 9×3 |

Preuves : `docs/progress/phase-05-playtest/`

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
