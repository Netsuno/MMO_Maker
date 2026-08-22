# Phase 5 — CHANGE SUMMARY (third rejection corrections)

## READY / spawn

- Strict `PlaytestReadyMarker` (correlation, runtime map, tileX/Y, pixelX/Y)
- Headless + Frog.Client emit authoritative PositionUpdate-derived coords (spawn 1,1 → pixels 48,48)
- `WaitForClientReadyAsync` parses/validates marker against `plan.Spawn` (rejects wrong map/spawn/malformed)

## Token

- Removed `--playtest-token` from process command line (env `FROG_PLAYTEST_AUTH_TOKEN` only)
- `PlaytestAuthTokenGate` consumes token atomically on first successful playtest auth; reuse fails
- Never logged

## Lifecycle

- Early-exit via headless `--exit-before-ready` (PID + exit code 7 + sanitized stderr)
- `StopAsync` retains ownership until termination confirmed; injectable `ForceStopWaitTimeout` seam
- Orchestrator defers workspace delete while owned processes remain

## Isolation / workspace

- Playtest children fail-fast if forbidden env **names** present (values never printed)
- Production path sanitizes then starts; integration injects forbidden names in parent
- `PlaytestMapPreparer` validates caller `WorkDirectory` before any create (no leak)

## Tests

- Unit: READY validate, token gate, workspace leak, production launcher spawn/env/early-exit/stop-failure, token reuse TCP
- Windows smoke: real `Frog.Server` + `Frog.Client.exe` + production orchestrator success path
