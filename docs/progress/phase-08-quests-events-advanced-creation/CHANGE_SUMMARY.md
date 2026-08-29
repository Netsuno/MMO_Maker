# Phase 8 — CHANGE_SUMMARY

## Starting point

- Phase 7 accepted code tip: `2f107b3cdb9a677a00992b2296262c78eaff7c6a`
- Phase 8 starting tip: `3be393b756f32337972432a0571ffabd06a306bb`
- Starting CI: https://github.com/Netsuno/MMO_Maker/actions/runs/33254661855

## Transition (first commit)

- `docs/STATUS.md` — Phase 7 ACCEPTED; Phase 8 IN PROGRESS with baseline.
- `README.md` — roadmap aligned to PRD: Phase 8 = quests/events/advanced creation; Phase 9 = packaging/admin.
- Created `docs/progress/phase-08-quests-events-advanced-creation/` dossier.

## P8-1 foundation (in progress)

- Core: `MapEventDefinition`, pages, typed conditions/commands, Phase 8 trigger kinds, runtime limits.
- PostgreSQL: `map_event_definitions`, snapshots, `map_event_placements`, published placements on map publish.
- `PostgresMapEventRepository`, `IPublishedMapEventPlacementCatalog`, `PublishedMapEventStoreAdapter`.
- Production host selects PostgreSQL event store when PG enabled (MariaDB path isolated).
- Tests: `MapEventDefinitionValidationTests` (+5 unit); `PostgresMapEventRepositoryTests` (+2 PG).

## P8-1 editor + client cleanup

- `EditorMapEventRepositoryFactory`, `MapEventsPostgreSqlService` — catalogue/placements via PostgreSQL (Guid map id, Phase 8 triggers).
- `MapEventsBrowseDialog`, `MainForm`, `MainWindow` — événements carte branchés sur la carte catalogue courante (`CurrentMapId`), sans MariaDB ni `script_key`.
- Marqueurs canevas/mini-carte : styles Phase 8 (`action`, `player_contact`, `autorun`, `parallel`).
- Client : bouton démo `WorldFlagsPatchRequest` retiré (`MainShellForm`).

## P8-2 runtime foundation (in progress)

- `MapEventRuntimeService` — sélection page Phase 8, conditions `character_switch`, commandes `show_text` + `set_switch`
- `ICharacterWorldStateRepository` + table PG `player.character_world_switches`
- `HandleInteractRequestAsync` délègue à l'interpréteur quand définition publiée disponible
- Tests : `MapEventParameterSchemasTests`, `MapEventRuntimeServiceTests` (+5 unit), `PostgresCharacterWorldStateRepositoryTests` (+1 PG)

## Explicit non-goals (Phase 8)

- Arbitrary Lua/C#/PowerShell execution
- Guilds, parties, player trading, auction houses
- Packaging, deployment, backup/restore (Phase 9)
- Instances or procedural worlds

## Phase 9

Not started.
