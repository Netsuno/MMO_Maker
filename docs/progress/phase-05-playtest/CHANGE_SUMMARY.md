# Phase 5 — CHANGE SUMMARY (second rejection corrections)

## Application

- `PlaytestOwnedProcessLauncher` — production process manager (sanitize, drain logs, Hello readiness, client READY wait, owned-only kill)
- `PlaytestWorkspacePaths` — canonical owned workspace root + marker; safe delete only
- `PlaytestAuthToken` — ephemeral loopback token (never logged)
- `PlaytestLogSanitizer` — removes full secret values (not name-only mangling)
- Preparer always creates owned workspace; generates auth token on plan
- Orchestrator cleanup uses owned-workspace delete only

## Server

- Playtest token login (`__frog_playtest__` + env token) when playtest enabled
- `PlaytestRuntimeOptions.AuthToken` from env

## Client

- `--playtest-token` / env token; auto-connect + token login + map load
- Stdout `FROG_PLAYTEST_READY` after auth+map; correlated Console logs (token redacted)
- Never logs the token

## Editor

- `EditorPlaytestProcessLauncher` thin wrapper over `PlaytestOwnedProcessLauncher`
- WPF `OnMainWindowClosing` / Quit: cancel close while playtest active/busy/owned; await `StopPlaytestAsync`; then dirty prompt; then close
- `FormClosed` stop is fallback only
- Success UI only after orchestrator returns Success (client ready)

## Tests

- Production launcher/orchestrator integration (Frog.Tests)
- Safe workspace sentinel + secret redaction
- WPF playtest close smokes + `FrogGameClient` protocol-version rejection (Windows)
