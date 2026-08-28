# Phase 7 — CHANGE_SUMMARY (P7-H1…H5)

## Starting point
- Re-review rejected tip: `7ecb8d2c26ac3130b3f8557aba0ef78803601677`
- Phase 6 accepted baseline: `f4db56592346d9bf0cad9ca153aaeff11ee65de8`
- P7-G1…G6 remediations preserved on branch.

## Implementation tip (local green, CI pending)
- `46df9a6e8756af1d21ac124af0df857f65c7a626`
- CI: pending push workflow

## P7-H1 — Equip/unequip single mutation boundary
- Removed dispatcher-level `PersistCombatStateAsync` after equip/unequip; transfer repository is sole commit.
- Session updated only from committed transfer result; snapshots sent after atomic success.
- Added `Phase7EquipPersistenceTests` (no redundant character save, gold not overwritten, reconnect equipment).

## P7-H2 — Shutdown + combat persistence
- `GameServerService` tracks client handler tasks; stops accepting on shutdown; awaits all handlers.
- `GameServerGracefulShutdownTests` blocks a real PG shop buy during `StopAsync`.
- PvP HP/death mutation + persistence under `CharacterMutationCoordinator`.
- Idempotent `IMonsterKillRewardRepository` (+ PG `monster_kill_rewards` migration) for monster XP.
- `Phase7PvPCombatTests`, `Phase7MonsterKillRewardTests`.

## P7-H3 — Genuine two-client economy race
- `ShopBuyRace_TwoClients_FinalStockUnit_ExactlyOneWinner` (two TCP clients, distinct request IDs, stock=0, exact gold/inventory, durable restart check).

## P7-H4 — Gameplay UI smokes
- Inventory row selection drives shop sell + bank deposit; bank list drives withdraw only.
- Death via second-client PvP melee; respawn via visible `Respawn` button (no session mutation).
- `GameplayClient_Phase7ScreenshotFlows` adds screenshots 06–10.

## P7-H5 — Evidence
- `git diff --check f4db56592346d9bf0cad9ca153aaeff11ee65de8..HEAD` clean after manifest whitespace fix.
- Multi-client matrix corrected: idempotent single-client retry ≠ multi-client shop race.
- STATUS **CHANGES REQUESTED** until CI green on implementation tip and screenshot hashes recorded.

## Phase 8
Not started.
