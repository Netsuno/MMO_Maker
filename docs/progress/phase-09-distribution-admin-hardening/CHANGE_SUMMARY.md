# Phase 9 — CHANGE_SUMMARY

**Status:** P9-0…P9-6 landed on `cursor/phase9-distribution-admin-hardening`. Product tip `66fa070` CI https://github.com/Netsuno/MMO_Maker/actions/runs/35291880532 **SUCCESS**. Phase 9 **READY**. P9-S **DEFERRED**.

## P9-0 (bootstrap)

- Added `docs/progress/phase-09-distribution-admin-hardening/` planning pack.
- Updated `docs/STATUS.md`: Phase 8 ACCEPTED on main; Phase 9 IN PROGRESS (later READY).
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
- Proof: `tests/Frog.Persistence.IntegrationTests/PostgresBackupRestoreTests.cs` (in CI PG **180** on `66fa070`). Command: `./scripts/postgres-backup-restore-smoke.sh`.
- No MariaDB path. After P9-1, CI included `PostgresBackupRestoreTests` on schema `OpsAccountSanctions`. Dedicated dump with mute/ban **rows** remains optional.

## P9-4 Packaging

- Scripts: `scripts/publish-frog.sh` / `.ps1` (RID layouts `server-linux-x64`, `server-win-x64`, `client-win-x64`, `editor-win-x64`), `scripts/run-packaged-server.sh` / `.ps1`, `scripts/packaged-server-smoke.sh`.
- Framework-dependent, not single-file (runtime load of `Frog.Persistence.PostgreSql.dll`). `CopyPostgreSqlRuntime.targets` publishes the PG sidecar as portable `net8.0`. `appsettings.Local.json` is never published (`CopyToPublishDirectory=Never`).
- Docs: concrete `PACKAGING_GUIDE.md` and `OPERATIONS_RUNBOOK.md`.
- Proof: `PackagedServerPostgreSqlProcessTests` plus CI `layout-only smoke OK`. Client/editor launch: Windows CI smokes on this run (**87×3** / **6×3** / Phase 8 **24×3**).

## P9-5 Load / observability

- Harness: `tools/Frog.LoadHarness` + `scripts/run-load-harness.sh`.
- Ops: `ServerOpsMetrics` counters + structured `ops_metrics` (EventId 5030). Optional `FROG_OPS_METRICS_PATH` JSON snapshot. No HTTP `/metrics`.
- Measured on the P9-5 agent: **200 TCP Hello**, **100 authed mixed** in-memory. See [`LOAD_REPORT.md`](LOAD_REPORT.md).
- Tests: `Frog.Tests/Phase9OpsMetricsTests.cs`, `tests/Frog.Persistence.IntegrationTests/PostgresLoadObservabilityTests.cs` (included in CI **436** / **180**).

## P9-6 Evidence

- Filled `TEST_PLAN.md`, `E2E_MATRIX.md`, `PHASE_REPORT.md`, `REVIEW_REQUEST.md`, `KNOWN_ISSUES.md`, this file, `TASK_MATRIX.md`, and `docs/STATUS.md`.
- Pinned real SHA `66fa070` and real CI URL https://github.com/Netsuno/MMO_Maker/actions/runs/35291880532 **SUCCESS**. Counts from logs: 436 / 180 / 87×3 / 6×3 / 24×3.
- Verdict: **READY**. Suggested PR #7 body lives in `REVIEW_REQUEST.md` for Orchestrator.
- Phase 8 screenshot scripts / `SCREENSHOT_MANIFEST.md` unchanged. **No Phase 8 regression found.**

## Out of scope (must stay empty)

- Phase 8 product behavior
- VB6 / `.fcc` import
- New MariaDB
- Phase 10
- P9-S social features
