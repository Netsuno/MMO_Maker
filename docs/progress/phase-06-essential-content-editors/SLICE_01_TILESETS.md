# Phase 6 — Slice 1: Tilesets (COMPLETE)

## Status

**COMPLETE** — committed on `cursor/phase0-baseline-audit-02c7`

## Commit

See git history after this file is committed (slice tip recorded in `docs/STATUS.md`).

## Files / migrations

- Domain: `Frog.Core/Models/TilesetDefinition.cs`
- Application: `Frog.Application/Content/*` (`ITilesetRepository`, session, in-memory)
- Persistence: expanded `TilesetEntity`, snapshots/history, `PostgresTilesetRepository`
- Migration: `20260823223136_TilesetDraftPublish`
- Editor: `Forms/GameData/GameDataForm.cs`, menu « Données de jeu… »
- Server: `PublishedTilesetConsumer` (published-only load)
- Tests: `TilesetContentTests`, `PostgresTilesetRepositoryTests`, `GameDataTilesetSmokeTests`

## Features

- Stable Guid IDs; optional `EditorPaletteId` for map painting
- Draft vs published snapshots; transactional save/publish; rollback on failure
- Search/filter; create/duplicate; live validation; dirty close protection
- Delete blocked when map cells reference palette id
- Server consumer loads published catalog only (no secrets in logs)

## Commands

```bash
dotnet test Frog.Tests/Frog.Tests.csproj -c Release
dotnet test tests/Frog.Persistence.IntegrationTests -c Release
# Windows CI: Frog.Editor.WindowsSmokeTests (14 × 3 expected after this slice)
```

## Test results (local)

| Suite | Passed |
| --- | ---: |
| Frog.Tests | 204 |
| PostgreSQL | 20 |
| Windows smoke | +1 tileset smoke (CI) |

## Remaining Phase 6 slices

2. NPCs and monsters → 3. Items → 4. Spells/skills → 5. Classes → 6. Shops → 7. Resources/spawns

## Known issues / debt

- Map tiles still store int palette ids (not Guid FK); reference check is JSON text search
- Game Data categories beyond Tilesets show placeholder labels
- Sprite preview in tileset form not yet wired to disk assets (SHA/path only)
- Phase 7 gameplay not started
