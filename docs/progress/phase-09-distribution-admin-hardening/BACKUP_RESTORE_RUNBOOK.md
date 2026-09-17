# Phase 9 — BACKUP_RESTORE_RUNBOOK

**Status:** stub (P9-0). No backup script exists in `scripts/` today.

## Schema to protect

PostgreSQL 16, EF migrations in `Frog.Persistence.PostgreSql/Migrations/` (24 Up, tip `MapEventExecutionRequestIdGlobalUnique`). Schemas: `auth`, `content`, `ops`, `player`, `world`. See BASELINE_AUDIT §5.

## TBD (P9-3)

- `pg_dump` format (custom vs directory) and required roles
- Whether to dump `ops.legacy_imports`
- Restore onto an empty cluster then `Database.Migrate()` (expect **0** pending)
- Proof: seed → dump → drop → restore → `PostgresDatabaseHealth` OK → login
- Retention / off-box copy
- What happens if P9-1 adds ban tables after the first runbook (re-prove restore)

## Out of scope

- MariaDB dump / `scripts/apply-frog-mariadb-schema.ps1`
- Point-in-time replication (not requested)
