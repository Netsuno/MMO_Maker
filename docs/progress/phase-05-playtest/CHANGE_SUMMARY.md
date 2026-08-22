# Phase 5 — CHANGE SUMMARY

## Application

- `PlaytestModels`, `PlaytestMapPreparer`, `PlaytestOrchestrator`, `RuntimeMapIdAllocator`, `PlaytestManifestWriter`
- `IMapRepository.LoadPublishedByIdAndRevisionAsync`
- `InMemoryMapRepository`: create+Publish now sets `PublishedRevision` on draft (parity with update path)

## Persistence

- `PostgresMapRepository.LoadPublishedByIdAndRevisionAsync` loads immutable snapshot by `(MapId, Revision)`

## Server

- Project reference: `Frog.Application` (manifest types only; **no** Persistence/PG)
- `FrogServerHostFactory` + playtest env (`FROG_PLAYTEST_MANIFEST_PATH`, correlation, port)
- `PlaytestMapBlobStore`, `PlaytestRuntimeOptions`
- `PacketDispatcher` playtest spawn + log scopes

## Editor

- Menu/commands: Playtest / Stop Playtest
- `EditorPlaytestProcessLauncher`, server exe resolution, free port, wait-for-ready, owned process kill tree
- Never forwards PostgreSQL connection strings to playtest child processes

## Client

- CLI: `--playtest --host --port --correlation`
- Prefills host/port; window title shows correlation

## Tests

- Unit/orchestration/protocol/E2E in `Frog.Tests`
- PG published-vs-draft pipeline test
- Windows smoke: playtest error (non-durable) + cancel cleanup
