# Phase 9 — PHASE_REPORT

**Status:** Phase 9 is **READY**. P9-0…P9-6 **DONE**. Evidence pack tip `8bf6f08` CI https://github.com/Netsuno/MMO_Maker/actions/runs/35292542956 **SUCCESS** (product `66fa070` / 35291880532 also SUCCESS). P9-S remains **DEFERRED**. Residuals below are honest, not hidden blockers.

| Tranche | Status |
| --- | --- |
| P9-0 Audit + plan | **DONE** |
| P9-1 Admin / moderation | **DONE** |
| P9-2 Security / permissions | **DONE** |
| P9-3 PostgreSQL backup / restore | **DONE** (optional residual: dedicated dump **with sanction rows**) |
| P9-4 Packaging | **DONE** (client/editor launch proven on Windows CI this run) |
| P9-5 Load / observability | **DONE** (see `LOAD_REPORT.md`; some BASELINE_AUDIT §10 rows **explicitly not certified**) |
| P9-6 Tests + review | **DONE** |
| P9-S Guilds / groups / trades | **DEFERRED** |

| Item | Value |
| --- | --- |
| Branch | `cursor/phase9-distribution-admin-hardening` |
| Draft PR | https://github.com/Netsuno/MMO_Maker/pull/7 (Orchestrator-owned; do not open a second PR) |
| Audit / start tip | `5af47b9cf6ba18a82dba5eee933fc1d0e6afa3eb` |
| Phase 8 on main | ACCEPTED — merge `1cd57ba` |
| Product tip | `66fa070b07352c5ee0429234bb31470e10608805` / https://github.com/Netsuno/MMO_Maker/actions/runs/35291880532 **SUCCESS** |
| Evidence pack tip | `8bf6f088cc002ba8957d062952843a82706600a1` / https://github.com/Netsuno/MMO_Maker/actions/runs/35292542956 **SUCCESS** |
| `build-and-test` (`8bf6f08`) | **success** 9m41s — job 105438279174 |
| `postgres-integration` (`8bf6f08`) | **success** 4m54s — job 105438279075 |
| Prior completed CI on this PR | https://github.com/Netsuno/MMO_Maker/actions/runs/35288813093 SUCCESS — **P9-0 docs-only** SHA `6f178e9` |
| Baseline CI (main, start SHA) | https://github.com/Netsuno/MMO_Maker/actions/runs/35286923042 SUCCESS |
| Protocol | v10 (unchanged) |
| Frog.Tests (CI log) | **Passed: 436** / Total tests: 436 |
| PG integration (CI log) | **Passed: 180** / Total tests: 180 |
| Editor smoke | **87×3** |
| Gameplay smoke | **6×3** |
| Phase 8 smoke | **24×3** + 12-file exact-sha manifest OK |
| Layout smoke | `layout-only smoke OK` |

Do not merge. Do not start Phase 10. Orchestrator owns the user-facing gate phrase.

## TASK_MATRIX acceptance

| Lot | Acceptance (short) | Verdict |
| --- | --- | --- |
| P9-0 | Folder + STATUS + P9-S decided | **Met** |
| P9-1 | Operator mute/kick/ban, PG persist, server-authoritative, no grant opcode | **Met** (CI **436** / **180**) |
| P9-2 | Threat model, `auth.operators`, leftover packets, bind/secret gates | **Met** |
| P9-3 | Documented dump/restore of `auth,content,ops,player,world`; restore proven | **Met** — `PostgresBackupRestoreTests` in CI **180** after `OpsAccountSanctions`. Dedicated sanction-row dump **optional residual**. |
| P9-4 | Repeatable publish layout; no MariaDB; no `.fcc` importer | **Met** (CI layout-only + packaged PG process test; WinForms launch = this Windows job) |
| P9-5 | Measured load; BASELINE §10 certified **or revised** | **Met** — 200 Hello / 100 authed mixed in-memory; idle 300 s, economy TPS, interact, restart-reconnect, PG pool size, PG×100 **not certified** (`LOAD_REPORT.md`) |
| P9-6 | Real SHAs + real CI URLs; Phase 8 regressions listed if found; READY/not READY explicit | **Met** — run 35291880532 SUCCESS; counts from logs; Phase 9 **READY** |
| P9-S | Remain deferred | **Met** (still **DEFERRED**) |

## Phase 8 regressions

**None found** on product tip `66fa070` / CI 35291880532:

- Phase 8 smoke **24×3** PASS; `Phase 8 screenshot manifest verification OK (12 file(s): 12 exact-sha, 0 present-dims; distinct-frame checks passed).`
- `git diff origin/main...HEAD` on Phase 8 screenshot scripts: empty. `SCREENSHOT_MANIFEST.md` not modified (capture tip `fa8c44f`).
- Phase 8 product files: only `COMMAND_CATALOG.md` docs (WorldFlags / operator SoT). No quest/event/interact gameplay edits.
- Client: slash commands in the **existing** chat box; no new layout. Editor smoke **87×3** and gameplay **6×3** still PASS.
- Wire: `ModerateRequest` 78 / `ModerateResult` 79 appended; protocol version still 10.
- PG **180** includes the existing Phase 8 E2E / Interact identity tests.

## Residual risks (not READY blockers)

- Clear-text TCP if `AllowNonLoopbackBind=true` without an external TLS terminator (`SECURITY_MODEL.md` §10). No TLS in-process.
- P9-3: dedicated dump-with-sanction-rows still optional.
- Load: PG concurrent authed certified floor is **4**, not 100. No HTTP `/metrics`.
- Operator grant remains out-of-band SQL (`auth.operators`).
- `docs/DATA_MODEL.md` still maps/tilesets-era vs `FrogDbContext`.
- Historical `// TODO: Implémenter` stubs including `Guild*` remain unused.
- CI annotation (not a test failure): Node.js 20 deprecation on `actions/checkout@v4` / `setup-dotnet@v4` / `upload-artifact@v4`.

## Verdict

**READY** — evidence pack tip `8bf6f08` / https://github.com/Netsuno/MMO_Maker/actions/runs/35292542956 SUCCESS (same counts as product tip `66fa070` / 35291880532). P9-6 **DONE**.
