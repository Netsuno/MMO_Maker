# STATUS

| Phase | Status |
| --- | --- |
| Phase 7 | ACCEPTED |
| **Phase 8** | **READY FOR RE-REVIEW** (evidence synced, awaiting external re-review) |
| Phase 9 | Not started |

Branch: `cursor/phase0-baseline-audit-02c7` (PR https://github.com/Netsuno/MMO_Maker/pull/2)
Implementation tip: `09e68dfcb86d0b479515d70b13f1bf607afa7926` (P1 Interact identity; current head vs prior `ebc96921`)
Capture/manifest tip: `fa8c44f` (SCREENSHOT_MANIFEST SHA-256 pin — keep separate from the implementation tip)
CI: https://github.com/Netsuno/MMO_Maker/actions/runs/35274081277

P8-G1–G5 then R2-4…R2-6 then **P1 Interact identity** (current head). Public TCP: `InteractRequest` carries `activationId` Guid (`FrogWireProtocol.Version = 10`); idempotency proven by `Phase8InteractIdentityTcpTests` ×6. Green CI counts: **412** Frog.Tests / **174** PG integration / Phase8 smoke **24×3** / Editor smoke **85×3** / Gameplay smoke **6×3**. Capture evidence remains on `fa8c44f` (client `01`≠`02` and `03`≠`04`, all 12 rows exact-sha). Historical stubs/debt outside Phase 8 (unused client Models/Services TODOs, MariaDB map-event legacy) are not claimed cleared. Prior pin `ebc96921` / CI 34436843321 (379 / 159 / Editor 56×3) is superseded. Do not merge; Phase 9 not started.
