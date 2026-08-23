# Phase 6 — Slice 2: NPCs and monsters

## Status

Implementation complete; local verification pending.

## Files / migration

- Domain: `Frog.Core/Models/NpcDefinition.cs`
- Application: NPC repository port, published catalog, workspace session and in-memory implementation
- Persistence: NPC draft entities, immutable snapshots/history and `PostgresNpcRepository`
- Migration: `20260823224117_NpcDraftPublish`
- Editor: `NpcEditorPanel`, repository factory and Windows smoke hook
- Server: `PublishedNpcConsumer` (published-only load)
- Tests: `NpcContentTests`, `PostgresNpcRepositoryTests`, `GameDataNpcSmokeTests`

## Features

- Stable `Guid` identity and NPC/monster kind
- Sprite logical path, level 1–99, optional notes and optional integer `EditorAliasId`
- Draft vs immutable published snapshot with transactional publish and rollback
- Optimistic revision conflicts, search/filter, create/duplicate and live validation
- Delete blocked when `world.map_npc_spawns.npc_definition_id` references `EditorAliasId`
- Server consumption through `IPublishedNpcCatalog`; drafts are never returned

## Verification commands

```bash
dotnet test Frog.Tests/Frog.Tests.csproj -c Release
dotnet test tests/Frog.Persistence.IntegrationTests/Frog.Persistence.IntegrationTests.csproj -c Release
# Windows CI: Frog.Editor.WindowsSmokeTests
```

## Remaining Phase 6 slices

3. Items → 4. Spells/skills → 5. Classes → 6. Shops → 7. Resources/spawns

## Known debt

- Map NPC spawns retain their legacy integer definition id; `EditorAliasId` bridges it to stable GUID content.
- Map tooling does not yet expose NPC spawn painting/editing.
- Sprite preview loads are not part of this slice; the editor stores and validates logical paths.
- Phase 7 gameplay remains out of scope and has not started.
