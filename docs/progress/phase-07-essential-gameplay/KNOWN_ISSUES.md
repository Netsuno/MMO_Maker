# Phase 7 — Known Issues (7.1)

- Client `AuthService` remains a stub; login UI does not yet persist/reuse auth tokens for reconnect.
- PostgreSQL auth requires `Frog.Persistence.PostgreSql.dll` beside the server executable (build target copies it).
- When `PostgreSql:Enabled=true` without the backend DLL loaded, host startup fails by design.
