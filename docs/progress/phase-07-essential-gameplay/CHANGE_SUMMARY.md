# Phase 7 — CHANGE_SUMMARY (P7-G1…G6)

## Starting point
- Prior rejected tip: `67281e3c62eb1943341b162fe1213abb5fc7011a`
- Phase 6 accepted implementation: `99b782f8f205c0161c0bba8838d041714e39947e`
- Prior remediations (P7-FIX / P7-R) preserved on branch.

## Implementation tip (CI green)
- `c1803132522d8dfb31e3a1284755341eb2d243b2`
- CI: https://github.com/Netsuno/MMO_Maker/actions/runs/33036286537

## P7-G1
- Never log auth/reconnect tokens; store privately; smoke + screenshot verification.
- Hide stats editor; reject `CharacterStatsUpdateRequest` in production composition.

## P7-G2
- Replace packet-only/tautological E2E checks with decoded business-state assertions for all 17 steps.

## P7-G3
- Clear EF tracker + rollback on cancel for economy and inventory-transfer transactions.
- Remove double post-commit equipment write after atomic transfer.

## P7-G4
- Scope request ID to character; reject different operation/payload reuse; concurrent race → defined replay/conflict.
- Strengthen combat race: exact XP, monster state, persisted progression; player HP lock.

## P7-G5
- Named inventory/equipment/bank/ground UI; pickup action; NPC combo; strict success smokes.

## P7-G6
- Graceful packaged shutdown (SIGTERM / shutdown file); no `pg_terminate_backend` on success; accurate evidence metadata and screenshot hashes.

## Docs / STATUS
- STATUS remains **CHANGES REQUESTED** until re-review accepts.
- Exact counts: 272 / 97 / 35×3 / 5×3.

## Phase 8
Not started.
