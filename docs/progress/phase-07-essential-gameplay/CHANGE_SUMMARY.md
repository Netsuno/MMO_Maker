# Phase 7 — Change Summary

## Starting baseline
- Accepted Phase 6 implementation: `99b782f8f205c0161c0bba8838d041714e39947e`

## Final tip
- Branch tip: `33ec0dce22c2b54ce325541bba1a9b68fad1b768`
- CI: https://github.com/Netsuno/MMO_Maker/actions/runs/32921656173

## Delivered by tranche

### 7.1 Authentication
- Identity ports, PBKDF2, sessions, rate limit, reconnect protocol, PG `auth` schema

### 7.2 Characters
- PG `player.characters`, class-seeded create, list/select/ownership

### 7.3 Inventory / equipment / ground
- Inventory/equipment/ground repositories, concurrent pickup, equip validation

### 7.4 Combat / spells
- Deterministic melee/spell formulas, monster runtime, cooldowns, forged rejection

### 7.5 Chat
- Map/global (existing) + rate limiting + recipient isolation E2E

### 7.6 Shops / bank
- Published shop buy/sell, bank deposit/withdraw, gold

### 7.7 Progression / death / respawn
- XP curve, level-up bonuses, death/respawn, persistence across reconnect

### Protocol
- Packets 36–37 (reconnect), 38–63 (gameplay)

### E2E
- `Phase7InMemorySmokeE2ETests` — fast smoke with `AllowInMemoryFallback=true` (DI helpers allowed)
- `Phase7PostgresE2ETests` — true PostgreSQL headless gate (63 PG integration tests incl. player repos, content seed visibility, 17-step flow)

### P7-FIX-3 remediation
- `GameplayLimits.StartingGold` (100) on character create (PG + in-memory)
- `Phase7PostgresContentSeed` helper — publishes Phase7ContentSeed catalog to PG before host start
- Expanded `PostgresPlayerRepositoryTests` — characters, inventory, equipment, ground race, bank, progression, schemas, gate lifecycle
- PvP melee applies damage / death (`TryMeleeAttackPlayerAsync`) for death/respawn E2E without mid-scenario DI
- Optional `requestId` on shop buy/sell wire payloads
- `Phase7PacketCodec.ReadGuid` fix (exactly 16 bytes)

## Phase 8
Not started.
