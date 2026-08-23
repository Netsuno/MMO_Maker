# Phase 6 — Slice 6: Shops

## Status

**COMPLETE** — shop content authoring, draft/publish persistence and published-only consumption.

## Files / migration

- Domain: `Frog.Core/Models/ShopDefinition.cs`
- Application: shop repository port, published catalog, workspace session and in-memory implementation
- Persistence: shop drafts, immutable snapshots/history, JSONB listings and `PostgresShopRepository`
- Migration: `20260823231902_ShopDraftPublish`
- Editor: `ShopEditorPanel`, repository factory and Windows smoke hook
- Server: `PublishedShopConsumer` (published-only load)
- Tests: `ShopContentTests`, `PostgresShopRepositoryTests`, `GameDataShopSmokeTests`

## Features

- Stable `Guid` identity, optional description and authored listing collection
- Listings store published item `Guid` values with a non-negative price and optional non-negative stock (`null` means unlimited)
- Draft saves and publications reject missing or unpublished item references
- Item deletion is blocked while any shop draft or immutable published snapshot references the item
- The editor displays published item names while persisting their stable `Guid` identities
- Draft vs immutable published snapshot with transactional publish and rollback
- Optimistic revision conflicts, create/duplicate/delete, search/status filter and live validation
- Server consumption through `IPublishedShopCatalog`; drafts are never returned

## Verification commands

```bash
dotnet test Frog.Tests/Frog.Tests.csproj -c Release --nologo
dotnet test tests/Frog.Persistence.IntegrationTests/Frog.Persistence.IntegrationTests.csproj -c Release --nologo
dotnet build tests/Frog.Editor.WindowsSmokeTests/Frog.Editor.WindowsSmokeTests.csproj \
  -c Release -p:EnableWindowsTargeting=true --nologo
```

## Test results

| Suite | Result |
| --- | ---: |
| Frog.Tests | 234 passed |
| PostgreSQL | 30 passed |
| Windows smoke | project build green; execution delegated to Windows CI |

## Remaining Phase 6 slices

7. Resources/spawns

## Scope boundary / known debt

- Shop buying, selling, currency, inventory transfer and stock mutation gameplay are explicitly deferred to Phase 7.
- Listings are content definitions only; authored stock is not decremented by this slice.
- Windows smoke execution requires Windows CI; Linux verifies that the smoke project cross-builds.
