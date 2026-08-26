# Phase 7 — CHANGE_SUMMARY (remediation)

## Starting point for remediations
- Rejected tip: `67281e3c62eb1943341b162fe1213abb5fc7011a`
- Phase 6 accepted implementation: `99b782f8f205c0161c0bba8838d041714e39947e`
- Phase 6 accepted evidence tip: `f4db56592346d9bf0cad9ca153aaeff11ee65de8`

## P7-FIX-1
- Fail-closed production DI without PostgreSQL (playtest / `AllowInMemoryFallback` only for tests).
- Backend registers Postgres published catalogs + migrates.
- Config examples document PostgreSQL as Phase 7 production DB.
- Host composition test asserts Postgres DI types.

## P7-FIX-2
- `BankGold`, `shop_stock`, `economy_request_ids` migration.
- Atomic `IEconomyTransactionRepository` for buy/sell/bank.
- Idempotent `requestId`; session update after commit.
- Monster XP granted once (`Defeated` / remove-before-grant).

## P7-FIX-3
- PG player/content/economy integration expansion.
- `Phase7PostgresE2ETests` 17-step headless PG E2E without mid-scenario DI injection.
- Multi-client: pickup, combat XP, shop idempotency, whisper isolation, reconnect displace.
- Renamed in-memory smoke E2E.

## P7-FIX-4
- Typed codec + client send/receive for Phase 7 packets.
- Usable MainShellForm gameplay UI (inventory/equip/shop/bank/combat/respawn/reconnect).
- `GameplayClientSmokeTests` + CI ×3 + screenshot artifact.

## P7-FIX-5
- STATUS = CHANGES REQUESTED during remediation; matrices updated for re-review.
- Exact counts (no `270+`); SHA/CI identity separated.

## Phase 8
Not started.
