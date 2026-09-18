# Phase 9 — CHANGE_SUMMARY

**Status:** P9-0 + P9-2 + P9-3 landed on `cursor/phase9-distribution-admin-hardening`. P9-1 / P9-4…P9-6 still TBD.

## P9-0 (bootstrap)

- Added `docs/progress/phase-09-distribution-admin-hardening/` planning pack.
- Updated `docs/STATUS.md`: Phase 8 ACCEPTED on main; Phase 9 IN PROGRESS.
- P9-S (guilds / groups / trades): **DEFERRED**. See PHASE_PLAN.

## P9-1 Admin / moderation

TBD — no mute/kick/ban product code. Design is in `SECURITY_MODEL.md` §5.

## P9-2 Security / permissions

- Wrote concrete `SECURITY_MODEL.md` (actors, assets, trust boundaries, leftover packets, bind/TLS, secrets).
- Permission SoT: table `auth.operators` + `IOperatorDirectory` (not an `auth.accounts` flag). EF migration `20260917223000_AuthOperators`.
- Strengthened `WorldFlagsPatchRequest` rejection (PG enabled **or** production composition).
- Placeholder secret refuse-to-start on public bind; committed `appsettings.json` uses `NOT_A_PRODUCTION_SECRET`.
- Non-loopback bind requires `Server:AllowNonLoopbackBind=true`.
- Tests: `Frog.Tests/Phase9SecurityGateTests.cs`, in-memory WorldFlags production reject, PG operator + WorldFlags TCP.

## P9-3 PostgreSQL backup / restore

- Scripts: `scripts/postgres-backup.sh`, `postgres-restore.sh`, `postgres-verify.sh`, `postgres-backup-restore-smoke.sh`, `postgres-common.sh`, plus Windows `.ps1` mirrors for backup/restore/verify.
- Runbook: [`BACKUP_RESTORE_RUNBOOK.md`](BACKUP_RESTORE_RUNBOOK.md) — dump custom format (`-Fc`) of schemas `auth`, `content`, `ops`, `player`, `world` (includes `ops.legacy_imports` and `world.__EFMigrationsHistory`). Restore onto an empty database; do not migrate first.
- Proof: `tests/Frog.Persistence.IntegrationTests/PostgresBackupRestoreTests.cs` (CI job `postgres-integration` after `postgresql-client` install). Command: `./scripts/postgres-backup-restore-smoke.sh`.
- No MariaDB path. P9-3 adds no EF migrations; dumps include P9-2 `auth.operators` via schema `auth`. Re-prove restore after P9-1 schema changes.

## P9-4 Packaging

TBD.

## P9-5 Load / observability

TBD.

## P9-6 Evidence

TBD — do not invent CI for later tips until a run exists.

## Out of scope (must stay empty)

- Phase 8 product behavior
- VB6 / `.fcc` import
- New MariaDB
- Phase 10
- P9-S social features
