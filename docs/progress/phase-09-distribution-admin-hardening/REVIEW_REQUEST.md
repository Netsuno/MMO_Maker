# Phase 9 — REVIEW_REQUEST

**Status:** **READY** for acceptance review. P9-0…P9-6 **DONE**. Product tip `66fa070` CI https://github.com/Netsuno/MMO_Maker/actions/runs/35291880532 **SUCCESS**. P9-S **DEFERRED**. Orchestrator owns draft PR #7 and the user-facing gate phrase — this file must not announce it.

## Identity

| Item | Value |
| --- | --- |
| Branch | `cursor/phase9-distribution-admin-hardening` |
| PR | https://github.com/Netsuno/MMO_Maker/pull/7 (draft; Orchestrator-owned) |
| Start / audit tip | `5af47b9cf6ba18a82dba5eee933fc1d0e6afa3eb` |
| Product tip (CI measured) | `66fa070b07352c5ee0429234bb31470e10608805` |
| Phase 8 | ACCEPTED on main (`1cd57ba`) |
| Baseline CI (main) | https://github.com/Netsuno/MMO_Maker/actions/runs/35286923042 SUCCESS |
| Phase 9 CI | https://github.com/Netsuno/MMO_Maker/actions/runs/35291880532 **SUCCESS** |
| `build-and-test` | success — https://github.com/Netsuno/MMO_Maker/actions/runs/35291880532/job/105436343236 |
| `postgres-integration` | success — https://github.com/Netsuno/MMO_Maker/actions/runs/35291880532/job/105436343549 |
| Counts (CI logs) | Frog.Tests **436**; PG **180**; editor **87×3**; gameplay **6×3**; Phase 8 **24×3** + 12 exact-sha |
| Protocol | `FrogWireProtocol.Version = 10` |
| Phase 9 | **READY** |
| P9-S | **DEFERRED** |

## Reviewer checklist

- [x] P9-S still deferred (no guild/group/trade delivery claim)
- [x] P9-1 mute/kick/ban is server-authoritative (`IOperatorDirectory`) and persisted in PostgreSQL (`ops.account_sanctions` / `ops.moderation_events`)
- [x] P9-2 `SECURITY_MODEL.md` matches implementation (placeholder secrets, non-loopback flag, WorldFlags reject)
- [x] P9-3 restore proven (`PostgresBackupRestoreTests` in CI **180**); optional residual: dump **with sanction rows** not required for this gate
- [x] P9-4 package starts without MariaDB (`PackagedServerPostgreSqlProcessTests` + CI `layout-only smoke OK`)
- [x] P9-5 load numbers are measured (`LOAD_REPORT.md`); revised (not certified) rows are explicit
- [x] Phase 8 suites green on `66fa070`; screenshot SHA gate not weakened (12 exact-sha OK)
- [x] Evidence uses real SHAs and real CI URLs (run **35291880532** SUCCESS)
- [ ] No Phase 10 / no merge without Marc (process; still true)

## Suggested PR #7 body update (Orchestrator)

Do not open a second PR. Optional replacement for the current draft body (live body still lists lots as “P9-1 packaging”; product lots are P9-1 moderation / P9-2 security / P9-3 backup / P9-4 packaging):

```markdown
## Summary
Phase 9 delivery branch: packaging, admin moderation (mute/kick/ban), production security hardening, PostgreSQL backup/restore, load certification, and gate evidence.

**Baseline:** `main` `5af47b9` / CI https://github.com/Netsuno/MMO_Maker/actions/runs/35286923042 SUCCESS
**Product tip:** `66fa070` / CI https://github.com/Netsuno/MMO_Maker/actions/runs/35291880532 SUCCESS
**Counts (CI logs):** Frog.Tests 436 · PG 180 · editor 87×3 · gameplay 6×3 · Phase 8 24×3 (12 exact-sha)

## Status
- Phase 8 ACCEPTED on `main` (`1cd57ba`)
- Phase 9 **READY** — P9-0…P9-6 DONE; P9-S DEFERRED
- No merge / no Phase 10 without Marc authorization
- Gate phrase is Orchestrator-owned

## Lots
P9-0 ✅ plan · P9-1 ✅ mute/kick/ban · P9-2 ✅ security · P9-3 ✅ backup/restore · P9-4 ✅ packaging · P9-5 ✅ load · P9-6 ✅ evidence · P9-S deferred

## Residuals (honest, not hidden)
- Dedicated `pg_dump` payload with mute/ban **rows** still optional (schema restore already in CI `PostgresBackupRestoreTests` after `20260918001424_OpsAccountSanctions`)
- LOAD_REPORT: idle 300 s, economy TPS, interact burst, restart-reconnect, PG pool size, PG×100 authed **not certified**
- Clear-text TCP; no in-process TLS
- `docs/DATA_MODEL.md` still incomplete vs `FrogDbContext`

## Test plan
- [x] CI green on product tip `66fa070` (Windows `build-and-test` + Ubuntu `postgres-integration`)
- [x] Phase 8 screenshot SHA gate still exact-sha (`scripts/verify-phase8-screenshot-manifest.ps1`)
- [x] Per-lot tests per `TASK_MATRIX.md`
```

## What not to do

- Do not merge.
- Do not start Phase 10.
- Do not invent a second CI URL or a Passed! count.
- Do not weaken Phase 8 screenshot SHA gates.
