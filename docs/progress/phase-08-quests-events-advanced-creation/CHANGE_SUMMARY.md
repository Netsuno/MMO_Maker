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
- Structured editor: `Phase8ContentBrowseDialog` + dialogue/quest/recipe/region/**profession/weather/common-event** panels (Carte → Contenu Phase 8); Delete + Duplicate; in-memory smoke service


### P8-R5 — Evidence

- PostgreSQL integration tests including full E2E matrix and multi-client scenarios
- Draft invisibility Theory for all Phase 8 content kinds
- `Phase8GameplayClientSmokeTests` + `Phase8EditorSmokeTests` via CI filter `FullyQualifiedName~.Phase8` ×3
- Screenshot gate: `scripts/verify-phase8-screenshot-manifest.ps1` (exact SHA-256 + file list for all 12 rows, no silent rewrite); local refresh `scripts/update-phase8-screenshot-manifest.ps1`
- Prior green pin (historical): CI 34436843321 on `ebc96921` — Frog.Tests **379**; PG integration **159**; Phase8 smoke **24×3**; Editor smoke **56×3**; Gameplay smoke **6×3**
- R2-6: client `01`≠`02` and `03`≠`04` via wait-for-state + panel captures; all committed rows **exact-sha** (TabControl `01`/`06`; deterministic editor GUIDs). Capture tip `fa8c44f` remains the SCREENSHOT_MANIFEST pin (not the implementation tip).

## R2 remediations + P1 + C1–C3 (current head)

Historical prior pins (not current):
- Prior P1 Interact (historical): tip `09e68dfcb86d0b479515d70b13f1bf607afa7926`, CI 35274081277, Editor smoke **85×3**
- P8-G: tip `ebc96921`, CI 34436843321, Frog.Tests **379** / PG **159** / Editor **56×3**

## R2 remediations + P1 (background)

- R2-4: one map-event activation = one PostgreSQL TX with ledger identity; `BeginActivation` wait-resume
- R2-5: quest counters on the public path; common-event page selection proof
- R2-6: screenshot exact-sha gate; capture tip `fa8c44f` kept separate from implementation tip
- Editor: all MainForm closes through coordinator; non-cooperative init proof on `EditorMainFormCloseCoordinatorTests.NonCooperativeInit_*` plus `MainForm_NonCooperativeSave_*`
- **P1** (historical pin): tip `09e68dfcb86d0b479515d70b13f1bf607afa7926`, CI https://github.com/Netsuno/MMO_Maker/actions/runs/35274081277: `InteractRequest` carries `activationId` Guid on public TCP (`FrogWireProtocol.Version = 10`); idempotency via `Phase8InteractIdentityTcpTests` ×6
- **C1–C3** implementation tip `3c36417f320858e950d65e7de120b9f749e74769`, CI https://github.com/Netsuno/MMO_Maker/actions/runs/35280403579: deterministic CommitUnreadReconnect (PG ledger+reward before close); atomic Interact pending-id lock + `FrogGameClient` tests; `git diff --check origin/main...HEAD` PASS
- Current green counts: Frog.Tests **412** (0 skipped); PG integration **174** (0 skipped); Phase8 smoke **24×3**; Editor smoke **87×3**; Gameplay smoke **6×3**; C1–C3 **DONE**

## Preserved foundations (P8-1 … P8-6 initial pass)

- Map event PG draft/publish, world switches/variables, typed command catalog, map event editor entry points
