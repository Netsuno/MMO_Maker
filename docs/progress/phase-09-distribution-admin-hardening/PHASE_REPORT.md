# Phase 9 — PHASE_REPORT

**Status:** Phase 9 is **NOT READY**. Prior READY on tip `5db5f6b` / evidence pack `8bf6f08` was **withdrawn** (Marc refused the gate despite green CI). C1–C6 corrections are on this branch. P9-S remains **DEFERRED**. Do not treat uncertified load rows or sanction-row restore as certified.

| Tranche | Status |
| --- | --- |
| P9-0 Audit + plan | **DONE** |
| P9-1 Admin / moderation | **DONE** (C2: shared idempotent `SessionTeardown` for kick/ban/disconnect) |
| P9-2 Security / permissions | **DONE** (C3: disabled-backend placeholders no longer block start) |
| P9-3 PostgreSQL backup / restore | **DONE** (schema restore in CI). Restore **with real sanction rows** is **not covered**. |
| P9-4 Packaging | **DONE** for server Linux proofs. Packaged client/editor **launch** from `publish-frog.ps1` is **not proven**. |
| P9-5 Load / observability | **DONE** as a measurement lot — see `LOAD_REPORT.md`. Unexecuted BASELINE_AUDIT §10 rows remain **not certified**. Not a full hosted-world certification. |
| P9-6 Tests + review | **IN PROGRESS** — C-fixes landed; overall Phase 9 **NOT READY** pending CI on this tip and Orchestrator re-review |
| P9-S Guilds / groups / trades | **DEFERRED** |

| Item | Value |
| --- | --- |
| Branch | `cursor/phase9-distribution-admin-hardening` |
| Draft PR | https://github.com/Netsuno/MMO_Maker/pull/7 (Orchestrator-owned; do not open a second PR) |
| Audit / start tip | `5af47b9cf6ba18a82dba5eee933fc1d0e6afa3eb` |
| Phase 8 on main | ACCEPTED — merge `1cd57ba` |
| Refused gate tip | `5db5f6bf30fee9599a55e42ef2c5ad41fca33dd7` |
| Prior green CI (historical) | https://github.com/Netsuno/MMO_Maker/actions/runs/35292542956 SUCCESS on `8bf6f08` (Frog.Tests **436** / PG **180**). New counts after C-fixes: local Frog.Tests **445**. CI URL for the correction tip is not invented here. |
| Protocol | v10 (unchanged) |

Do not merge. Do not start Phase 10.

## TASK_MATRIX acceptance

| Lot | Acceptance (short) | Verdict |
| --- | --- | --- |
| P9-0 | Folder + STATUS + P9-S decided | **Met** |
| P9-1 | Operator mute/kick/ban, PG persist, server-authoritative, no grant opcode | **Met** locally; kick/ban now share `SessionTeardown` (peer leave, save, cancel, idempotent) |
| P9-2 | Threat model, `auth.operators`, leftover packets, bind/secret gates | **Met** — disabled MariaDB placeholders no longer fail a public bind when PostgreSQL is the active backend |
| P9-3 | Documented dump/restore of `auth,content,ops,player,world`; restore proven | **Schema restore met** via `PostgresBackupRestoreTests`. Dedicated dump **with sanction rows** is **not covered** (not certified). |
| P9-4 | Repeatable publish layout; no MariaDB; no `.fcc` importer | **Server layout + Linux process start met**. Packaged WinForms client/editor launch **not proven** (from-source Windows smokes ≠ `publish-frog.ps1` trees). |
| P9-5 | Measured load; BASELINE §10 certified **or revised** | **Measured** 200 Hello / 100 authed mixed in-memory. Idle 300 s, economy TPS, interact, restart-reconnect, PG pool size, PG×100 **not certified**. Do not sell as full certification. |
| P9-6 | Real SHAs + real CI URLs; Phase 8 regressions listed if found; READY/not READY explicit | **IN PROGRESS** — Phase 9 **NOT READY**; CI on the correction tip not pinned yet |
| P9-S | Remain deferred | **Met** (still **DEFERRED**) |

## Phase 8 regressions

None introduced by the C-fixes (product files outside kick teardown / secret policy are unchanged vs the refused tip). Phase 8 screenshot SHA scripts were **not** touched. Re-confirm on the correction-tip CI (`build-and-test` exact-sha).

## Residual risks (READY blockers until re-review)

- Uncertified LOAD_REPORT rows (idle 300 s, economy TPS, interact burst, restart-reconnect, PG pool, PG×100 authed).
- Restore with real mute/ban **rows** not covered.
- Packaged client/editor launch from `publish-frog.ps1` not proven.
- Clear-text TCP if `AllowNonLoopbackBind=true` without an external TLS terminator.
- Operator grant remains out-of-band SQL (`auth.operators`).
- `docs/DATA_MODEL.md` still maps/tilesets-era vs `FrogDbContext`.

## Verdict

**NOT READY** — C-fixes address the refused-gate items. Orchestrator re-review after CI on this tip. No user-facing gate phrase from this file.
