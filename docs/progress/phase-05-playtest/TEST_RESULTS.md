# Phase 5 — TEST RESULTS

## Commit range

- After Phase 4 accepted: `22d19b4570eaf552e5ce162243a83020ce86e2eb`
- Green head (this gate): _filled after push/CI — see REVIEW_REQUEST.md_

## Suites

### Unit / E2E / protocol (`Frog.Tests`)

**145 / 145 PASS** (local Release)

Includes:

- Playtest preparer (dirty save, durable gate, spawn validation, draft≠published)
- Orchestrator (launch failure, cancel, timeout, stop cleanup)
- Manifest/protocol (schema version, invalid login/move payloads, Hello version, size contract)
- Architecture: Server → Application, **not** Persistence
- Non-UI E2E playtest host: startup → Hello → login → spawn → valid move → blocked move → warp → disconnect → port closed

### PostgreSQL integration

**17 / 17 PASS** (local)

New: `ServerPlaytestPipeline_LoadsPublishedSnapshot_NotNewerDraft` — published blocks remain after newer draft clears them; `MapService` fingerprint revision = published revision.

### Windows smoke (`Frog.Editor.WindowsSmokeTests`)

- Phase 4 suite retained (7) + playtest smoke (2) = **9** tests
- CI runs the suite **×3 consecutive** (unchanged workflow policy)
- Not executed on this Linux cloud agent (WPF STA host requires Windows runner)

## Correlated logs (sample)

```
[a1b2…f0] Playtest préparé MapId=<guid> rev=<n>
[a1b2…f0] Démarrage serveur port=<p>
[a1b2…f0] Serveur PID=<pid>
[a1b2…f0] Démarrage client 127.0.0.1:<p>
[a1b2…f0] Client PID=<pid>
```

Server scope fields when playtest enabled: `PlaytestCorrelationId`, `PlaytestMapId`, `PlaytestPublishedRevision`.

## Draft-not-loaded proof

- Unit: after publish, draft rename + save; published snapshot name unchanged; manifest pins published revision.
- PG: draft clears blocks; preparer + `MapService.IsBlocked(1,6,5)` still true from published snapshot.
- E2E: draft name `SHOULD_NOT_LOAD` / blocks cleared in draft; live `MapService` still reports published blocks + warp to runtime map 2.

## Process shutdown

- Orchestrator clears active session and stops client then server on failure/cancel/Stop.
- E2E: after `host.StopAsync()`, playtest TCP port is closed (no orphan listener).
- Editor `FormClosed` / Stop Playtest calls orchestrator + `StopAllOwnedAsync`.

## Not executed here

| Test | Why |
| --- | --- |
| Windows UI smoke ×3 | Linux agent — deferred to GitHub Actions `windows` job |
| Live WPF screenshot capture | No Windows UI session in this environment (schematics provided) |
