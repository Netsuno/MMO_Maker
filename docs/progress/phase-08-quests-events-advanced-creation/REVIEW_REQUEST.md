# Phase 8 — REVIEW_REQUEST

## Gate phrase

`PHASE 8 GATE REACHED — WAITING FOR RE-REVIEW`

## Identity

| Item | Value |
| --- | --- |
| Branch | `cursor/phase0-baseline-audit-02c7` |
| PR | https://github.com/Netsuno/MMO_Maker/pull/2 |
| Accepted Phase 7 baseline | `3be393b756f32337972432a0571ffabd06a306bb` |
| Prior rejected Phase 8 head | `779d4f9546ac45468c6d33dcdc48917605bc88ef` |
| Remediation tip | `1aec0e4` |
| Phase 9 | **Not started** |

## Remediation checklist (P8-R1 … P8-R5)

| ID | Requirement | Status |
| --- | --- | --- |
| P8-R1 | PostgreSQL production source of truth for all Phase 8 catalogs + craft idempotency | **DONE** — `PostgresPhase8PublishedCatalogs`, `PostgresEventCraftRepository`; in-memory only in explicit playtest/unit path |
| P8-R2 | Authoritative transactional quests + atomic crafting | **DONE** — `PostgresQuestMutationRepository`, objective model, PG tests |
| P8-R3 | Event runtime hardening, WorldFlagsPatch disabled in PG, autorun dispatch | **DONE** — per-execution depth, take_item verify-first, autorun on map entry, execution tracker |
| P8-R4 | Wire protocol, client UI, structured editor | **DONE** — packets 66–74, client panels, Phase8 content browse + structured editors |
| P8-R5 | PG tests, 23-step E2E, multi-client, Windows smoke ×3 | **DONE** — see `E2E_MATRIX.md`, `TEST_RESULTS.md`, CI artifacts |

## Tranche status

| ID | Item | Done |
| --- | --- | --- |
| P8-1 | PostgreSQL event model + editor | Yes |
| P8-2 | Typed event runtime | Yes |
| P8-3 | Dialogues + quests | Yes |
| P8-4 | Professions + recipes | Yes |
| P8-5 | Regions + weather/lighting | Yes |
| P8-6 | Common events + creator tools | Yes |

## CI

Run full workflow on final tip: build Release, unit 297, PG 137, editor smoke ×3, Phase 7 client smoke ×3, **Phase 8 client smoke ×3**.

## Phase 9

Not started.
