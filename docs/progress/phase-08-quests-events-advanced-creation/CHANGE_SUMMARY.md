# Phase 8 — CHANGE_SUMMARY

## Remediation (P8-R1 … P8-R5)

### P8-R1 — PostgreSQL production source of truth

- Unified tables: `content.phase8_content_definitions`, published snapshots, publication history
- `PostgresPhase8PublishedCatalogs` implements all seven published catalogs + `IPhase8ContentEditorRepository`
- Production composition no longer registers `Phase8InMemoryPublishedContent` when PostgreSQL is enabled
- `PostgresEventCraftRepository` with idempotency via `player.event_craft_requests`

### P8-R2 — Transactional quests and crafting

- Quest objectives: talk, kill, collect, visit, craft with counters in `character_quest_progress.objective_counters_json`
- `PostgresQuestMutationRepository` — atomic turn-in (gold + inventory + progress + idempotency) in one transaction
- `QuestGameplayService` objective auto-progress hooks and journal wire builder

### P8-R3 — Event runtime

- Per-execution `CommonEventDepth`, step budget, branch depth; `take_item` verifies full quantity first
- `MapEventExecutionTracker` for autorun/parallel deduplication
- Autorun dispatch on character select / map entry via `Phase8GameplayHandlers`
- `WorldFlagsPatchRequest` rejected in PostgreSQL production mode

### P8-R4 — Protocol, client, editor

- Wire packets 66–74: dialogue, quest journal, turn-in, craft, environment
- Client panels: `DialoguePanel`, `QuestJournalPanel`, `CraftPanel`, `EnvironmentPanel` (+ Quêtes tab)
- Structured editor: `Phase8ContentBrowseDialog` + dialogue/quest/recipe/region panels (Carte → Contenu Phase 8)

### P8-R5 — Evidence

- 18 new PostgreSQL integration tests including full 23-step E2E and 8 multi-client scenarios
- `Phase8GameplayClientSmokeTests` ×3 in CI with screenshot artifacts

## Preserved foundations (P8-1 … P8-6 initial pass)

- Map event PG draft/publish, world switches/variables, typed command catalog, map event editor entry points
