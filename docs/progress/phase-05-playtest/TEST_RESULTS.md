# Phase 5 — TEST RESULTS (fifth rejection corrections)

## Commit range

- Prior rejected tip: `b6991aa695da5b14690bd46696c533692dea56ce`
- Implementation tip: `d33f9924e5ff4ee613475200bfb1a6dd88f0d65e`
- Range: `b6991aa..HEAD`
- CI: https://github.com/Netsuno/MMO_Maker/actions/runs/32654683068 (success on `d33f992`)

## Suites (CI on gate HEAD)

| Suite | Passed | Failed | Total |
| --- | ---: | ---: | ---: |
| Frog.Tests | 197 | 0 | 197 |
| Frog.Persistence.IntegrationTests | 18 | 0 | 18 |
| Frog.Editor.WindowsSmokeTests | 13 | 0 | 13 × 3 consecutive |

### New / updated token-security tests

| Test | Result |
| --- | --- |
| `Tcp_ReservedRegistration_Rejected_AllCasings` (`__frog_playtest__`) | PASS |
| `Tcp_ReservedRegistration_Rejected_AllCasings` (`__FROG_PLAYTEST__`) | PASS |
| `Tcp_ReservedRegistration_Rejected_AllCasings` (`__FRoG_PlayTest__`) | PASS |
| `Tcp_MixedCaseSeededAccount_CannotReuseTokenAfterConsume` | PASS |
| `Tcp_AbortAfterPositiveLoginResult_TokenRemainsConsumed` | PASS |
| `Tcp_InjectedFailureAfterLoginResult_TokenRemainsConsumed` | PASS |
| `Tcp_SessionCreationFailure_DoesNotConsumeToken` | PASS |
| `Tcp_ConcurrentAuth_ExactlyOneSuccess` | PASS |
| `Tcp_TokenNeverAppearsInLoginFailureMessage` | PASS |
| `IsReservedUsername_Matches_AllCasings` (×3) | PASS |
| `IsReservedUsername_Rejects_Unrelated` | PASS |

### Visual

| Item | Status |
| --- | --- |
| Graphical WPF screenshots | **NOT RUN** |

## Confirmations

- Phase 6 not started
