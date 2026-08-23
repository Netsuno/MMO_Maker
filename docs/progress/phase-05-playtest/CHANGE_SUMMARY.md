# Phase 5 — CHANGE SUMMARY (fourth rejection corrections)

## Playtest token (blocker 1)

- `PlaytestAuthToken.IsReservedUsername()` — rejects registration of `__frog_playtest__`
- `PacketDispatcher`: reserved username routes only through playtest gate; **no** fallback to `AuthService.ValidateCredentials`
- `PlaytestAuthTokenGate`: **TryClaim / CommitClaim / ReleaseClaim** — token consumed only after successful session + login; released on session-creation failure
- TCP integration tests in `PlaytestTokenReuseTests`

## READY map authority (blocker 2)

- `PlaytestClientReadyState`: tracks `PositionMapId` (PositionUpdate) and `LoadedMapId` (MapData / MapAlreadySynced) separately
- `MainShellForm`: emits READY only when both IDs exist and match; sanitized failure on mismatch
- Unit tests in `PlaytestClientReadyStateTests`

## Preserved (third rejection)

- Strict READY marker parsing and plan validation
- Real Frog.Server + Frog.Client production smoke ×3
- Token removed from command-line arguments
- Early-exit PID, stderr, exit code 7; stop ownership retention
- Child environment isolation; invalid WorkDirectory no-leak
- Screenshots **NOT RUN**
