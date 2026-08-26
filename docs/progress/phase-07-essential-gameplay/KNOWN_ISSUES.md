# Phase 7 — Known Issues

- Client `AuthService` remains a stub; login UI does not yet persist/reuse auth tokens for reconnect.
- PostgreSQL auth requires `Frog.Persistence.PostgreSql.dll` beside the server executable (build target copies it).
- When `PostgreSql:Enabled=true` without the backend DLL loaded, host startup fails by design.
- Bank gold (deposit/withdraw via packets 55/57 with 4-byte payload) is stored in-memory per `ShopBankGameplayService` — not yet persisted to PostgreSQL `CharacterEntity`.
- Client does not yet handle packets 38–63; server-side gameplay is complete and E2E-tested via TCP.
