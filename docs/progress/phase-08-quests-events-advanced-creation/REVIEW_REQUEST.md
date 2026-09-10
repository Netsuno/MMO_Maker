# Phase 8 — REVIEW_REQUEST

## Status

READY FOR RE-REVIEW pending external — evidence synced, awaiting external re-review.

J6 evidence pins are coherent on the implementation tip below. Do not merge. Phase 9 not started.

## Identity

| Item | Value |
| --- | --- |
| Branch | `cursor/phase0-baseline-audit-02c7` |
| PR | https://github.com/Netsuno/MMO_Maker/pull/2 |
| Accepted Phase 7 baseline | `3be393b756f32337972432a0571ffabd06a306bb` |
| Prior rejected head | `a9bd0898c1e9a2bfd266c5d8741592a3f8bae4c4` |
| Implementation tip | `ebc96921d8e40f1ddf2779dddd50cecd39bb4d45` |
| Manifest tip | `9ddbd5ee0d015e0d60afce5d9f4c3d214b970ecb` |
| Narrative tip | this commit (J6-EVIDENCE-01 / J6-DOCS-01) |
| CI (green) | https://github.com/Netsuno/MMO_Maker/actions/runs/34436843321 |
| Phase 9 | **Not started** |

## Remediation checklist (P8-G1 … P8-G5)

| ID | Requirement | Status |
| --- | --- | --- |
| P8-G1 | Event runtime (movement, parallel, validation, idempotency) | **DONE** |
| P8-G2 | Dialogue revision + craft replay + profession acquisition | **DONE** |
| P8-G3 | Editor close state machine + structured map event editor | **DONE** |
| P8-G4 | E2E matrix + functional client smoke (network) | **DONE** |
| P8-G5 | Evidence hygiene (379 Frog.Tests, 159 PG integration, manifest) | **DONE** |

## Evidence

- Frog.Tests: **379** passed, **0** skipped
- PostgreSQL integration: **159** passed, **0** skipped
- Phase8 smoke: **24×3** PASS
- Editor smoke: **56×3** PASS
- Gameplay smoke: **6×3** PASS
- Unit: `MapEventMovementServiceTests`, `MapEventExecutionTrackerTests`, `MapEventRuntimeServiceTests`, `MapEventPageSelectorTests`, `MapEventCommandParameterValidatorTests`, `DialogSessionServiceTests`
- Integration: Phase 8 E2E 23-step, multi-client ×9 (incl. `MapEventOnceRewardRace_SameCharacter_ExactlyOneItem`), craft/quest PG repos
- Windows: `Phase8GameplayClientSmokeTests` (functional network), `Phase8EditorSmokeTests` (close during blocked save), `MainFormLifecycleSmokeTests` (init cancel + close-during-save + dispose-once + ActiveScopeCount→0) ×3
- Residual non-blocking gap: client captures `01`/`02` and `03`/`04` still share SHA-256 (identical smoke frames). Do not invent new screenshots in this narrative sync.
