# Phase 9 — E2E_MATRIX

**Status:** Phase 9 is **NOT READY**. Prior READY on `8bf6f08` withdrawn. P9-S remains **DEFERRED**.

Phase 8 matrix remains the product gameplay gate: [`../phase-08-quests-events-advanced-creation/E2E_MATRIX.md`](../phase-08-quests-events-advanced-creation/E2E_MATRIX.md). Do not drop those steps. This branch does not change Phase 8 E2E product behavior.

| Step | Description | Expected test | Status |
| ---: | --- | --- | --- |
| 1 | Operator mute persists in PostgreSQL | `Phase9ModerationTests.BanAndMute_PersistAcrossNewGate` | **DONE** (P9-1) |
| 2 | Muted player `ChatSend` rejected; others still receive Global/Map/Whisper | `Phase9ModerationTests` TCP (in-memory + PG) | **DONE** (P9-1) |
| 3 | Kick closes the TCP session; peer `PlayerLeave`; state save; execution cancel; idempotent repeat; reconnect without a lasting kick policy | `Phase9ModerationTests` TCP + `Phase9SessionTeardownTests` | **DONE** (P9-1 / C2) |
| 4 | Ban rejects login and reconnect token | `Phase9ModerationTests` TCP + PG host restart | **DONE** (P9-1) |
| 5 | Unprivileged account cannot issue mute/kick/ban | `Phase9ModerationTests` unit + TCP | **DONE** (P9-1) |
| 6 | `pg_dump` / restore of a seeded world; server migrates 0 pending; login works | `PostgresBackupRestoreTests` | **DONE** (schema). **Not** a dedicated dump whose payload is mute/ban **rows**. **Not certified.** |
| 7 | Packaged **server** starts with `PostgreSql:Enabled=true` | `PackagedServerPostgreSqlProcessTests` + `packaged-server-smoke.sh --layout-only` | **DONE** (P9-4 Linux). Packaged client/editor launch **not proven**. |
| 8 | Load run at the measured session count | `scripts/run-load-harness.sh` + `Phase9OpsMetricsTests` / `PostgresLoadObservabilityTests` | **Measured** — 100 authed mixed in-memory, 200 TCP Hello; PG concurrent authed floor 4. Unexecuted BASELINE rows **not certified**. See `LOAD_REPORT.md`. |
| — | Guild / group / trade | — | **DEFERRED (P9-S)** |
| — | Phase 8 23-step + multi-client + Interact identity | `Phase8PostgresE2ETests`, `Phase8MultiClientE2ETests`, `Phase8InteractIdentityTcpTests` | Historical PASS on refused-gate CI PG **180**. Windows Phase 8 smoke **24×3** + 12-file exact-sha OK on that CI. Re-confirm on correction-tip CI. |

## Multi-client

| Scenario | Status |
| --- | --- |
| Two players: one muted, one not — chat isolation | **DONE** (`Phase9ModerationTests` TCP) |
| Ban vs already-connected session | **DONE** (ban drops live session + PG restart rejects login) |
| Kick peer `PlayerLeave` + state save + execution cancel | **DONE** (`Tcp_KickNotifiesPeersSavesStateCancelsExecutionsAndIsIdempotent`) |
| Restore then two-client Phase 7 chat still works | P9-3 restore proof exists (Phase 7 TCP login after restore). Dedicated two-client chat **after** a dump that contains sanction rows was **not** run. **Not certified.** |

## CI pin

- Historical (refused gate): `8bf6f08` / https://github.com/Netsuno/MMO_Maker/actions/runs/35292542956 SUCCESS — Frog.Tests **436**, PG **180**, editor **87×3**, gameplay **6×3**, Phase 8 **24×3**
- Correction tip: **not pinned yet**
