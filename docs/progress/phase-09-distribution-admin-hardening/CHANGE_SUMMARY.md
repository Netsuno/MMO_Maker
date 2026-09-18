# Phase 9 — CHANGE_SUMMARY

**Status:** P9-0 + P9-1 + P9-2 + P9-3 + P9-4 landed on `cursor/phase9-distribution-admin-hardening`. P9-5 / P9-6 still TBD.

## P9-0 (bootstrap)

- Added `docs/progress/phase-09-distribution-admin-hardening/` planning pack.
- Updated `docs/STATUS.md`: Phase 8 ACCEPTED on main; Phase 9 IN PROGRESS.
- P9-S (guilds / groups / trades): **DEFERRED**. See PHASE_PLAN.

## P9-1 Admin / moderation

- Schema `ops.account_sanctions` + `ops.moderation_events` (EF migration `20260918001424_OpsAccountSanctions`). Mute/ban state is operational, not an `auth.accounts` flag.
- Server path: `ModerationService` + `IAccountSanctionStore`. **Always** `IOperatorDirectory.IsOperatorAsync(session.AccountId)` before mute/kick/ban. Grant stays SQL / `GrantAsync` out of band — no grant/revoke PacketId.
- Wire: `PacketId.ModerateRequest` (78) / `ModerateResult` (79). Client slash commands (`/mute` `/kick` `/ban` `/unmute` `/unban`) in the existing chat box; no new layout (Phase 8 screenshot gates untouched).
- Enforcement: mute rejects `ChatSend` only; kick drops the live TCP session (event log, not a lasting ban); ban persists, `RevokeAllForAccountAsync`, drops the session, login + reconnect rejected.
- Folklore stubs (`AdminCommandService`, `Role`, `Permission`, `AccessRightEnum`) remain unused (ADR-0003).
- Tests: `Frog.Tests/Phase9ModerationTests.cs` (unit + in-memory TCP) and `tests/Frog.Persistence.IntegrationTests/Phase9ModerationTests.cs` (PG persist + host restart).

## P9-2 Security / permissions

- Wrote concrete `SECURITY_MODEL.md` (actors, assets, trust boundaries, leftover packets, bind/TLS, secrets).
- Permission SoT: table `auth.operators` + `IOperatorDirectory` (not an `auth.accounts` flag). EF migration `20260917223000_AuthOperators`.
- Strengthened `WorldFlagsPatchRequest` rejection (PG enabled **or** production composition).
- Placeholder secret refuse-to-start on public bind; committed `appsettings.json` uses `NOT_A_PRODUCTION_SECRET`.
- Non-loopback bind requires `Server:AllowNonLoopbackBind=true`.
- Tests: `Frog.Tests/Phase9SecurityGateTests.cs`, in-memory WorldFlags production reject, PG operator + WorldFlags TCP.

## P9-3 PostgreSQL backup / restore

- Scripts: `scripts/postgres-backup.sh`, `postgres-restore.sh`, `postgres-verify.sh`, `postgres-backup-restore-smoke.sh`, `postgres-common.sh`, plus Windows `.ps1` mirrors for backup/restore/verify.
- Runbook: [`BACKUP_RESTORE_RUNBOOK.md`](BACKUP_RESTORE_RUNBOOK.md) — dump custom format (`-Fc`) of schemas `auth`, `content`, `ops`, `player`, `world`, plus `public.__EFMigrationsHistory`. Restore onto an empty database; do not migrate first.
- Proof: `tests/Frog.Persistence.IntegrationTests/PostgresBackupRestoreTests.cs` (CI job `postgres-integration` after `postgresql-client` install). Command: `./scripts/postgres-backup-restore-smoke.sh`.
- No MariaDB path. P9-3 adds no EF migrations; dumps include P9-2 `auth.operators` via schema `auth`. **Re-prove restore after P9-1 schema** (`ops.account_sanctions` / `ops.moderation_events`, migration `20260918001424_OpsAccountSanctions`).

## P9-4 Packaging

- Scripts: `scripts/publish-frog.sh` / `.ps1` (RID layouts `server-linux-x64`, `server-win-x64`, `client-win-x64`, `editor-win-x64`), `scripts/run-packaged-server.sh` / `.ps1`, `scripts/packaged-server-smoke.sh`.
- Framework-dependent, not single-file (runtime load of `Frog.Persistence.PostgreSql.dll`). `CopyPostgreSqlRuntime.targets` publishes the PG sidecar as portable `net8.0` (`RemoveProperties` so a host RID does not break restore). `appsettings.Local.json` is never published (`CopyToPublishDirectory=Never`).
- Docs: concrete `PACKAGING_GUIDE.md` (commands, artifact paths, overlay, protocol v10) and `OPERATIONS_RUNBOOK.md` (start/stop, content root, migrate-then-need-a-published-map).
- Proof: existing `PackagedServerPostgreSqlProcessTests` plus layout assertions (example overlay present; Local overlay absent). Client/editor launch remains Windows CI smokes — no Linux GUI claim.
- Tiny CI tweak on the existing `postgres-integration` job: `./scripts/packaged-server-smoke.sh --layout-only`. No new job. No invented CI URL.

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
