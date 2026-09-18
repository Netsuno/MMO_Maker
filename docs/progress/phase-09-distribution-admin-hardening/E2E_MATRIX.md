# Phase 9 — E2E_MATRIX

**Status:** P9-0…P9-6 **DONE** on product tip `66fa070`. CI https://github.com/Netsuno/MMO_Maker/actions/runs/35291880532 **SUCCESS**. Phase 9 is **READY**. P9-S remains **DEFERRED**.

Phase 8 matrix remains the product gameplay gate: [`../phase-08-quests-events-advanced-creation/E2E_MATRIX.md`](../phase-08-quests-events-advanced-creation/E2E_MATRIX.md). Do not drop those steps. This branch does not change Phase 8 E2E product behavior.

| Step | Description | Expected test | Status |
| ---: | --- | --- | --- |
| 1 | Operator mute persists in PostgreSQL | `Phase9ModerationTests.BanAndMute_PersistAcrossNewGate` | **DONE** (P9-1; in CI PG **180**) |
| 2 | Muted player `ChatSend` rejected; others still receive Global/Map/Whisper | `Phase9ModerationTests` TCP (in-memory + PG) | **DONE** (P9-1; unit **436** + PG **180**) |
| 3 | Kick closes the TCP session; reconnect without a lasting kick policy (login works until ban) | `Phase9ModerationTests` TCP | **DONE** (P9-1) |
| 4 | Ban rejects login and reconnect token | `Phase9ModerationTests` TCP + PG host restart | **DONE** (P9-1) |
| 5 | Unprivileged account cannot issue mute/kick/ban | `Phase9ModerationTests` unit + TCP | **DONE** (P9-1) |
| 6 | `pg_dump` / restore of a seeded world; server migrates 0 pending; login works | `PostgresBackupRestoreTests` | **DONE** (P9-3). **Re-run after P9-1:** CI PG **180** on `66fa070` includes this test on a DB that already has `20260918001424_OpsAccountSanctions`. **Not** a dedicated dump whose payload is mute/ban **rows**. Optional residual. |
| 7 | Packaged server starts with `PostgreSql:Enabled=true` | `PackagedServerPostgreSqlProcessTests` + `packaged-server-smoke.sh --layout-only` | **DONE** (P9-4). CI: PG **180** + log `layout-only smoke OK`. |
| 8 | Load run at the certified session count | `scripts/run-load-harness.sh` + `Phase9OpsMetricsTests` / `PostgresLoadObservabilityTests` | **DONE** (P9-5) — certified **100 authed mixed** in-memory, **200 TCP Hello**; PG concurrent authed floor **4**. See `LOAD_REPORT.md`. CI includes those tests in **436** / **180**. |
| — | Guild / group / trade | — | **DEFERRED (P9-S)** |
| — | Phase 8 23-step + multi-client + Interact identity | `Phase8PostgresE2ETests`, `Phase8MultiClientE2ETests`, `Phase8InteractIdentityTcpTests` | **PASS** on this tip via CI PG **180**. Windows Phase 8 smoke **24×3** + 12-file exact-sha OK. **No Phase 8 regression found.** |

## Multi-client

| Scenario | Status |
| --- | --- |
| Two players: one muted, one not — chat isolation | **DONE** (`Phase9ModerationTests` TCP) |
| Ban vs already-connected session | **DONE** (ban drops live session + PG restart rejects login) |
| Restore then two-client Phase 7 chat still works | P9-3 restore proof exists (Phase 7 TCP login after restore; in CI **180**). Dedicated two-client chat **after** a dump that contains sanction rows was **not** run. Residual / optional. |

## CI pin

- Product tip: `66fa070b07352c5ee0429234bb31470e10608805`
- Run: https://github.com/Netsuno/MMO_Maker/actions/runs/35291880532 **SUCCESS**
- Windows: Frog.Tests **436**, editor **87×3**, gameplay **6×3**, Phase 8 **24×3**
- Ubuntu: PG **180**, layout-only smoke OK
