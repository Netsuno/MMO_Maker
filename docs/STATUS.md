# STATUS

| Phase | Status |
| --- | --- |
| Phase 7 | ACCEPTED on main |
| **Phase 8** | **ACCEPTED on main** (merge `1cd57ba`) |
| **Phase 9** | **NOT READY** (C-fixes landed; CI green on `422993b`; **C2 follow-up in flight / awaiting CI**; C-fixes **awaiting re-review**). P9-S **DEFERRED**. Prior READY / gate on tip `5db5f6b` was **withdrawn**. |

Branch: `cursor/phase9-distribution-admin-hardening` (from `main` tip `5af47b9cf6ba18a82dba5eee933fc1d0e6afa3eb`)
Refused gate tip: `5db5f6bf30fee9599a55e42ef2c5ad41fca33dd7` — CI https://github.com/Netsuno/MMO_Maker/actions/runs/35292542956 SUCCESS on evidence pack `8bf6f08` (historical; READY was refused).
Correction product tip: `422993b6f779df076b8061c85bca3523017081d5` (`422993b`).
Correction CI: https://github.com/Netsuno/MMO_Maker/actions/runs/35369587406 **SUCCESS** on `422993b`.
- `build-and-test` **SUCCESS** (job [105680123329](https://github.com/Netsuno/MMO_Maker/actions/runs/35369587406/job/105680123329)): Frog.Tests **445**; editor **87×3**; gameplay **6×3**; Phase 8 **24×3** + 12 exact-sha
- `postgres-integration` **SUCCESS** (job [105680123069](https://github.com/Netsuno/MMO_Maker/actions/runs/35369587406/job/105680123069)): PG **181**; `layout-only smoke OK`
C2 follow-up (ban vs reconnect/login after pre-lock validation; kick/ban↔packet and same-account reconnect serialization preserved): **in flight / awaiting CI** on this branch tip. Local Release: Frog.Tests **454** (was 451). Do **not** treat this as gated. Orchestrator owns re-review after CI on the exact tip.
Predecessor FAILURE on `2cb842d`: https://github.com/Netsuno/MMO_Maker/actions/runs/35368492352 (`build-and-test` SUCCESS / `postgres-integration` FAILURE — fake `Host=db.example` at PG `Build()`). Fixed on `422993b`.
Draft PR: https://github.com/Netsuno/MMO_Maker/pull/7
Phase 8 acceptance: merge [`1cd57ba`](https://github.com/Netsuno/MMO_Maker/commit/1cd57bad694f530fa5699639f9e63008522507e0) (PR #2). README alignment: PR #6.
Baseline CI **on main** at the branch start SHA: https://github.com/Netsuno/MMO_Maker/actions/runs/35286923042 SUCCESS.

Phase 9 folder: [`docs/progress/phase-09-distribution-admin-hardening/`](progress/phase-09-distribution-admin-hardening/). P9-S (guilds / groups / trades) is **DEFERRED**. `PRD_MMO_Maker_CSharp.md` v2.1 is cited but not present in the repository.

Protocol remains `FrogWireProtocol.Version = 10`. Historical stubs outside Phase 8 are not claimed cleared. Residuals: no TLS / clear-text TCP; restore **with real sanction rows** not covered; LOAD_REPORT uncertified rows (idle 300 s, economy TPS, interact, restart-reconnect, PG pool, PG×100); packaged client/editor launch from `publish-frog.ps1` **not proven**. Do not merge; do not start Phase 10.
