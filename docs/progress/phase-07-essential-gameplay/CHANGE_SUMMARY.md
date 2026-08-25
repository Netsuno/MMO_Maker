# Phase 7 — Change Summary (7.1)

## Authentication and sessions

- New `Frog.Application/Identity/` ports and validation rules
- New `Frog.Core/Security/PasswordHasher.cs` (PBKDF2 v1 + legacy compat)
- PostgreSQL migration `20260825224547_AuthAccountsAndSessions`
- Repositories: `PostgresAccountRepository`, `PostgresAuthSessionRepository`
- Server: `AuthService`, `LoginRateLimiter`, in-memory and MariaDB legacy adapters
- Protocol: login issues opaque token; `ReconnectRequest` (36) / `ReconnectResult` (37)
- Architecture: `IServerAuthBackend` registry; PG DLL copied/loaded at runtime (no compile ref from Server)

## Removed

- Legacy `Frog.Server/Database/IAccountRepository.cs` and MariaDB-only account repos (replaced by Application port + adapters)

## Not in this tranche

- Character creation/selection (7.2)
- Client UI auth token storage (deferred to 7.2/E2E)
