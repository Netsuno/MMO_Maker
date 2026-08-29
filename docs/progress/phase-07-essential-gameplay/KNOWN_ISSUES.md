# Phase 7 — Known Issues

## Remediations landed (P7-FIX / P7-R / P7-G / P7-I / P7-J)

Prior rejection items were addressed in code. Remaining limitations:

1. Unused legacy stub files under `Frog.Client/Models/*` and some `Frog.Client/Services/*` still contain `// TODO` placeholders; gameplay uses `MainShellForm` + `FrogGameClient` + inventory/equipment/bank/ground panels instead.
2. Legacy MariaDB adapters remain for older map/player world-state paths; Phase 7 **auth/gameplay production** requires PostgreSQL and does not select MariaDB identity.
3. Gameplay-client screenshot SHA-256 hashes in `SCREENSHOT_MANIFEST.md` are from CI artifact run `33138380861` (implementation near `bfa86bafa`); preserved for P7-J because visible smoke evidence is unchanged.

## Phase 8

Not started.
