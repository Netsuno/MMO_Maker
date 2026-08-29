# Phase 8 — PHASE_REPORT

## Status

**Phase 8: CHANGES REQUESTED** (remediation in progress)

| Tranche | Status |
| --- | --- |
| P8-1 PostgreSQL event model + map authoring | DONE (preserved) |
| P8-2 Authoritative typed event runtime | IN PROGRESS — executor hardened; autorun/parallel/wait/resume pending |
| P8-3 Dialogues and quests | IN PROGRESS — PG content store; transactional turn-in pending |
| P8-4 Professions and recipes | IN PROGRESS — PG content + PostgresEventCraftRepository |
| P8-5 Regions, weather and lighting | IN PROGRESS — PG content store |
| P8-6 Common events and advanced creator tools | IN PROGRESS — PG content; structured editor pending |
| P8-R5 E2E / Windows smoke | NOT RUN |

## Remediation progress (this branch)

### P8-R1 (partial)
- Unified PostgreSQL `phase8_content_definitions` + published snapshots (dialogue, quest, common event, profession, recipe, region, weather)
- `PostgresPhase8PublishedCatalogs` replaces in-memory catalogs in **production PostgreSQL** composition
- `PostgresEventCraftRepository` replaces in-memory craft in PG mode

### P8-R2 (partial)
- Atomic craft with idempotency keys in PostgreSQL
- Quest turn-in still needs single-transaction mutation repository

### P8-R3 (partial)
- `WorldFlagsPatchRequest` rejected when PostgreSQL production enabled
- Per-execution `CommonEventDepth` / step budget state (no singleton field)

### P8-R4 / P8-R5
- Wire protocol, client UI, structured editors, 23-step E2E, multi-client, Windows smoke: **not started**

## Phase 9

Not started.
