# Phase 6 — Slice 5: Classes

## Status

**COMPLETE** — class authoring, draft/publish persistence and published-only consumption.

## Files / migration

- Domain: `Frog.Core/Models/ClassDefinition.cs`
- Application: class repository port, published catalog, workspace session and in-memory implementation
- Persistence: class drafts, immutable snapshots/history and `PostgresClassRepository`
- Migration: `20260823230826_ClassDraftPublish`
- Editor: `ClassEditorPanel`, repository factory and Windows smoke hook
- Server: `PublishedClassConsumer` (published-only load)
- Tests: `ClassContentTests`, `PostgresClassRepositoryTests`, `GameDataClassSmokeTests`

## Features

- Stable `Guid` identity, optional description, positive base HP/MP and STR/AGI/VIT/INT/DEX/LUCK in the 1–99 range
- Optional starting spell selected from the published spell catalog
- Repository validation rejects class draft saves and publications when the starting spell is missing or unpublished
- PostgreSQL prevents deleting a spell referenced by a class draft or published snapshot
- Draft vs immutable published snapshot with transactional publish and rollback
- Optimistic revision conflicts, create/duplicate/delete, search/status filter and live validation
- Server consumption through `IPublishedClassCatalog`; drafts are never returned

## Verification commands

```bash
dotnet test Frog.Tests/Frog.Tests.csproj -c Release --nologo
dotnet test tests/Frog.Persistence.IntegrationTests/Frog.Persistence.IntegrationTests.csproj -c Release --nologo
dotnet build tests/Frog.Editor.WindowsSmokeTests/Frog.Editor.WindowsSmokeTests.csproj \
  -c Release -p:EnableWindowsTargeting=true --nologo
```

## Test results (local)

| Suite | Result |
| --- | ---: |
| Frog.Tests | 228 passed |
| PostgreSQL | 28 passed |
| Windows smoke | project build green; execution delegated to Windows CI |

## Remaining Phase 6 slices

6. Shops → 7. Resources/spawns

## Known debt

- Class progression curves, equipment restrictions and gameplay stat application remain outside this content-authoring slice.
- Starting spells are optional; broader skill trees and multiple initial abilities are not modeled yet.
- Windows smoke execution requires Windows CI; Linux verifies that the smoke project cross-builds.
