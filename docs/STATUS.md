# STATUS

| Phase | Status |
| --- | --- |
| Phase 7 | ACCEPTED |
| **Phase 8** | **READY FOR RE-REVIEW** (evidence synced, awaiting external re-review) |
| Phase 9 | Not started |

Branch: `cursor/phase0-baseline-audit-02c7` (PR https://github.com/Netsuno/MMO_Maker/pull/2)
Implementation tip: `ebc96921`
Manifest tip: `9ddbd5e`
CI: https://github.com/Netsuno/MMO_Maker/actions/runs/34436843321

P8-G1–G5 re-review remediation: atomic once-grant (`TryClaimSwitchAsync`), map movement clear on map change, condition parameter validation, async editor PostgreSQL init/disposal, map-event once-reward same-character race E2E, editor close-during-block smoke assertions. Green CI counts: 379 Frog.Tests / 159 PG integration / Phase8 smoke 24×3 / Editor smoke 56×3 / Gameplay smoke 6×3. Residual non-blocking gap: client captures `01`/`02` and `03`/`04` still share SHA-256 (identical smoke frames). Do not merge; Phase 9 not started.
