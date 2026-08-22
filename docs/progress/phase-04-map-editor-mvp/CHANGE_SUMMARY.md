# Phase 04 — résumé des changements

## Application

- `MapWorkspaceSession` : `IsDirty`, `MarkDirty`, `SaveCurrentAsync`, `ReloadCurrentAsync`

## Éditeur

- `SaveMap` / Ctrl+S → PostgreSQL brouillon
- `PublishMap` → PostgreSQL publié
- `ExportMapToFile` → `.fmap` (export secondaire)
- `WarpDestinationDialog` — configuration carte cible + X/Y
- `MapCanvas.DefaultWarpTargetMapId`, `MapEdited`
- Menus WPF/WinForms mis à jour (PG primaire, MariaDB héritage)

## Persistence

- `PostgresMapRepository` : mise à jour fiable (ExecuteDelete + insert enfants, fix FK warp)

## Core / docs

- Commentaires VB6 obsolètes retirés de `Map.cs`, `TileType.cs`
- `docs/BACKLOG.md` Phase 4 cochée

## Commits de référence

- Implémentation shell Phase 3 : `3fc6530`
- Phase 4 : plage `3fc6530..HEAD`
