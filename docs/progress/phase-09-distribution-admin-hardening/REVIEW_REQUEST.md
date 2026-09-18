# Phase 9 — REVIEW_REQUEST

> **Acceptation datée (2026-09-18).** Phase 9 **ACCEPTED**, PR #7 fusionnée (`f74b34c`). Ce fichier reste l’archive de la demande de re-revue C-fixes. Ne plus le lire comme « n’ouvrez pas Phase 10 » : Phase 10 P10-0 est le chantier actif.

**Status (archive pré-acceptation) :** **NOT READY.** Prior READY on tip `5db5f6b` was **withdrawn** (Marc refused the gate despite green CI on `8bf6f08` / 35292542956). C1–C6 corrections are on product tip `422993b` with green CI 35369587406. C2 follow-up (ban vs pre-lock reconnect/login) is local-only until CI on the exact tip. C-fixes **awaiting re-review**. Orchestrator owns draft PR #7. This file does not announce a gate.

## Identity

| Item | Value |
| --- | --- |
| Branch | `cursor/phase9-distribution-admin-hardening` |
| PR | https://github.com/Netsuno/MMO_Maker/pull/7 (draft; Orchestrator-owned) |
| Start / audit tip | `5af47b9cf6ba18a82dba5eee933fc1d0e6afa3eb` |
| Refused gate tip | `5db5f6bf30fee9599a55e42ef2c5ad41fca33dd7` |
| Correction product tip | `422993b6f779df076b8061c85bca3523017081d5` (`422993b`) |
| Correction CI | https://github.com/Netsuno/MMO_Maker/actions/runs/35369587406 **SUCCESS** on `422993b` |
| Job conclusions | `build-and-test` **SUCCESS** ([105680123329](https://github.com/Netsuno/MMO_Maker/actions/runs/35369587406/job/105680123329)); `postgres-integration` **SUCCESS** ([105680123069](https://github.com/Netsuno/MMO_Maker/actions/runs/35369587406/job/105680123069)) |
| Passed! counts (35369587406) | Frog.Tests **445**; PG **181**; editor **87×3**; gameplay **6×3**; Phase 8 **24×3** + 12 exact-sha; `layout-only smoke OK` |
| Prior green CI (historical, not this tip) | https://github.com/Netsuno/MMO_Maker/actions/runs/35292542956 SUCCESS on `8bf6f08` |
| Phase 8 | ACCEPTED on main (`1cd57ba`) |
| Baseline CI (main) | https://github.com/Netsuno/MMO_Maker/actions/runs/35286923042 SUCCESS |
| Protocol | `FrogWireProtocol.Version = 10` |
| Phase 9 | **NOT READY** |
| P9-S | **DEFERRED** |
| C-fixes | Documented; **awaiting re-review** |

## Reviewer checklist

- [x] P9-S still deferred (no guild/group/trade delivery claim)
- [x] P9-1 mute/kick/ban is server-authoritative (`IOperatorDirectory`) and persisted in PostgreSQL (`ops.account_sanctions` / `ops.moderation_events`)
- [x] C2: kick/ban/disconnect share idempotent `SessionTeardown` (peer `PlayerLeave`, state save, Phase 8 cancel, no double-dispose)
- [ ] C2 follow-up: ban vs pre-lock reconnect/login under the same per-username lock — **local tests passed; awaiting CI on the exact tip**
- [x] C3: disabled-backend placeholders do not block start; enabled backends still gated on public bind
- [x] P9-2 `SECURITY_MODEL.md` matches implementation (placeholder secrets, non-loopback flag, WorldFlags reject)
- [ ] P9-3 restore with **real sanction rows** — **not covered** (schema restore only)
- [x] P9-4 packaged **server** starts without MariaDB (`PackagedServerPostgreSqlProcessTests` + layout-only smoke)
- [ ] P9-4 packaged **client/editor** launch from `publish-frog.ps1` — **not proven**
- [x] P9-5 load numbers are measured (`LOAD_REPORT.md`); uncertified rows are explicit; **not** a full certification
- [x] CI SUCCESS on correction product tip `422993b` — https://github.com/Netsuno/MMO_Maker/actions/runs/35369587406 (`build-and-test` SUCCESS, `postgres-integration` SUCCESS; Frog.Tests **445** / PG **181** / editor **87×3** / gameplay **6×3** / Phase 8 **24×3** + 12 exact-sha). C-fixes **awaiting re-review**.
- [x] Phase 8 screenshot SHA gate not weakened
- [ ] No Phase 10 / no merge without Marc (process; still true)

## Suggested PR #7 body note (Orchestrator)

Do not open a second PR. READY is withdrawn. C-fixes: trailing whitespace; shared session teardown; disabled-backend secret policy; honest packaging/load status. CI 35369587406 SUCCESS on `422993b`. Phase 9 **NOT READY**. C-fixes **awaiting re-review**.

## What not to do

- Do not merge.
- Do not start Phase 10.
- Do not invent a CI URL. Correction product tip is `422993b` / 35369587406.
- Do not weaken Phase 8 screenshot SHA gates.
- Do not claim Phase 9 READY from this file.
