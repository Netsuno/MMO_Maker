# Phase 9 — CHANGE_SUMMARY

> **Acceptation datée (2026-09-18).** Merge PR #7 `f74b34c`. Tip produit `cab57b9` (C2 follow-up inclus dans l’acceptation). Corps ci-dessous = historique C-fixes.

**Status (archive pré-acceptation) :** C-fixes after refused gate on tip `5db5f6b`. Phase 9 **NOT READY**. P9-S **DEFERRED**. Prior READY withdrawn. C-fixes **awaiting re-review**. **C2 follow-up in flight / awaiting CI** (ban vs reconnect/login after pre-lock validation; prior kick/ban↔packet and same-account reconnect serialization preserved). Correction product tip `422993b` / CI https://github.com/Netsuno/MMO_Maker/actions/runs/35369587406 **SUCCESS** is the last pinned green; this pass is not gated.

## C-fixes (this pass)

- **C1** Trailing whitespace stripped so `git diff --check origin/main...HEAD` is clean.
- **C2** Kick/ban/logout/idle/peer-disconnect share idempotent `SessionTeardown` (peer `PlayerLeave`, character state save, Phase 8 execution cancel, no double-dispose). Tests: `Phase9SessionTeardownTests`, TCP `Tcp_KickNotifiesPeersSavesStateCancelsExecutionsAndIsIdempotent`.
- **C2 races (in flight / awaiting CI)** Teardown retains the TCP across a concurrent `TryGetActiveSession` Unregister. Same-account login/reconnect/kick serialize on a per-username gate; `CompleteLoginAsync` binds the exact session created for that attempt; `TryRemoveSession` unmaps username only for that session id; `TryDisplaceAndCreateSession` no longer deletes an occupant. Prior tests (failed on `7bf016d`, then passed): `TearDown_RetainsTcpWhenConcurrentPacketUnregistersInactiveSession`, `ConcurrentTearDown_SaveAndCancelOnce_ClosesTcp`, `Tcp_ModerationDuringPlayerPacket_*` (kick + ban), `Tcp_SimultaneousSameAccountReconnects_OneLiveSessionNoOrphan`. **C2 follow-up:** final login/reconnect validation and reconnect-token `ValidateTokenAsync` run inside that same lock; ban apply + token revoke + teardown also run under it (no nested same-username acquire; `TouchAsync` is not a substitute for token validation). New barrier tests (failed on `97fba2e`, then passed): `Tcp_BanDuringValidatedReconnect_RefusesRevokesAndLeavesNoSession`, `Tcp_BanDuringValidatedLogin_RefusesAndLeavesNoSession`. Nested same-lock acquire throws. Local Frog.Tests **454** (was 451).
- **C3** `PlaceholderSecretPolicy` ignores **disabled** backends. PostgreSQL-on + MariaDB-off with MariaDB placeholders must not block a public bind. Enabled backends still fail start on placeholder + public bind. Tests in `Phase9SecurityGateTests` (unit + host composition; PG integration Build).
- **C4** Documentary claims reduced: `publish-frog.ps1` client/editor **launch** is **not proven**. Server Linux packaging remains proven. Windows CI smokes are from-source test hosts.
- **C5** P9-5 is a measurement lot, not a full certification. Unexecuted LOAD_REPORT rows and restore-with-sanction-rows stay **not covered**. STATUS / PHASE_REPORT / REVIEW_REQUEST say **NOT READY**.
- **C6** New/updated tests as above. CI Frog.Tests **445**; PG **181** (was 180). Predecessor FAILURE 35368492352 on `2cb842d` (`postgres-integration`: fake `Host=db.example` at `Build()`). Rewritten as loopback + isolated fixture DSN on `422993b`.

## P9-0 (bootstrap)

- Added `docs/progress/phase-09-distribution-admin-hardening/` planning pack.
- Updated `docs/STATUS.md`: Phase 8 ACCEPTED on main; Phase 9 was IN PROGRESS then (refused) READY; now **NOT READY**.
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
- Proof: `tests/Frog.Persistence.IntegrationTests/PostgresBackupRestoreTests.cs` (in CI PG **181** on `422993b`). Command: `./scripts/postgres-backup-restore-smoke.sh`.
- No MariaDB path. After P9-1, CI included `PostgresBackupRestoreTests` on schema `OpsAccountSanctions`. Dedicated dump with mute/ban **rows** remains optional.

## P9-4 Packaging

- Scripts: `scripts/publish-frog.sh` / `.ps1` (RID layouts `server-linux-x64`, `server-win-x64`, `client-win-x64`, `editor-win-x64`), `scripts/run-packaged-server.sh` / `.ps1`, `scripts/packaged-server-smoke.sh`.
- Framework-dependent, not single-file (runtime load of `Frog.Persistence.PostgreSql.dll`). `CopyPostgreSqlRuntime.targets` publishes the PG sidecar as portable `net8.0`. `appsettings.Local.json` is never published (`CopyToPublishDirectory=Never`).
- Docs: concrete `PACKAGING_GUIDE.md` and `OPERATIONS_RUNBOOK.md`.
- Proof: `PackagedServerPostgreSqlProcessTests` plus CI `layout-only smoke OK`. Packaged client/editor launch from `publish-frog.ps1` is **not proven**. Windows CI smokes are from-source `dotnet test` hosts (**87×3** / **6×3** / Phase 8 **24×3** on 35369587406), not published RID trees.

## P9-5 Load / observability

- Harness: `tools/Frog.LoadHarness` + `scripts/run-load-harness.sh`.
- Ops: `ServerOpsMetrics` counters + structured `ops_metrics` (EventId 5030). Optional `FROG_OPS_METRICS_PATH` JSON snapshot. No HTTP `/metrics`.
- Measured on the P9-5 agent: **200 TCP Hello**, **100 authed mixed** in-memory. See [`LOAD_REPORT.md`](LOAD_REPORT.md).
- Tests: `Frog.Tests/Phase9OpsMetricsTests.cs`, `tests/Frog.Persistence.IntegrationTests/PostgresLoadObservabilityTests.cs` (included in CI **445** / **181**).

## P9-6 Evidence

- Filled `TEST_PLAN.md`, `E2E_MATRIX.md`, `PHASE_REPORT.md`, `REVIEW_REQUEST.md`, `KNOWN_ISSUES.md`, this file, `TASK_MATRIX.md`, and `docs/STATUS.md`.
- Historical pin: SHAs `66fa070` / `8bf6f08` with CI 35291880532 and 35292542956 SUCCESS (Frog.Tests **436** then). That READY was **refused**.
- This pass: C-fixes on `422993b`; CI https://github.com/Netsuno/MMO_Maker/actions/runs/35369587406 **SUCCESS** — `build-and-test` SUCCESS (Frog.Tests **445**, editor **87×3**, gameplay **6×3**, Phase 8 **24×3** + 12 exact-sha); `postgres-integration` SUCCESS (PG **181**, `layout-only smoke OK`). C-fixes **awaiting re-review**.
- Verdict: **NOT READY**. Phase 8 screenshot scripts / `SCREENSHOT_MANIFEST.md` unchanged.

## Out of scope (must stay empty)

- Phase 8 product behavior
- VB6 / `.fcc` import
- New MariaDB
- Phase 10
- P9-S social features
