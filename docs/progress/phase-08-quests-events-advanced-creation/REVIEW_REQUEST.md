# Phase 8 — REVIEW_REQUEST

## Status

READY FOR RE-REVIEW pending external — evidence synced, awaiting external re-review.

J6 / R2 / P1 evidence pins are coherent on the implementation tip below. Capture/manifest tip stays `fa8c44f` (not overwritten). Do not merge. Phase 9 not started.

## Identity

| Item | Value |
| --- | --- |
| Branch | `cursor/phase0-baseline-audit-02c7` |
| PR | https://github.com/Netsuno/MMO_Maker/pull/2 |
| Accepted Phase 7 baseline | `3be393b756f32337972432a0571ffabd06a306bb` |
| Prior rejected head | `a9bd0898c1e9a2bfd266c5d8741592a3f8bae4c4` |
| Prior P1 Interact pin (historical) | `09e68dfcb86d0b479515d70b13f1bf607afa7926` / CI 35274081277 (412 / 174 / Editor 85×3) |
| Prior P8-G evidence pin (historical) | `ebc96921d8e40f1ddf2779dddd50cecd39bb4d45` / CI 34436843321 (379 / 159 / Editor 56×3) |
| Implementation tip | `3c36417f320858e950d65e7de120b9f749e74769` (C1–C3; vs prior `09e68df` / `ebc96921`) |
| Capture/manifest tip | `fa8c44f937e729e6004481c651a7bf957585d50e` (SCREENSHOT_MANIFEST; keep separate) |
| CI (green) | https://github.com/Netsuno/MMO_Maker/actions/runs/35280403579 |
| Protocol | `FrogWireProtocol.Version = 10` (`InteractRequest` Guid `activationId`) |
| Phase 9 | **Not started** |

## Remediation checklist (P8-G1 … P8-G5)

| ID | Requirement | Status |
| --- | --- | --- |
| P8-G1 | Event runtime (movement, parallel, validation, idempotency) | **DONE** |
| P8-G2 | Dialogue revision + craft replay + profession acquisition | **DONE** |
| P8-G3 | Editor close state machine + structured map event editor | **DONE** |
| P8-G4 | E2E matrix + functional client smoke (network) | **DONE** |
| P8-G5 | Evidence hygiene (412 Frog.Tests, 174 PG integration, manifest) | **DONE** |
| R2-4…R2-6 | Unified activation TX, quest counters / CE pages, screenshot exact-sha | **DONE** |
| P1 | Interact identity on public TCP (`activationId` Guid, v10) | **DONE** (current head) |

## Evidence

- Frog.Tests: **412** passed, **0** skipped
- PostgreSQL integration: **174** passed, **0** skipped (includes `Phase8InteractIdentityTcpTests` ×6)
- Phase8 smoke: **24×3** PASS
- Editor smoke: **87×3** PASS (was 85×3 / 56×3)
- Gameplay smoke: **6×3** PASS
- Protocol: `FrogWireProtocol.Version = 10` — `InteractRequest` carries `activationId` Guid; public TCP idempotency via `Phase8InteractIdentityTcpTests`
- Unit: `MapEventMovementServiceTests`, `MapEventExecutionTrackerTests`, `MapEventRuntimeServiceTests`, `MapEventPageSelectorTests`, `MapEventCommandParameterValidatorTests`, `DialogSessionServiceTests`
- Integration: Phase 8 E2E 23-step, multi-client ×9 (incl. `MapEventOnceRewardRace_SameCharacter_ExactlyOneItem`), craft/quest PG repos, `Phase8InteractIdentityTcpTests`
- Windows: `Phase8GameplayClientSmokeTests` (functional network), `Phase8EditorSmokeTests` (close during blocked save), `MainFormLifecycleSmokeTests` (init cancel + close-during-save + `MainForm_NonCooperativeSave_*` + dispose-once + ActiveScopeCount→0) ×3; non-cooperative init on pure coordinator `EditorMainFormCloseCoordinatorTests.NonCooperativeInit_*`
- R2-6: CI verifies Phase 8 screenshot SHA-256 against committed `SCREENSHOT_MANIFEST.md` (exact-sha, all 12 files); client `01`≠`02` and `03`≠`04` via wait-for-state + panel captures. Capture tip `fa8c44f` is not the implementation tip.
