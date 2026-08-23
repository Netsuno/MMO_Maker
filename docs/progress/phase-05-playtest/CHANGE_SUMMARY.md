# Phase 5 — CHANGE SUMMARY (fifth rejection corrections)

## Token security (blockers)

### Case-insensitive reserved identity

- Shared `Frog.Core.Identity.AccountUsername` (`OrdinalIgnoreCase`) used by:
  - `PlaytestAuthToken.IsReservedUsername`
  - `AccountRepository`
  - `ConnectionManager`
- Every casing of `__frog_playtest__` routes exclusively through the playtest gate; registration rejected for all casings.

### Commit before LoginResult

- `HandlePlaytestLoginAsync` commits the claim via `beforeSuccessfulLoginResult` **before** sending a positive `LoginResult`.
- `claimCommitted` flag: release only when failure occurs before commit; post-commit errors clean up the session but never restore the token.
- Test seam: `PlaytestRuntimeOptions.FailAfterSuccessfulLoginResult`.

## Preserved

- READY map authority (`PositionMapId` / `LoadedMapId`)
- Real Frog.Client smoke ×3, env-only token, early-exit/stop ownership, env isolation, workspace no-leak
- Screenshots **NOT RUN**
