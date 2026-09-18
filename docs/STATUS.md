# STATUS

| Phase | Status |
| --- | --- |
| Phase 7 | ACCEPTED on main |
| **Phase 8** | **ACCEPTED on main** (merge `1cd57ba`) |
| **Phase 9** | **READY** (P9-0…P9-6 DONE; P9-S DEFERRED) |

Branch: `cursor/phase9-distribution-admin-hardening` (from `main` tip `5af47b9cf6ba18a82dba5eee933fc1d0e6afa3eb`)
Product tip: `66fa070b07352c5ee0429234bb31470e10608805` — CI https://github.com/Netsuno/MMO_Maker/actions/runs/35291880532 **SUCCESS**
Evidence pack tip: `8bf6f088cc002ba8957d062952843a82706600a1` (`8bf6f08`) — CI https://github.com/Netsuno/MMO_Maker/actions/runs/35292542956 **SUCCESS** (`build-and-test` job 105438279174 9m41s; `postgres-integration` job 105438279075 4m54s). Logs on both tips: Frog.Tests **Passed: 436**; PG **Passed: 180**; editor **87×3**; gameplay **6×3**; Phase 8 **24×3** + 12-file exact-sha; `layout-only smoke OK`.
Draft PR: https://github.com/Netsuno/MMO_Maker/pull/7
Phase 8 acceptance: merge [`1cd57ba`](https://github.com/Netsuno/MMO_Maker/commit/1cd57bad694f530fa5699639f9e63008522507e0) (PR #2). README alignment: PR #6.
Baseline CI **on main** at the branch start SHA: https://github.com/Netsuno/MMO_Maker/actions/runs/35286923042 SUCCESS. Prior main SUCCESS for the Phase 8 merge itself: https://github.com/Netsuno/MMO_Maker/actions/runs/35285230766.

Phase 9 folder: [`docs/progress/phase-09-distribution-admin-hardening/`](progress/phase-09-distribution-admin-hardening/). P9-S (guilds / groups / trades) is **DEFERRED**. `PRD_MMO_Maker_CSharp.md` v2.1 is cited but not present in the repository.

Protocol remains `FrogWireProtocol.Version = 10`. Historical stubs outside Phase 8 are not claimed cleared. Residuals: no TLS / clear-text TCP; optional dump-with-sanction-rows; LOAD_REPORT uncertified rows (idle 300 s, economy TPS, interact, restart-reconnect, PG pool, PG×100). Do not merge; do not start Phase 10.
