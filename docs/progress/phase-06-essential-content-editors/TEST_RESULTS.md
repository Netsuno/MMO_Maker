# Phase 6 — TEST RESULTS

## Commands

```bash
dotnet test Frog.Tests/Frog.Tests.csproj -c Release
dotnet test tests/Frog.Persistence.IntegrationTests/Frog.Persistence.IntegrationTests.csproj -c Release
# CI Windows:
dotnet test tests/Frog.Editor.WindowsSmokeTests/Frog.Editor.WindowsSmokeTests.csproj -c Release
# (×3 consecutive in workflow)
```

## Local results (pre-gate)

| Suite | Passed | Failed |
| --- | ---: | ---: |
| Frog.Tests | 240 | 0 |
| Frog.Persistence.IntegrationTests | 31 | 0 |
| Frog.Editor.WindowsSmokeTests | 20 (expected in CI ×3) | 0 |

CI URL and exact Windows counts: https://github.com/Netsuno/MMO_Maker/actions/runs/32674090607 — 240 unit / 31 PostgreSQL / 20 Windows smoke ×3.

## Slice coverage (representative)

| Slice | Unit / session | PostgreSQL | Windows smoke |
| --- | --- | --- | --- |
| Tilesets | `TilesetContentTests` | `PostgresTilesetRepositoryTests` | `GameDataTilesetSmokeTests` |
| NPCs | `NpcContentTests` | `PostgresNpcRepositoryTests` | `GameDataNpcSmokeTests` |
| Items | `ItemContentTests` | `PostgresItemRepositoryTests` | `GameDataItemSmokeTests` |
| Spells | `SpellContentTests` | `PostgresSpellRepositoryTests` | `GameDataSpellSmokeTests` |
| Classes | `ClassContentTests` | `PostgresClassRepositoryTests` | `GameDataClassSmokeTests` |
| Shops | `ShopContentTests` | `PostgresShopRepositoryTests` | `GameDataShopSmokeTests` |
| Resources | `ResourceContentTests` | `PostgresResourceRepositoryTests` | `GameDataResourceSmokeTests` |

## Confirmations

- No secrets/connection strings in failure messages (sanitized persistence errors)
- Phase 7 not started
