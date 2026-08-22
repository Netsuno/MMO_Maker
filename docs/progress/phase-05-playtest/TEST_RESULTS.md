# Phase 5 — TEST RESULTS (second rejection corrections)

## Commit range

- Prior rejected tip: `f9d88b4827fc89c9e4ab63bd8c941bbb823d662b`
- Implementation commit: 62b27c542bab33896b73e00889da4e0a29211fad
- Gate HEAD: 62b27c542bab33896b73e00889da4e0a29211fad
- CI: _pending_

## Suites (local pre-push)

| Suite | Passed | Failed | Total |
| --- | ---: | ---: | ---: |
| Frog.Tests | 168 | 0 | 168 |
| Frog.Persistence.IntegrationTests | 18 | 0 | 18 |
| Frog.Editor.WindowsSmokeTests | CI | — | expected ≥11 × 3 |

### Production launcher / orchestrator (`PlaytestProductionLauncherTests`)

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
