# Phase 7 — CHANGE_SUMMARY (P7-J)

## Starting point
- P7-I rejected tip: `d36169add732544841ba850edea7fce339894037`
- Last code-bearing P7-I tip: `bfa86bafa1d367a8ab0127c2fff352113b439d65`
- Phase 6 accepted baseline: `f4db56592346d9bf0cad9ca153aaeff11ee65de8`

## P7-J1 — Client disconnect / shutdown lifecycle
- `ClientSession`: `_disposing` gate, acquire/release guard, active-send drain before dispose.
- `ClientNetworkExceptions.IsExpectedTermination`: IOException, SocketException, ObjectDisposedException, cancellation.
- `GameServerService`: observe handler faults without stopping host; expected termination during shutdown/disconnect.
- `PlayerLifecycleNotifier`: skip closed clients during leave fan-out.
- `GameServerClientLifecycleTests`: disconnect during broadcast, reconnect displacement, multi-client shutdown.

## P7-J2 — PostgreSQL PvP EF tracking rollback
- `PostgresCharacterRepository.SaveAsync`: detach tracked entity on any failure/cancellation before rethrow.
- `PostgresPvPCombatTests`: lethal save failure/cancel must not contaminate unrelated later save; retry persists exactly one death.

## P7-J3 — Reward / PvP smoke false positives
- `Phase7MonsterKillRewardTests`: `FailNext=true` before first killing blow.
- `CombatGameplayService`: reward persistence exceptions return `Recompense non accordee.` after monster restore (integration boundary).
- `PostgresMonsterKillCombatTests`: final-hit failure/cancel/retry through `CombatGameplayService`.
- `GameplayClientSmokeTests`: await PvP attacker task; stop after lethal hit; post-respawn HP cooldown assert.

## P7-J4 — Evidence / CI discipline
- `GameDataPanelLifecycle`: barrier no longer blocks UI thread (fixes delete-close timeout).
- CI smoke steps: removed 2-attempt retry loops (first-attempt gate only).
- `global.json`: SDK **8.0.424** pinned.
- Evidence docs updated; screenshot hashes preserved from run `33138380861`.

## Suite targets (P7-J tip)

| Suite | Target |
| --- | --- |
| Frog.Tests | 283 PASS |
| PG integration | 115 PASS |
| Editor smoke | 35 ×3 first-attempt |
| Gameplay smoke | 6 ×3 first-attempt |

## Phase 8

Not started.
