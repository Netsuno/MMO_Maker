# État du projet — MMO Maker

- Dernière mise à jour : 2026-08-22
- Branche : `cursor/phase0-baseline-audit-02c7`
- **Phase 4 : ACCEPTED**
  - Commit accepté : `22d19b4570eaf552e5ce162243a83020ce86e2eb`
  - CI acceptée : https://github.com/Netsuno/MMO_Maker/actions/runs/32585827562
- **Phase 5 : GATE REACHED — WAITING FOR REVIEW** (corrections after rejection of `baaf79c`)
  - Rejected tip : `baaf79c846f1151f7e7a5f544812756635f1fcfd`
  - Green corrections head : `af71e6f24c1650a2e945a667c2e8a022acc1b2cb`
  - Commit range : `baaf79c..af71e6f`
  - CI : https://github.com/Netsuno/MMO_Maker/actions/runs/32596037772
- Phase 6 : non commencée

## Vérifié Phase 5 (CI)

| Suite | Passed | Failed | Total |
| --- | ---: | ---: | ---: |
| Frog.Tests | 165 | 0 | 165 |
| Frog.Persistence.IntegrationTests | 18 | 0 | 18 |
| Frog.Editor.WindowsSmokeTests | 9×3 | 0 | 9×3 |

Preuves : `docs/progress/phase-05-playtest/`

## Différé

- Phase 6+
- Manual WPF screenshots (**NOT RUN** on Linux agent; schematics only)
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
