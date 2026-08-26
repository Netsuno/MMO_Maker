# Phase 7 — Essential Gameplay

## Status

| Tranche | Status |
| --- | --- |
| 7.1 Authentication and sessions | DONE |
| 7.2 Characters | DONE |
| 7.3 Inventory, equipment, ground items | DONE |
| 7.4 Combat and essential spells | DONE |
| 7.5 Chat | DONE |
| 7.6 Shops and bank | DONE |
| 7.7 Progression, death and respawn | DONE |
| Phase 7 E2E gate | DONE |

## Baselines

- Starting implementation SHA (accepted Phase 6): `99b782f8f205c0161c0bba8838d041714e39947e`
- Branch: `cursor/phase0-baseline-audit-02c7`
- PR: #2

## Delivered

### Runtime content
- `Phase7PublishedContent` — in-memory published catalogs (class, spell, item, NPC, shop) seeded from `Phase7ContentSeed`

### Gameplay services
- `CharacterGameplayService` — create/list/select with published classes
- `InventoryGameplayService` — add/remove/equip/unequip/drop/pickup
- `CombatGameplayService` — monster registry, melee, spells, XP, respawn
- `ShopBankGameplayService` — buy/sell, bank item/gold deposit/withdraw

### Protocol (packets 38–63)
- Inventory, equipment, ground items, combat state, shop/bank, respawn, XP/death notifications

### Session model
- Gameplay fields on `Session` + `ApplyFromCharacter` / `ToCharacterPatch`

### DI
- `FrogServerHostFactory` registers in-memory repos when PG auth not registered; published content + gameplay services always registered

### Tests
- `Phase7CharacterTests`, `Phase7InventoryTests`, `Phase7CombatTests`, `Phase7ShopBankTests`, `Phase7ProgressionTests`, `Phase7E2EGameplayTests`

## Phase 8

Not started.
