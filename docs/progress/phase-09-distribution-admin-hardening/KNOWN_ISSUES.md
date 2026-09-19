# Phase 9 — KNOWN_ISSUES

> **Acceptation datée (2026-09-18).** Phase 9 **ACCEPTED** (merge `f74b34c` / PR #7). Les résidus ci-dessous restent **techniquement vrais** et sont repris par [`../phase-10-beta-release/KNOWN_ISSUES.md`](../phase-10-beta-release/KNOWN_ISSUES.md). Le bandeau « NOT READY / awaiting re-review / Do not start Phase 10 » est l’état **d’avant** l’acceptation.

## Gate (P9-6) — archive pré-acceptation

- Phase 9 is **NOT READY**. Prior READY / gate on tip `5db5f6b` was **withdrawn** (Marc refused despite green CI on evidence pack `8bf6f08` / https://github.com/Netsuno/MMO_Maker/actions/runs/35292542956).
- C1–C6 corrections are on product tip `422993b`. CI https://github.com/Netsuno/MMO_Maker/actions/runs/35369587406 **SUCCESS** (`build-and-test` SUCCESS, `postgres-integration` SUCCESS; Frog.Tests **445** / PG **181** / editor **87×3** / gameplay **6×3** / Phase 8 **24×3** + 12 exact-sha). C-fixes **awaiting re-review**. Predecessor FAILURE: 35368492352 on `2cb842d`.
- C2 follow-up (ban vs reconnect/login after pre-lock validation) is **in flight / awaiting CI**. Last pinned green remains 35369587406 on `422993b`. Do not treat local **454** as gated.
- P9-S remains **DEFERRED**.
- CI annotation (not a test failure, historical): Node.js 20 deprecation on `actions/checkout@v4` / `setup-dotnet@v4` / `upload-artifact@v4`.

## Product residuals (documented, not silent)

- `PRD_MMO_Maker_CSharp.md` v2.1 is cited but **not in the repo**. P9-S used README / BACKLOG / ADRs instead.
- `docs/DATA_MODEL.md` is incomplete vs `FrogDbContext` (still maps/tilesets/`ops.legacy_imports`). Live schemas: `auth`, `content`, `ops`, `player`, `world`. P9-2 added `auth.operators`; P9-1 added `ops.account_sanctions` / `ops.moderation_events` (migration `20260918001424_OpsAccountSanctions`).
- `docs/BACKLOG.md` checkboxes stop at Phase 6.
- ~106 historical `// TODO: Implémenter` stubs remain (including `Guild*`). `AdminCommandService` / `Role` / `Permission` / `AccessRightEnum` are unused folklore (P9-1 uses `ModerationService`). Not a Phase 9 clear-out.
- **No TLS.** Clear-text TCP if `AllowNonLoopbackBind=true` without an external terminator (`SECURITY_MODEL.md` §10).
- Operator mute/kick/ban is implemented (P9-1). Grant remains out-of-band SQL (`auth.operators`).
- After P9-1 migration `20260918001424_OpsAccountSanctions`, restore was re-hit in CI via `PostgresBackupRestoreTests`. That is **not** a dedicated dump-with-sanction-rows campaign. **Not certified.**
- Load: 100 authed mixed **measured in-memory** on a 4-core Linux agent; PG concurrent authed storm was only 4 in `PostgresLoadObservabilityTests`. Idle 300 s, economy TPS, interact burst, restart-reconnect, and PG pool size are **not certified** (`LOAD_REPORT.md`). No HTTP `/metrics`. Do not sell P9-5 as a full hosted-world certification.
- Packaged client/editor launch from `publish-frog.ps1` (`client-win-x64` / `editor-win-x64`) is **not proven**. Windows CI smokes are from-source `dotnet test` hosts. Server Linux packaging **is** proven (`PackagedServerPostgreSqlProcessTests` + layout-only smoke).
- Login rate-limit key is still `RemoteEndPoint` (IP:port); NAT of distinct source ports does not share a window (`SECURITY_MODEL.md` §9).
- CI `concurrency.cancel-in-progress: true` cancelled every PR run between P9-0 SUCCESS (`35288813093` on `6f178e9`) and `66fa070`. Those cancelled URLs are **not** green evidence.

## Phase 8 leftovers (not Phase 9 gates)

See [`../phase-08-quests-events-advanced-creation/KNOWN_ISSUES.md`](../phase-08-quests-events-advanced-creation/KNOWN_ISSUES.md): wait-across-disconnect, loot-table editors, arbitrary scripting, MariaDB map-event files.

Phase 8 screenshot SHA gates were **not** weakened.

## P9-S

Guilds / groups / trades remain **DEFERRED**.
