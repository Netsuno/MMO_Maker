# Phase 6 — Slice 4: Spells and skills

## Status

**COMPLETE** — implemented in `2af8db2420a04dbce2120b43141e841c1af6ee02`.

## Files / migration

- Domain: `Frog.Core/Models/SpellDefinition.cs`, `SpellKind` and `TargetType`
- Application: spell repository port, published catalog, workspace session and in-memory implementation
- Persistence: spell drafts, immutable snapshots/history and `PostgresSpellRepository`
- Migration: `20260823225941_SpellDraftPublish`
- Editor: `SpellEditorPanel`, repository factory and Windows smoke hook
- Server: `PublishedSpellConsumer` (published-only load)
- Tests: `SpellContentTests`, `PostgresSpellRepositoryTests`, `GameDataSpellSmokeTests`

## Features

- Stable `Guid` identity with authored Spell/Skill and target types
- Non-negative mana cost and cooldown, logical icon path and optional description
- Draft vs immutable published snapshot with transactional publish and rollback
- Optimistic revision conflicts, create/duplicate/delete, search/status filter and live validation
- Server consumption through `IPublishedSpellCatalog`; drafts are never returned

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
| Frog.Tests | 223 passed |
| PostgreSQL | 26 passed |
| Windows smoke | project build green (0 warnings/errors); execution delegated to Windows CI |

## Remaining Phase 6 slices

5. Classes → 6. Shops → 7. Resources/spawns

## Known debt

- Spell effects and gameplay execution are outside this content-authoring slice.
- Icon preview loading is not part of this slice; the editor stores and validates logical paths.
- Phase 7 gameplay remains out of scope and has not started.
