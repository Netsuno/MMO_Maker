# Phase 9 — REVIEW_REQUEST

**Status:** **NOT READY.** Prior READY on tip `5db5f6b` was **withdrawn** (Marc refused the gate despite green CI on `8bf6f08` / 35292542956). C1–C6 corrections are on this branch. Orchestrator owns draft PR #7. This file does not announce a gate.

## Identity

| Item | Value |
| --- | --- |
| Branch | `cursor/phase9-distribution-admin-hardening` |
| PR | https://github.com/Netsuno/MMO_Maker/pull/7 (draft; Orchestrator-owned) |
| Start / audit tip | `5af47b9cf6ba18a82dba5eee933fc1d0e6afa3eb` |
| Refused gate tip | `5db5f6bf30fee9599a55e42ef2c5ad41fca33dd7` |
| Prior green CI (historical, not this tip) | https://github.com/Netsuno/MMO_Maker/actions/runs/35292542956 SUCCESS on `8bf6f08` |
| Phase 8 | ACCEPTED on main (`1cd57ba`) |
| Baseline CI (main) | https://github.com/Netsuno/MMO_Maker/actions/runs/35286923042 SUCCESS |
| Protocol | `FrogWireProtocol.Version = 10` |
| Phase 9 | **NOT READY** |
| P9-S | **DEFERRED** |
| Local Frog.Tests after C-fixes | **445** PASS (was 436 on `8bf6f08`) |

## Reviewer checklist

- [x] P9-S still deferred (no guild/group/trade delivery claim)
- [x] P9-1 mute/kick/ban is server-authoritative (`IOperatorDirectory`) and persisted in PostgreSQL (`ops.account_sanctions` / `ops.moderation_events`)
- [x] C2: kick/ban/disconnect share idempotent `SessionTeardown` (peer `PlayerLeave`, state save, Phase 8 cancel, no double-dispose)
- [x] C3: disabled-backend placeholders do not block start; enabled backends still gated on public bind
- [x] P9-2 `SECURITY_MODEL.md` matches implementation (placeholder secrets, non-loopback flag, WorldFlags reject)
- [ ] P9-3 restore with **real sanction rows** — **not covered** (schema restore only)
- [x] P9-4 packaged **server** starts without MariaDB (`PackagedServerPostgreSqlProcessTests` + layout-only smoke)
- [ ] P9-4 packaged **client/editor** launch from `publish-frog.ps1` — **not proven**
- [x] P9-5 load numbers are measured (`LOAD_REPORT.md`); uncertified rows are explicit; **not** a full certification
- [ ] CI SUCCESS on **this** correction tip (not yet pinned)
- [x] Phase 8 screenshot SHA gate not weakened
- [ ] No Phase 10 / no merge without Marc (process; still true)

## Suggested PR #7 body note (Orchestrator)

Do not open a second PR. READY is withdrawn. C-fixes: trailing whitespace; shared session teardown; disabled-backend secret policy; honest packaging/load status.

## What not to do

- Do not merge.
- Do not start Phase 10.
- Do not invent a CI URL.
- Do not weaken Phase 8 screenshot SHA gates.
- Do not claim Phase 9 READY from this file.
