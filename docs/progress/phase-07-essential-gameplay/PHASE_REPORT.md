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

**Gate:** `PHASE 7 GATE REACHED — WAITING FOR REVIEW`

## Baselines

| Item | Value |
| --- | --- |
| Starting (Phase 6 accepted) | `99b782f8f205c0161c0bba8838d041714e39947e` |
| Final branch tip | `33ec0dce22c2b54ce325541bba1a9b68fad1b768` |
| Branch | `cursor/phase0-baseline-audit-02c7` |
| PR | #2 |
| Final CI | https://github.com/Netsuno/MMO_Maker/actions/runs/32921656173 |

## Database (PostgreSQL)

- `auth.accounts`, `auth.auth_sessions` (7.1)
- `player.characters`, `player.inventory_slots`, `player.bank_slots`, `player.ground_items` (7.2–7.6)
- Migrations: `20260825224547_AuthAccountsAndSessions`, `20260826014225_PlayerCharactersInventoryBankGround`

## Protocol

- Reconnect 36–37; gameplay 38–63 (inventory, equip, ground, combat, shop, bank, respawn, XP, death)

## Security

- PBKDF2 passwords; opaque tokens; generic errors; rate limits; server-authoritative combat/economy

## Tests (CI tip)

- Unit/protocol/E2E headless: PASS (270+)
- PostgreSQL integration: PASS (39)
- Windows smoke ×3: PASS

## Phase 8

Not started.
