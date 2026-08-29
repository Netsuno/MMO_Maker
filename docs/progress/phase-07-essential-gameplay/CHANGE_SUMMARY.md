# Phase 7 — CHANGE_SUMMARY (P7-J)

## Starting point
- P7-J rejected tip: `d36169add732544841ba850edea7fce339894037`
- P7-I code-bearing tip: `bfa86bafa1d367a8ab0127c2fff352113b439d65`
- Phase 6 accepted baseline: `f4db56592346d9bf0cad9ca153aaeff11ee65de8`

## Final tip (CI green)
- `947e665cf53ebad2d176868415f9f95a586c0e6a`
- CI: https://github.com/Netsuno/MMO_Maker/actions/runs/33231613723

## P7-J1 — Client disconnect / shutdown lifecycle
- `ClientSession`: send/dispose coordination, `_disposing` gate, active-send drain.
- `ClientNetworkExceptions.IsExpectedTermination` + `GameServerService` handler observation.
- `PlayerLifecycleNotifier`: skip closed clients during leave fan-out.
- `GameServerClientLifecycleTests` (3 PG lifecycle cases).

## P7-J2 — PostgreSQL PvP EF tracking rollback
- `PostgresCharacterRepository.SaveAsync`: detach on failure/cancel.
- `PostgresPvPCombatTests`: contamination + retry death tests.

## P7-J3 — Reward / PvP smoke false positives
- `Phase7MonsterKillRewardTests`: `FailNext=true` before killing blow.
- `CombatGameplayService`: reward persistence exceptions → restore + `Recompense non accordee.`
- `PostgresMonsterKillCombatTests`: final-hit integration via `TestBeforeCommitAsync`.
- `GameplayClientSmokeTests`: await attacker, stop after lethal, post-respawn HP assert, DoEvents host stop.

## P7-J4 — Evidence / CI discipline
- Delete-close: serialized delete + cancellation-safe barrier; smoke barrier fix.
- CI: first-attempt smoke gate (no retry loop); `global.json` SDK 8.0.424; concurrency cancel.
- Evidence docs updated; screenshot hashes preserved from run `33138380861`.

## Suite counts (947e665 / CI 33231613723)

| Suite | Result |
| --- | --- |
| Frog.Tests | 283 PASS |
| PG integration | 115 PASS |
| Editor smoke | 35 ×3 first-attempt PASS |
| Gameplay smoke | 6 ×3 first-attempt PASS |

## Phase 8

Not started.
