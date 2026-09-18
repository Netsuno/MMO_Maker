# Phase 9 — KNOWN_ISSUES

## Gate (P9-6)

- Phase 9 is **READY**. Product tip `66fa070` CI **SUCCESS**: https://github.com/Netsuno/MMO_Maker/actions/runs/35291880532
  - `build-and-test` success 7m13s (job 105436343236): Frog.Tests **Passed: 436**; editor **87×3**; gameplay **6×3**; Phase 8 **24×3**; 12-file exact-sha OK
  - `postgres-integration` success 4m53s (job 105436343549): **Passed: 180**; `layout-only smoke OK`
- P9-6 is **DONE**. P9-S remains **DEFERRED**.
- CI annotation (not a test failure): Node.js 20 deprecation on `actions/checkout@v4` / `setup-dotnet@v4` / `upload-artifact@v4`.

## Product residuals (documented, not Phase 9 silent failures)

- `PRD_MMO_Maker_CSharp.md` v2.1 is cited but **not in the repo**. P9-S used README / BACKLOG / ADRs instead.
- `docs/DATA_MODEL.md` is incomplete vs `FrogDbContext` (still maps/tilesets/`ops.legacy_imports`). Live schemas: `auth`, `content`, `ops`, `player`, `world`. P9-2 added `auth.operators`; P9-1 added `ops.account_sanctions` / `ops.moderation_events` (migration `20260918001424_OpsAccountSanctions`).
- `docs/BACKLOG.md` checkboxes stop at Phase 6.
- ~106 historical `// TODO: Implémenter` stubs remain (including `Guild*`). `AdminCommandService` / `Role` / `Permission` / `AccessRightEnum` are unused folklore (P9-1 uses `ModerationService`). Not a Phase 9 clear-out.
- **No TLS.** Clear-text TCP if `AllowNonLoopbackBind=true` without an external terminator (`SECURITY_MODEL.md` §10).
- Operator mute/kick/ban is implemented (P9-1). Grant remains out-of-band SQL (`auth.operators`).
- After P9-1 migration `20260918001424_OpsAccountSanctions`, restore was re-hit in CI via `PostgresBackupRestoreTests` (in **180**). That is **not** a dedicated dump-with-sanction-rows campaign. Still optional.
- Load: 100 authed mixed certified **in-memory** on a 4-core Linux agent; PG concurrent authed storm was only 4 in `PostgresLoadObservabilityTests`. Idle 300 s, economy TPS, interact burst, restart-reconnect, and PG pool size are **not** certified (`LOAD_REPORT.md`). No HTTP `/metrics`.
- Login rate-limit key is still `RemoteEndPoint` (IP:port); NAT of distinct source ports does not share a window (`SECURITY_MODEL.md` §9).
- CI `concurrency.cancel-in-progress: true` cancelled every PR run between P9-0 SUCCESS (`35288813093` on `6f178e9`) and `66fa070`. Those cancelled URLs are **not** green evidence.

## Phase 8 leftovers (not Phase 9 gates)

See [`../phase-08-quests-events-advanced-creation/KNOWN_ISSUES.md`](../phase-08-quests-events-advanced-creation/KNOWN_ISSUES.md): wait-across-disconnect, loot-table editors, arbitrary scripting, MariaDB map-event files.

Phase 8 screenshot SHA gates were **not** weakened. **No Phase 8 product regression found** on `66fa070` / CI 35291880532 (24×3 + 12 exact-sha + PG 180 including Phase 8 E2E).

## P9-S

Guilds / groups / trades remain **DEFERRED**.
