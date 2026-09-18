# STATUS

| Phase | Status |
| --- | --- |
| Phase 7 | ACCEPTED on main |
| **Phase 8** | **ACCEPTED on main** (merge `1cd57ba`) |
| **Phase 9** | **IN PROGRESS** (P9-0 DONE, P9-1 DONE, P9-2 DONE, P9-3 DONE, P9-4 DONE; P9-5/P9-6 not started; P9-S DEFERRED) |

Branch: `cursor/phase9-distribution-admin-hardening` (from `main` tip `5af47b9cf6ba18a82dba5eee933fc1d0e6afa3eb`)
Phase 8 acceptance: merge [`1cd57ba`](https://github.com/Netsuno/MMO_Maker/commit/1cd57bad694f530fa5699639f9e63008522507e0) (PR #2). README alignment: PR #6.
Baseline CI **on main** at the branch start SHA: https://github.com/Netsuno/MMO_Maker/actions/runs/35286923042 SUCCESS. Prior main SUCCESS for the Phase 8 merge itself: https://github.com/Netsuno/MMO_Maker/actions/runs/35285230766. **No CI URL is claimed for later commits on this branch** (`ci.yml` runs on `main`/`master` pushes and PRs to those branches only).

Phase 9 folder: [`docs/progress/phase-09-distribution-admin-hardening/`](progress/phase-09-distribution-admin-hardening/). P9-S (guilds / groups / trades) is **DEFERRED** — current in-repo PRD-aligned docs assign Phase 9 to packaging, admin (mute/kick/ban), prod security, and load certification (`README.md` roadmap + chat table; Phase 8 `KNOWN_ISSUES.md`). `PRD_MMO_Maker_CSharp.md` v2.1 is cited but not present in the repository.

Protocol remains `FrogWireProtocol.Version = 10`. Phase 8 acceptance counts (historical, on main): Frog.Tests **412** / PG integration **174** / Phase8 smoke **24×3** / Editor smoke **87×3** / Gameplay smoke **6×3**. Historical stubs outside Phase 8 are not claimed cleared. Do not merge; do not start Phase 10.
