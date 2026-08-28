# Phase 7 — CHANGE_SUMMARY (P7-I1…I4)

## Starting point
- Re-review rejected tip: `5ca27c77c3f5e281fe57ed562356112b34c818a1`
- Phase 6 accepted baseline: `f4db56592346d9bf0cad9ca153aaeff11ee65de8`

## Final tip (CI green)
- `95a32ed5e6be37b35edf2a98bfae2ff7446a1359`
- CI: https://github.com/Netsuno/MMO_Maker/actions/runs/33138990836

## P7-I1 — Gameplay-client smokes
- Shop/bank: weapon + consumable in distinct slots; gold priming for 100+25 pricing.
- PvP death: per-hit HP polling; background-thread attacker TCP (STA DoEvents); victim HP primed for lethal hit.
- `SmokeTcpClient`: socket/stream read timeouts.

## P7-I2 — PvP persistence
- Durable-first HP/death save; session restored from DB on failure/cancellation.
- Concurrent `Task.WhenAll` + PG/unit failure/cancel tests.

## P7-I3 — Monster kill reward boundary
- Monster restored on reward failure/cancel; authoritative PG progression + ledger race replay.
- `PostgresMonsterKillRewardTests` + combat-service restore tests.

## P7-I4 — Evidence
- Frog.Tests **283 PASS**; PG **108 PASS**; editor smoke **35×3**; gameplay smoke **6×3** (all first-attempt).
- Screenshot hashes 01–10 recorded; artifact uploaded.
- `git diff --check f4db565..HEAD` **PASS**.

## Phase 8
Not started.
