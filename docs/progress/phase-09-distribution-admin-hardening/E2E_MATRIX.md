# Phase 9 — E2E_MATRIX

**Status:** P9-0 planning + P9-1 TCP tests executed (see rows 1–5). Phase 8 matrix remains the product gameplay gate.

Phase 8 matrix remains the product gameplay gate: [`../phase-08-quests-events-advanced-creation/E2E_MATRIX.md`](../phase-08-quests-events-advanced-creation/E2E_MATRIX.md). Do not drop those steps.

| Step | Description | Expected test | Status |
| ---: | --- | --- | --- |
| 1 | Operator mute persists in PostgreSQL | `Phase9ModerationTests.BanAndMute_PersistAcrossNewGate` | DONE |
| 2 | Muted player `ChatSend` rejected; others still receive Global/Map/Whisper | `Phase9ModerationTests` TCP (in-memory + PG) | DONE |
| 3 | Kick closes the TCP session; reconnect without a lasting kick policy (login works until ban) | `Phase9ModerationTests` TCP | DONE |
| 4 | Ban rejects login and reconnect token | `Phase9ModerationTests` TCP + PG host restart | DONE |
| 5 | Unprivileged account cannot issue mute/kick/ban | `Phase9ModerationTests` unit + TCP | DONE |
| 6 | `pg_dump` / restore of a seeded world; server migrates 0 pending; login works | `PostgresBackupRestoreTests` | DONE (P9-3). **Re-run after P9-1 migration.** |
| 7 | Packaged server starts with `PostgreSql:Enabled=true` | `PackagedServerPostgreSqlProcessTests` | **DONE** (P9-4; re-run this agent: included in PG **180 PASS**) |
| 8 | Load run at the certified session count | `scripts/run-load-harness.sh` + `Phase9OpsMetricsTests` / `PostgresLoadObservabilityTests` | **DONE** — certified **100 authed mixed** in-memory, **200 TCP Hello**; see `LOAD_REPORT.md` |
| — | Guild / group / trade | — | **DEFERRED (P9-S)** |

## Multi-client (planned)

| Scenario | Status |
| --- | --- |
| Two players: one muted, one not — chat isolation | DONE (`Phase9ModerationTests` TCP) |
| Ban vs already-connected session | DONE (ban drops live session + PG restart rejects login) |
| Restore then two-client Phase 7 chat still works | P9-3 restore proof exists; re-run after P9-1 dump |
