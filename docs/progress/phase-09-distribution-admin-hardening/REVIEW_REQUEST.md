# Phase 9 — REVIEW_REQUEST

**Status:** not ready for acceptance review. P9-0 planning bootstrap only.

## Identity

| Item | Value |
| --- | --- |
| Branch | `cursor/phase9-distribution-admin-hardening` |
| Start / audit tip | `5af47b9cf6ba18a82dba5eee933fc1d0e6afa3eb` |
| Phase 8 | ACCEPTED on main (`1cd57ba`) |
| Baseline CI (main) | https://github.com/Netsuno/MMO_Maker/actions/runs/35286923042 |
| Phase 9 CI on this branch | **none yet** |
| Phase 9 | **IN PROGRESS** (P9-0) |

## What a later reviewer should check

- [ ] P9-S still deferred (no guild/group/trade delivery claim)
- [ ] P9-1 mute/kick/ban is server-authoritative and persisted in PostgreSQL
- [ ] P9-2 security model matches implementation
- [ ] P9-3 restore proven
- [ ] P9-4 package starts without MariaDB
- [ ] P9-5 load numbers are measured
- [ ] Phase 8 suites still green; no speculative Phase 8 edits
- [ ] Evidence uses real SHAs and real CI URLs

Do not merge. Orchestrator owns the draft PR.
