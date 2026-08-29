# Phase 7 — CHANGE_SUMMARY (P7-K)

## Starting point
- P7-J re-review rejected tip: `0ece3d9f8a64f4e34517696dce6e5ce20eabdd71`
- P7-J implementation tip: `947e665cf53ebad2d176868415f9f95a586c0e6a`
- Phase 6 accepted baseline: `f4db56592346d9bf0cad9ca153aaeff11ee65de8`

## Final tip (CI green)
- `2f107b3cdb9a677a00992b2296262c78eaff7c6a`
- CI: https://github.com/Netsuno/MMO_Maker/actions/runs/33252829298

## P7-K1 — Reconnect displacement handler fault
- `ClientSession.RemoteEndPoint`: captured at accept time as immutable string; never reads disposed `TcpClient.Client`.
- `TcpFramingProtocolTests.RemoteEndPoint_RemainsSafe_AfterDisconnectAndDispose`: direct unit coverage.
- `Phase7TestLogCollector` + `CreateBuilderWithLogCapture`: lifecycle tests fail on Error/Critical including `Client handler task faulted unexpectedly`.
- `GameServerClientLifecycleTests`: all three cases assert clean logs and observe `StopAsync`.

## P7-K2 — Windows smoke lifecycle
- `StaTestRunner`: `CatchException` mode; hooks for `ThreadException`, `UnhandledException`, unobserved tasks; deterministic STA shutdown via `UiSmokeCollectionMarker.Dispose`.
- `GameplaySmokeHarness.Dispose`: awaits and asserts successful `StopAsync` (timeout/fault → test failure).
- `scripts/ci-guard-lifecycle-logs.ps1` + CI tee steps: reject `Unhandled exception`, handler faults, `BackgroundService failed`, locked-assembly warnings.

## Preserved (accepted P7-J)
- PostgreSQL character-state detachment, PvP contamination/retry, reward failure seam, PG final-hit tests, PvP attacker await, post-respawn HP assert, CI smoke no-retry, SDK 8.0.424 pin, screenshot evidence.

## Suite counts (2f107b3 / CI 33252829298)

| Suite | Result |
| --- | --- |
| Frog.Tests | 284 PASS |
| PG integration | 115 PASS |
| Editor smoke | 35 ×3 first-attempt PASS (log guard PASS) |
| Gameplay smoke | 6 ×3 first-attempt PASS (log guard PASS) |

## Phase 8

Not started.
