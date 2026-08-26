# Phase 7 — Known Issues

- Client receive handlers exist for packets 36–63 (events); dedicated inventory/combat/shop UI panels are not built yet.
- PostgreSQL auth/player requires `Frog.Persistence.PostgreSql.dll` beside the server executable (build target copies it).
- When `PostgreSql:Enabled=true` without the backend DLL loaded, host startup fails by design.
- Bank gold wallet (packets 55/57 with gold payload) is stored in-memory in `ShopBankGameplayService`; character gold and item bank persist via character/inventory repositories.
