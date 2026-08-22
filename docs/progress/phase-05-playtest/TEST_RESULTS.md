# Phase 5 — TEST RESULTS (second rejection corrections)

## Commit range

- Prior rejected tip: `f9d88b4827fc89c9e4ab63bd8c941bbb823d662b`
- Implementation commit: `62b27c542bab33896b73e00889da4e0a29211fad`
- CI-evidence tip: `2665e040937045155c887c62affdbfea98aa7153`
- CI: https://github.com/Netsuno/MMO_Maker/actions/runs/32600611710 (success)

## Suites (CI on `2665e04`)

| Suite | Passed | Failed | Total |
| --- | ---: | ---: | ---: |
| Frog.Tests | 168 | 0 | 168 |
| Frog.Persistence.IntegrationTests | 18 | 0 | 18 |
| Frog.Editor.WindowsSmokeTests | 12 | 0 | 12 × 3 consecutive |

### Production launcher / orchestrator (`PlaytestProductionLauncherTests`)

- Headless READY client: committed `tests/Frog.PlaytestHeadlessClient` (resolved from build output; CI-safe on Windows)
- `OwnedLauncher_Orchestrator_ServerClientReady_Stop_NoOrphan_SafeCleanup` — real `PlaytestOwnedProcessLauncher` + `PlaytestOrchestrator`, real Frog.Server, READY client, correlation logs, secret absent, owned temp deleted, external sentinel preserved, port closed
- `OwnedLauncher_ClientEarlyExit_FailsWithActionableLogs`
- `WorkspaceCleanup_RejectsExternalSentinelDirectory`
- `LogSanitizer_RemovesFullSecretValues`

### WPF coordinated shutdown (Windows smoke)

- `Close_ActivePlaytest_CleanMap_AwaitsStop_ThenCloses`
- `Close_ActivePlaytest_DirtyMap_CancelKeepsOpen_AndPlaytestStillStoppable`

### Protocol version (Windows smoke)

- `FrogGameClient_RejectsIncompatibleHello_DoesNotAuthenticate`

### Visual

| Item | Status |
| --- | --- |
| Graphical WPF screenshots | **NOT RUN** |

## Confirmations

- Phase 6 not started
- FormClosed is fallback only; Closing/Quit await `StopPlaytestAsync`
