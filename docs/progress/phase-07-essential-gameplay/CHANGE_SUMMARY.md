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
- `Phase7E2EGameplayTests` full flow + pickup race

## Phase 8
Not started.
