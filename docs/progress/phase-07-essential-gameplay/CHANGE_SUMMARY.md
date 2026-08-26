# Phase 7 — Change Summary

## 7.1 Authentication and sessions (prior tranche)

- Application identity ports, PBKDF2 hashing, PostgreSQL auth schema, reconnect protocol

## 7.2–7.7 Essential gameplay (this tranche)

### Core / protocol
- `PacketId` 38–63 for inventory, combat, shop, bank, respawn
- `Phase7GameplayWire` DTOs for snapshot packets

### Server gameplay
- `Phase7PublishedContent` — singleton published catalogs + `GetItem`/`GetSpell`/etc.
- `CharacterGameplayService`, `InventoryGameplayService`, `CombatGameplayService`, `ShopBankGameplayService`
- `ChatRateLimiter` — per-session chat flood control
- `Session` extensions + `SessionGameplayExtensions`

### Network
- `PacketSender` methods for all new packets
- `PacketDispatcher` partial — account-aware character create/select/list, gameplay handlers, combat-before-PvP melee, chat rate limit

### DI
- In-memory gameplay repos when `!pgAuthRegistered`
- Published catalog + service registration in `FrogServerHostFactory`

### Tests
- Six new Phase 7 test classes including TCP E2E gate (`Phase7E2EGameplayTests`)

## Not in scope

- Client UI for new packets (deferred)
- Phase 8 content/editor integration
