# Phase 5 — TEST RESULTS (third rejection corrections)

## Commit range

- Prior rejected tip: `6dcf0b301c9b3f06aa3b118a49e10d832719b0fc`
- Implementation tip: `2c5719038019e4e56c3e484d543cbf27e84777d2`
- CI-evidence tip: `c310d02b30cac9e184a822e74837736ed1670482`
- CI: https://github.com/Netsuno/MMO_Maker/actions/runs/32604665896 (success)

## Suites (CI on `c310d02`)

| Suite | Passed | Failed | Total |
| --- | ---: | ---: | ---: |
| Frog.Tests | 180 | 0 | 180 |
| Frog.Persistence.IntegrationTests | 18 | 0 | 18 |
| Frog.Editor.WindowsSmokeTests | 13 | 0 | 13 × 3 consecutive |

### READY / spawn

- Spawn `(1,1)` → authoritative pixels `(48,48)` via `WorldMetrics.TileCenterToPixels`
- Wrong map / wrong tile / malformed / correlation-only markers rejected
- Production launcher parses READY (no bare `Contains`)

### Frog.Client success (Windows smoke)

- `ClientPlaytestSuccessSmokeTests.FrogClient_PlaytestAutoStart_ExactSpawn_Ready_CleanShutdown` — **PASS** ×3
- Real `Frog.Server` + real `Frog.Client` + production orchestrator

### Token

- Command-line args never contain token
- First auth succeeds; reuse after disconnect fails

### Lifecycle / isolation / workspace

- Early-exit: started PID, `code=7`, `early-exit-before-ready`
- Stop failure retains ownership; workspace not deleted while owned
- Forbidden parent env sanitized; children start without forbidden names
- Invalid supplied WorkDirectory leaves no owned dir for that correlation

### Visual

| Item | Status |
| --- | --- |
| Graphical WPF screenshots | **NOT RUN** |

## Confirmations

- Phase 6 not started
