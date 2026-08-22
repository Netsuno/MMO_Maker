# Phase 5 — TEST RESULTS (corrections after temporary rejection)

## Commit range

- Prior rejected-but-CI-green tip: `baaf79c846f1151f7e7a5f544812756635f1fcfd`
- Corrections start after that commit on `cursor/phase0-baseline-audit-02c7`
- Head (this gate): `5774b4ffc3b69ba08a15b59ec5b0329c09c5ba28`

## Suites (local pre-push)

| Suite | Passed | Failed | Total |
| --- | ---: | ---: | ---: |
| Frog.Tests | 165 | 0 | 165 |
| Frog.Persistence.IntegrationTests | 18 | 0 | 18 |
| Frog.Editor.WindowsSmokeTests | (CI Windows) | — | 9 × 3 expected |

Previous Phase 0–4 suites remain included; counts rose with Phase 5 correction tests.

### Unit / E2E / protocol (`Frog.Tests`) — correction coverage

- Child-process secret isolation: `PlaytestChildEnvironmentTests` (server + client role probes)
- Brand-new unsaved map prepare: `Prepare_BrandNewUnsavedMap_*`
- Warp BFS: A→B→C, A↔B cycle, shared target, unpublished transitive fail
- Spawn validator: valid / blocked / OOB / 1×1 / edge corners
- Real OS process: `PlaytestRealProcessLifecycleTests` (dotnet Frog.Server.dll, Hello readiness, owned kill, port closed, temp deleted, no secret echo)
- TCP framing loopback on `ClientSession`: fragmented length/payload, max frame, oversized, zero/negative, truncated, cancel, timeout, protocol-version mismatch Hello, int.Max length quick reject
- E2E TCP-only: move success, block Error, two consecutive warps A→B→C, MapRequest after each warp, clean disconnect + port closed (**no MovementService calls**)

### PostgreSQL

- Prior: `ServerPlaytestPipeline_LoadsPublishedSnapshot_NotNewerDraft`
- New: `Playtest_BrandNewUnsavedMap_SavesPublishesAndLoadsSnapshot`

### Windows smoke ×3

Phase 4 suite + playtest error/cancel; spawn override hook for non-modal smoke. Validated on CI Windows runners.

## Visual / manual evidence

| Item | Status |
| --- | --- |
| Interactive WPF screenshots of editor launch / client / movement / stop | **NOT RUN** |
| Why | Cloud agent is Linux; no Windows desktop session. PNGs in this folder are **schematic mockups**, not validated WPF screenshots. |
| Automated process proof | `PlaytestRealProcessLifecycleTests` + E2E in-process host + Windows smoke (CI) |

## Process lifecycle

- Async stdout/stderr drain in `EditorPlaytestProcessLauncher`
- Hello readiness on `127.0.0.1` (not bare port)
- Kill **owned** PIDs only (no `GetProcessById` fallback)
- Await `WaitForExitAsync` on stop; FormClosed awaits `StopPlaytestAsync`
- Temp workdir deleted on orchestrator stop/failure
- Bind forced `127.0.0.1` via `FROG_PLAYTEST_BIND_ADDRESS`

## Confirmations

- Working tree clean after docs commit
- Phase 6 **not** started
