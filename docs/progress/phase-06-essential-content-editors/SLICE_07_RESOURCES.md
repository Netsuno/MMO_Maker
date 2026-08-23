# Phase 6 — Slice 7: Resources and spawns

## Status

**COMPLETE** — resource and map-spawn content authoring, draft/publish persistence and
published-only consumption. This is the final Phase 6 content slice; the Phase 6 acceptance
gate remains separate.

## Files / migration

- Domain: `Frog.Core/Models/ResourceDefinition.cs`
- Application: resource/spawn repository ports, published catalogs, workspace sessions and
  in-memory implementations
- Persistence: resource/spawn drafts, immutable snapshots/history and PostgreSQL repositories
- Migration: `ResourceDraftPublish`
- Editor: related `ResourceEditorPanel` and `ResourceSpawnEditorPanel` tabs under
  **Ressources / spawns**, repository factories and Windows smoke hook
- Server: `PublishedResourceConsumer` and `PublishedResourceSpawnConsumer`
- Tests: `ResourceContentTests`, `PostgresResourceRepositoryTests`,
  `GameDataResourceSmokeTests`

## Features

- Resource definitions have stable `Guid` identity, optional description, logical sprite path,
  non-negative respawn delay, optional tool item, required yield item and yield quantity 1–999
- Tool and yield references must resolve to published item definitions on draft save and publish
- Resource spawns are independent draft/publish content with stable `Guid` identity, an existing
  map `Guid`, a published resource `Guid` and non-negative tile coordinates
- Item deletion is blocked while any resource draft or immutable published snapshot references
  it; resource deletion is blocked while any spawn draft or snapshot references it
- Draft vs immutable published snapshots with transactional publish and rollback for both content
  types
- Optimistic revision conflicts, create/duplicate/delete, filters and live validation
- Published consumers never return drafts; spawn consumption can be filtered by map

## Verification commands

```bash
dotnet test Frog.Tests/Frog.Tests.csproj -c Release --nologo
dotnet test tests/Frog.Persistence.IntegrationTests/Frog.Persistence.IntegrationTests.csproj \
  -c Release --nologo
dotnet build tests/Frog.Editor.WindowsSmokeTests/Frog.Editor.WindowsSmokeTests.csproj \
  -c Release -p:EnableWindowsTargeting=true --nologo
```

## Expected test inventory

| Suite | Expected after this slice |
| --- | ---: |
| Frog.Tests | 240 |
| PostgreSQL | 31 |
| Windows smoke | project cross-build on Linux; execution delegated to Windows CI |

## Scope boundary

- Harvest interaction, tool use, inventory grants, respawn timers and runtime spawn simulation are
  Phase 7 gameplay work and are not started here.
- Resource spawns are content records; this slice does not mutate map tile payloads.
- Windows smoke execution requires Windows CI; Linux verifies that the smoke project cross-builds.
