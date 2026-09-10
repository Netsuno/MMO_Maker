# Phase 6 — Slice 3: Items

## Status

**COMPLETE** — implemented in `44585235fc0745cee1a498c264c3452acb4f2ce4`.

## Files / migration

- Domain: `Frog.Core/Models/ItemDefinition.cs` using the stable `Frog.Core.Enums.ItemType`
- Application: item repository port, published catalog, workspace session and in-memory implementation
- Persistence: item drafts, immutable snapshots/history and `PostgresItemRepository`
- Migration: `20260823225349_ItemDraftPublish`
- Editor: `ItemEditorPanel`, repository factory and Windows smoke hook
- Server: `PublishedItemConsumer` (published-only load)
- Tests: `ItemContentTests`, `PostgresItemRepositoryTests`, `GameDataItemSmokeTests`

## Features

- Stable `Guid` identity and authored item types (`Unknown` is rejected)
- Logical icon path, stack limit 1–999, non-negative buy/sell prices and optional description
- Draft vs immutable published snapshot with transactional publish and rollback
- Optimistic revision conflicts, create/duplicate/delete, search/status filter and live validation
- Server consumption through `IPublishedItemCatalog`; drafts are never returned

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
| Frog.Tests | 217 passed |
| PostgreSQL | 24 passed |
| Windows smoke | project build green (0 warnings/errors); execution delegated to Windows CI |

## Remaining Phase 6 slices

4. Spells/skills → 5. Classes → 6. Shops → 7. Resources/spawns

## Known debt

- The legacy integer `Item` gameplay model is not yet migrated to `ItemDefinition` GUID references.
- Icon preview loading is not part of this slice; the editor stores and validates logical paths.
- Phase 7 gameplay remains out of scope and has not started.
