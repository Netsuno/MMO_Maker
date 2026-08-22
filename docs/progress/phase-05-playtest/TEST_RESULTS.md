# Phase 5 — TEST RESULTS (third rejection corrections)

## Commit range

- Prior rejected tip: `6dcf0b301c9b3f06aa3b118a49e10d832719b0fc`
- Implementation / CI: see `docs/STATUS.md`

## Suites (local pre-push)

| Suite | Passed | Failed | Total |
| --- | ---: | ---: | ---: |
| Frog.Tests | 180 | 0 | 180 |
| Frog.Persistence.IntegrationTests | 18 | 0 | 18 (CI) |
| Frog.Editor.WindowsSmokeTests | CI | — | expected ≥13 × 3 |

### READY / spawn

- Spawn `(1,1)` → authoritative pixels `(48,48)` via `WorldMetrics.TileCenterToPixels`
- Wrong map / wrong tile / malformed / correlation-only markers rejected

### Frog.Client success (Windows smoke)

- `ClientPlaytestSuccessSmokeTests.FrogClient_PlaytestAutoStart_ExactSpawn_Ready_CleanShutdown`
- Real server + `Frog.Client.exe` + production launcher/orchestrator

### Token

- Args never contain token
- First auth succeeds; reuse after disconnect fails (`PlaytestTokenReuseTests`)

### Lifecycle / isolation / workspace

- Early-exit: started PID, `code=7`, `early-exit-before-ready`
- Stop failure retains ownership; workspace not deleted
- Forbidden parent env sanitized; children start without forbidden names
- Invalid supplied WorkDirectory leaves no owned dir for that correlation

### Visual

| Item | Status |
| --- | --- |
| Graphical WPF screenshots | **NOT RUN** |

## Confirmations

- Phase 6 not started
