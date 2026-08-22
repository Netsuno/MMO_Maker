# Phase 03 — résumé des changements (corrections gate)

## Smoke Windows

- `tests/Frog.Editor.WindowsSmokeTests/` — STA, MainWindow, timeout 45 s, assertions shell
- `EditorSmokeTestAccess`, `EditorTestHooks`, `FROG_EDITOR_FORCE_IN_MEMORY`
- Chargement thème WPF (`EditorWpfTheme.xaml`) hors `StartupUri` pour ressources menu
- CI Windows : étape smoke après tests unitaires — **PASS** run 32575250906
- `scripts/windows-editor-smoke.ps1` — secours manuel

## Identité MapId

- `IMapRepository` : `MapId`, `LoadByIdAsync`, `SaveMapResult.Success(..., MapId)`
- `MapWorkspaceSession.CurrentMapId` (Guid)
- Migration `ModernMapIdentity` + snapshot EF
- `Tile.WarpTargetMapId` → Guid ; warps PG `target_map_id` FK
- `.fmap` v5 : warp Guid 16 octets

## Éditeur

- Arbre monde par `MapId` ; statut sans LegacyId
- Skip MariaDB au démarrage en mode test

## Docs

- `DATA_MODEL.md`, `EDITOR_WORKSPACE.md`, `STATUS.md`
- Evidence pack phase-03 mis à jour

## Commits de référence

- Implémentation shell initiale : `3fc6530`
- Corrections gate : plage `3fc6530..HEAD`
