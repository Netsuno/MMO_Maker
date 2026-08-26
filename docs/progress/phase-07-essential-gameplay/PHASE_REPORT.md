# Phase 7 — PHASE_REPORT (CHANGES REQUESTED → remediations)

## Status

**Phase 7: CHANGES REQUESTED** — P7-FIX-1…5 + P7-R1…R7 remediations applied; CI green on implementation tip; **waiting for re-review**.

| Workstream | Status |
| --- | --- |
| P7-FIX-1 PostgreSQL SoT + published catalogs | DONE |
| P7-FIX-2 Atomic shop/bank + bank gold | DONE |
| P7-FIX-3 PG integration + 17-step E2E | DONE |
| P7-FIX-4 Client protocol/UI + gameplay smoke | DONE |
| P7-FIX-5 Documentation integrity | DONE |
| P7-R1 Published world migration + seed | DONE |
| P7-R2 Packaged Release server PG process test | DONE |
| P7-R5 Atomic combat mutations | DONE |
| P7-R6 Production-composition PG E2E | DONE |
| P7-R7 Client catalog UI + gameplay smoke ×5 | DONE |

## Identity

| Item | Value |
| --- | --- |
| Branch | `cursor/phase0-baseline-audit-02c7` |
| PR | #2 |
| Phase 6 accepted implementation | `99b782f8f205c0161c0bba8838d041714e39947e` |
| Phase 6 accepted evidence tip | `f4db56592346d9bf0cad9ca153aaeff11ee65de8` |
| Phase 7 rejected tip | `67281e3c62eb1943341b162fe1213abb5fc7011a` |
| Phase 7 implementation SHA | `862d2e3398b23ea38b50ab14a675d3b0579f8cff` |
| CI (implementation) | https://github.com/Netsuno/MMO_Maker/actions/runs/33021897140 |
| Phase 8 | **Not started** |

## What changed (remediation)

### P7-FIX-1
- Production fails closed unless `PostgreSql:Enabled=true` (or playtest / `AllowInMemoryFallback` for tests).
- `PostgreSqlServerAuthBackend` registers player repos + `IPublished*` Postgres catalogs + migrates DB.
- `Phase7PublishedContent` limited to playtest/in-memory test composition.
- MariaDB auth not selected for Phase 7 production.

### P7-FIX-2
- `BankGold` on characters; `shop_stock`; `economy_request_ids`.
- `IEconomyTransactionRepository` / Postgres atomic buy/sell/bank.
- Session updated only after commit; idempotent `requestId`.

### P7-FIX-3
- Expanded PG player/economy/content visibility tests.
- `Phase7PostgresE2ETests` true PG headless gate (no mid-scenario DI mutation).
- Multi-client: pickup race, combat XP once, shop idempotency, whisper isolation, reconnect displace.
- In-memory companion `Phase7InMemorySmokeE2ETests` + immediate-reconnect registry regression.

### P7-FIX-4
- Typed `Phase7PacketCodec` + client send/receive.
- MainShellForm gameplay tabs; Inventory/Equipment panels.
- `GameplayClientSmokeTests` + CI ×3 + artifact `phase-07-gameplay-client-screenshots`.
- Reconnect path: unregister displaced client; preload map after reconnect; Playing after map ready.

### P7-FIX-5
- STATUS remains **CHANGES REQUESTED** until re-review accepts.
- Exact suite counts (271 / 93 / 35×3 / 5×3); SHA + CI URLs in this dossier.

## Phase 8

Not started.
