# Phase 03 — résumé des changements

## Documents

- `docs/EDITOR_WORKSPACE.md` — wireframe et responsabilités
- `docs/ARCHITECTURE.md`, `STATUS.md`, `BACKLOG.md` — Phase 3

## Application

- `IMapRepository.ListSummariesAsync` + `MapCatalogEntry`
- `DemoMapFactory`, `InMemoryMapRepository`, `MapWorkspaceSession`

## Persistence

- `PostgresMapRepository.ListSummariesAsync`

## Éditeur

- Références Application + Persistence (composition)
- `EditorMapRepositoryFactory` (PG ou mémoire)
- Arbre « Monde » catalogue ; toolbar ; titres MMO Maker
- Persistance largeurs colonnes ; init workspace au Loaded
- Menus MariaDB relabelés « héritage »

## Tests

- `MapWorkspaceSessionTests` (4)
- Architecture : Editor → Application, pas Legacy
- Intégration : ListSummaries
