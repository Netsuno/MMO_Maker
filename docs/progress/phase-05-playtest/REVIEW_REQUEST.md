# Demande de revue — Phase 5 Client/server playtest

## Contexte

Phase 4 acceptée sur `22d19b4`. Phase 5 livrée : playtest d’une carte **explicitement publiée** (PostgreSQL), serveur/client locaux, protocole uniquement, cleanup processus.

## Plage de commits

- Début (après Phase 4 acceptée) : `22d19b4570eaf552e5ce162243a83020ce86e2eb`
- Head vert : `e2a1c0c179d5c2189ec2ef58d7dd945856c7678d`

## CI verte

https://github.com/Netsuno/MMO_Maker/actions/runs/32590970105

| Suite | Passed | Failed | Total |
| --- | ---: | ---: | ---: |
| Frog.Tests (unit + protocol + E2E) | 145 | 0 | 145 |
| Frog.Persistence.IntegrationTests | 17 | 0 | 17 |
| Frog.Editor.WindowsSmokeTests | 9×3 | 0 | 9×3 |

## Preuves

- [`PHASE_REPORT.md`](PHASE_REPORT.md)
- [`TEST_RESULTS.md`](TEST_RESULTS.md)
- [`CHANGE_SUMMARY.md`](CHANGE_SUMMARY.md)
- [`KNOWN_ISSUES.md`](KNOWN_ISSUES.md)
- Screenshots : `playtest-launch.png`, `playtest-client-running.png`
- Draft non chargé : unit + PG `ServerPlaytestPipeline_LoadsPublishedSnapshot_NotNewerDraft` + E2E
- Shutdown propre : E2E port fermé après `StopAsync` ; smoke cancel cleanup
- `git status` clean après ce commit de docs
- Phase 6 **non commencée**

## Trois risques principaux

1. Kill process tree / `dotnet dll` portability under Windows shells  
2. Ephemeral port race under heavy CI  
3. Warp targets must be published or playtest preparation fails  

## Question de gate

**Accepter Phase 5** et autoriser Phase 6 ?
