# Phase 5 — TEST RESULTS

## Commit range

- After Phase 4 accepted: `22d19b4570eaf552e5ce162243a83020ce86e2eb`
- Green head: `e2a1c0c179d5c2189ec2ef58d7dd945856c7678d`
- CI: https://github.com/Netsuno/MMO_Maker/actions/runs/32590970105

## Suites (CI)

| Suite | Passed | Failed | Total |
| --- | ---: | ---: | ---: |
| Frog.Tests | 145 | 0 | 145 |
| Frog.Persistence.IntegrationTests | 17 | 0 | 17 |
| Frog.Editor.WindowsSmokeTests | 9 | 0 | 9 × 3 consecutive |

### Unit / E2E / protocol (`Frog.Tests`)

Includes preparer, orchestrator (failure/cancel/timeout/stop), manifest/protocol, architecture Server↛Persistence, non-UI E2E playtest host.

### PostgreSQL

Includes `ServerPlaytestPipeline_LoadsPublishedSnapshot_NotNewerDraft` — published blocks remain after newer draft clears them; `MapService` fingerprint = published revision.

### Windows smoke ×3

Phase 4 suite (7) + playtest error/cancel (2) = 9; three consecutive passes OK.

## Correlated logs (sample)

```
[<correlation>] Playtest préparé MapId=<guid> rev=<n>
[<correlation>] Démarrage serveur port=<p>
[<correlation>] Serveur PID=<pid>
[<correlation>] Démarrage client 127.0.0.1:<p>
[<correlation>] Client PID=<pid>
```

Server playtest scopes: `PlaytestCorrelationId`, `PlaytestMapId`, `PlaytestPublishedRevision`.

## Draft-not-loaded proof

- Unit: draft rename after publish; published snapshot unchanged; manifest pins revision.
- PG: draft clears blocks; preparer + `MapService.IsBlocked` still true from published.
- E2E: draft diverges; live MapService still has published blocks + warp to runtime map 2.

## Process shutdown

- Orchestrator clears session; stops client then server.
- E2E: port closed after host stop (no orphan listener).
- Smoke cancel: fake launcher records cleanup on cancelled server start wait.
- Editor FormClosed / Stop Playtest cleanup.

## Screenshots

- `playtest-launch.png` — playtest launch schematic
- `playtest-client-running.png` — client session schematic  

(Real WPF capture deferred; Linux agent — schematics committed for gate evidence.)

## Not executed / deferred

| Item | Why |
| --- | --- |
| Live interactive WPF screenshot of Frog.Client window | No Windows desktop session in cloud agent; CI smoke covers command/error/cancel |

## Confirmations

- `git status` clean on gate docs commit  
- Phase 6 not started  
