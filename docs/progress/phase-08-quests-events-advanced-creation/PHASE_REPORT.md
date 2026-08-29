# Phase 8 — PHASE_REPORT

## Status

**Phase 8: CHANGES REQUESTED** — second remediation pass (P0 gaps vs re-review).

| Tranche | Status |
| --- | --- |
| P8-1 PostgreSQL event model + map authoring | DONE |
| P8-2 Authoritative typed event runtime | DONE (wait resume + parallel uniqueness + async cache) |
| P8-3 Dialogues and quests | DONE (objective hooks: talk/kill/collect/visit/craft) |
| P8-4 Professions and recipes | DONE (craft gold + profession XP in PG transaction) |
| P8-5 Regions, weather and lighting | DONE |
| P8-6 Common events and advanced creator tools | DONE (structured Profession/Weather/CommonEvent editors + cycle detect) |
| P8-R5 E2E / Windows smoke | PENDING CI re-validation on this tip |

## This remediation (vs prior tip `6cad4cc`)

- Quest objective auto-progress wired from dialogue, kill, pickup, visit, craft
- Craft: GoldCost + ProfessionExperienceReward in same PG transaction
- Parallel once-per-map-visit + wait resume on heartbeat
- Common-event cycle detection at publish
- Async map-event catalog with TTL + InvalidateAll (no sync-over-async)
- Structured editors for remaining kinds; draft invisibility Theory ×7 kinds
- Phase 8 editor smoke + client smoke under CI filter `~.Phase8` ×3
- Screenshot manifest hasher script

## Phase 9

Not started.
