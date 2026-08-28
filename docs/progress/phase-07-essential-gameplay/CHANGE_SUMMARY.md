# Phase 7 — CHANGE_SUMMARY (P7-I1…I4)

## Starting point
- Re-review rejected tip: `5ca27c77c3f5e281fe57ed562356112b34c818a1`
- Prior implementation: `46df9a6e8756af1d21ac124af0df857f65c7a626`
- Phase 6 accepted baseline: `f4db56592346d9bf0cad9ca153aaeff11ee65de8`

## Implementation tip (local green, CI pending)
- `fefa07080583c4cc53f16fd52f1606bac90b1442`
- CI: pending workflow on branch tip

## P7-I1 — Gameplay-client smokes
- `GameplayClient_ShopSellAndBankGold`: buy stack-1 weapon then consumable for two distinct inventory slots; assert nonzero slot selection.
- `KillVictimViaPvpForTest`: per hit wait for HP decrease or death; death notify only after lethal hit.
- `SmokeTcpClient`: socket/stream read timeouts; `ReadUntil` respects deadline.

## P7-I2 — PvP persistence
- Durable-first HP/death save before session mutation; restore session from DB on save failure or cancellation.
- `Phase7PvPCombatTests`: `Task.WhenAll` concurrent attackers, lethal save failure + retry, cancellation alignment.
- `PostgresPvPCombatTests`: same scenarios against real PostgreSQL with `TestBeforeCommitAsync(record, ct)` seam.

## P7-I3 — Monster kill reward boundary
- Restore monster on reward failure/cancellation (`CancellationToken.None` for restore path).
- `IMonsterKillRewardRepository`: authoritative DB row progression (`FOR UPDATE`); duplicate-key race replay.
- Removed redundant post-reward spell `PersistCombatStateAsync`; MP persisted in reward transaction.
- `Phase7MonsterKillRewardTests` + `PostgresMonsterKillRewardTests` (grant, replay, race, fail, cancel, retry).

## P7-I4 — Evidence
- `git diff --check f4db565..HEAD` clean after csproj LF normalization.
- Local: Frog.Tests **283 PASS**; PG **108 PASS**; gameplay screenshots/hashes pending CI artifact.

## Phase 8
Not started.
