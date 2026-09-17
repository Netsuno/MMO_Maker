# Phase 9 — E2E_MATRIX

**Status:** stub (P9-0). Rows are planned, not executed.

Phase 8 matrix remains the product gameplay gate: [`../phase-08-quests-events-advanced-creation/E2E_MATRIX.md`](../phase-08-quests-events-advanced-creation/E2E_MATRIX.md). Do not drop those steps.

| Step | Description | Expected test | Status |
| ---: | --- | --- | --- |
| 1 | Operator mute persists in PostgreSQL | TBD P9-1 | NOT STARTED |
| 2 | Muted player `ChatSend` rejected; others still receive Global/Map/Whisper | TBD P9-1 | NOT STARTED |
| 3 | Kick closes the TCP session; reconnect without lifting kick policy TBD | TBD P9-1 | NOT STARTED |
| 4 | Ban rejects login and reconnect token | TBD P9-1 | NOT STARTED |
| 5 | Unprivileged account cannot issue mute/kick/ban | TBD P9-2 | NOT STARTED |
| 6 | `pg_dump` / restore of a seeded world; server migrates 0 pending; login works | TBD P9-3 | NOT STARTED |
| 7 | Packaged server starts with `PostgreSql:Enabled=true` | TBD P9-4 | NOT STARTED |
| 8 | Load run at the certified session count (number TBD after P9-5 measure) | TBD P9-5 | NOT STARTED |
| — | Guild / group / trade | — | **DEFERRED (P9-S)** |

## Multi-client (planned)

| Scenario | Status |
| --- | --- |
| Two players: one muted, one not — chat isolation | NOT STARTED |
| Ban vs already-connected session | NOT STARTED |
| Restore then two-client Phase 7 chat still works | NOT STARTED |
