# Phase 5 — TEST RESULTS (fourth rejection corrections)

## Commit range

- Prior rejected tip: `ac3a71b2270812567c30f04395b58ad5438faabf`
- Range: `ac3a71b..HEAD`
- CI: see `docs/STATUS.md`

## Suites (CI on gate HEAD)

| Suite | Passed | Failed | Total |
| --- | ---: | ---: | ---: |
| Frog.Tests | 188 | 0 | 188 |
| Frog.Persistence.IntegrationTests | 18 | 0 | 18 |
| Frog.Editor.WindowsSmokeTests | 13 | 0 | 13 × 3 consecutive |

### New token tests (`PlaytestTokenReuseTests` + `PlaytestAuthTokenGateTests`)

| Test | Result |
| --- | --- |
| `Tcp_ReservedRegistration_Rejected` | PASS |
| `Tcp_FirstAuthSucceeds_ReuseAfterDisconnectFails` | PASS |
| `Tcp_NoNormalAuthFallback_AfterTokenConsumed_EvenIfAccountExists` | PASS |
| `Tcp_ConcurrentAuth_ExactlyOneSuccess` | PASS |
| `Tcp_SessionCreationFailure_DoesNotConsumeToken` | PASS |
| `Tcp_TokenNeverAppearsInLoginFailureMessage` | PASS |
| `TryClaim_Commit_FirstSucceeds_SecondFails` | PASS |
| `TryClaim_WrongToken_DoesNotClaim` | PASS |
| `ReleaseClaim_AfterFailedSession_TokenStillAvailable` | PASS |
| `Concurrent_OnlyOneClaimSucceeds` | PASS |

### New READY map tests (`PlaytestClientReadyStateTests`)

| Test | Result |
| --- | --- |
| `TryBuildReadyLine_Rejects_PositionAndLoadedMapMismatch` | PASS |
| `TryBuildReadyLine_Emits_WhenMapsMatch` | PASS |

### Frog.Client success (Windows smoke — preserved)

- `ClientPlaytestSuccessSmokeTests.FrogClient_PlaytestAutoStart_ExactSpawn_Ready_CleanShutdown` — **PASS** ×3
- Real `Frog.Server` + real `Frog.Client` + production orchestrator

### Visual

| Item | Status |
| --- | --- |
| Graphical WPF screenshots | **NOT RUN** |

## Confirmations

- Phase 6 not started
