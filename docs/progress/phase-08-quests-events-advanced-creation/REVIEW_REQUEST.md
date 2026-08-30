# Phase 8 — REVIEW_REQUEST

## Gate phrase

`PHASE 8 GATE REACHED — WAITING FOR RE-REVIEW`

## Identity

| Item | Value |
| --- | --- |
| Branch | `cursor/phase0-baseline-audit-02c7` |
| PR | https://github.com/Netsuno/MMO_Maker/pull/2 |
| Accepted Phase 7 baseline | `3be393b756f32337972432a0571ffabd06a306bb` |
| Prior rejected head | `a9bd0898c1e9a2bfd266c5d8741592a3f8bae4c4` |
| Final evidence tip | pending green CI on exact tip (implementation includes P8-I1 close-during-save + P8-I5 mid-progress reconnect/replay) |
| CI (green) | pending — do not treat cancelled/partial runs as green |
| Phase 9 | **Not started** |

## Remediation checklist (P8-G1 … P8-G5)

| ID | Requirement | Status |
| --- | --- | --- |
| P8-G1 | Event runtime (movement, parallel, validation, idempotency) | **DONE** |
| P8-G2 | Dialogue revision + craft replay + profession acquisition | **DONE** |
| P8-G3 | Editor close state machine + structured map event editor | **DONE** |
| P8-G4 | E2E matrix + functional client smoke (network) | **DONE** |
| P8-G5 | Evidence hygiene (321 Frog.Tests, 144 PG integration, manifest) | **DONE** |

## Evidence

- Frog.Tests: **321** passed, **0** skipped
- PostgreSQL integration: **144** passed, **0** skipped
- Unit: `MapEventMovementServiceTests`, `MapEventExecutionTrackerTests`, `MapEventRuntimeServiceTests`, `MapEventPageSelectorTests`, `MapEventCommandParameterValidatorTests`, `DialogSessionServiceTests`
- Integration: Phase 8 E2E 23-step, multi-client ×9 (incl. `MapEventOnceRewardRace_SameCharacter_ExactlyOneItem`), craft/quest PG repos
- Windows: `Phase8GameplayClientSmokeTests` (functional network), `Phase8EditorSmokeTests` (close during blocked save, form alive until cancel) ×3
