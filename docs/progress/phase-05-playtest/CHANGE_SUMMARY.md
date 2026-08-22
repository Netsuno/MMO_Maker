# Phase 5 — CHANGE SUMMARY (incl. rejection corrections)

## Application

- `PlaytestModels`, `PlaytestMapPreparer` (save-new/dirty → publish → BFS warp closure), `PlaytestOrchestrator` (temp cleanup), `RuntimeMapIdAllocator`, `PlaytestManifestWriter`
- `PlaytestChildEnvironment` — shared sanitize + child env-name probe (no secret values)
- `PlaytestSpawnValidator` — bounds + passability
- `IMapRepository.LoadPublishedByIdAndRevisionAsync`

## Persistence

- `PostgresMapRepository.LoadPublishedByIdAndRevisionAsync`
- PG tests: published-vs-draft + brand-new unsaved map playtest prepare

## Server

- `FrogServerHostFactory` + playtest env (`FROG_PLAYTEST_*` incl. `BIND_ADDRESS=127.0.0.1`)
- `PlaytestMapBlobStore`, `PlaytestRuntimeOptions`
- `PacketDispatcher` playtest spawn + correlated log scopes
- **No** Persistence/PG reference on playtest path

## Editor

- Playtest / Stop Playtest; `PlaytestSpawnDialog` (tile X/Y)
- `EditorPlaytestProcessLauncher`: sanitize server+client env; async stdout/stderr; Hello readiness; owned-only kill; await exit; correlated bounded logs
- Smoke: `OverrideSpawnTile` hook

## Client

- CLI: `--playtest --host --port --correlation`

## Tests

- Secret probe (server+client roles)
- New-map + warp graph unit suite
- Spawn validator cases
- Real `dotnet Frog.Server.dll` process lifecycle
- TCP framing fragmentation / invalid sizes / version mismatch
- TCP E2E move/block/two-warps/map request/shutdown
- Windows smoke playtest error + cancel
