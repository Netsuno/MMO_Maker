# Phase 8 — Known Issues

## Deferred (not Phase 8 gate requirements unless required by a vertical slice)

- Generalized loot-table editors
- Roles/permissions beyond event command authority scopes
- Arbitrary user scripting runtime (requires separate threat model)
- Phase 9 packaging, admin moderation, load certification

## Legacy (P8-1 retirement target)

- `MariaDbMapEventStore`, `MapEventsMariaDbReader`, `MapEventsMariaDbWriter` — MariaDB event path to be isolated from production composition.
- `script_key` on legacy catalog — metadata only; must not execute.
- `WorldFlagsPatchRequest` client demo — to be removed/disabled; switches/variables only via validated server commands.

## Phase 9

Not started.
