# Phase 6 — Test Results (Third Fix Pass)

## Implementation SHA

`b3fe913`

## CI (implementation)

https://github.com/Netsuno/MMO_Maker/actions/runs/32793426334 — **success**

## Verified counts

| Suite | Count |
| --- | ---: |
| Frog.Tests (unit) | 244 |
| PostgreSQL integration | 38 |
| Windows smoke | 29 × 3 consecutive passes |

## UI smoke matrix (real controls)

| Editor | Create | Edit | Duplicate | Save | Publish | Invalid publish | Search/filter | Close/reopen | Cancel dirty nav | Delete | Protected delete |
| --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- | --- |
| Tilesets | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | — |
| NPCs | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | — |
| Items | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | shop listing |
| Spells | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | class reference |
| Classes | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | — |
| Shops | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | item listing |
| Resources | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | spawn reference |
| Resource Spawns | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | ✓ | — |

Additional: initial tileset category; spawn map/resource “Toutes…” reset; panel lifecycle STA/close; init-failure scope dispose.

## Preview / UI screenshots

`docs/progress/phase-06-essential-content-editors/screenshots/`

- `tileset-preview-smoke.png`
- `npc-preview-smoke.png`
- `item-preview-smoke.png`
- `spell-preview-smoke.png`
- `resource-preview-smoke.png`

## PostgreSQL lifecycle

- `PostgresGameDataScopeLifecycleTests` on `EditorPostgreSqlScope` (migrate once, dispose once, drain, cancel, repeated open/close)
- `PostgresGameDataConcurrencyTests` retained

## Phase 7

Not started.
