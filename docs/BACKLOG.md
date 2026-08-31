# Backlog actif — MMO Maker

Aligné sur `PRD_MMO_Maker_CSharp.md` v2.1 et ADR-0003.

## Hors backlog (différé / non bloquant)

- Import / export `.fcc` FRoG
- `Frog.LegacyImporter`
- Parité VB6 / golden masters FRoG
- Compatibilité protocole VB6
- Nouvelles fonctionnalités MariaDB

## Phase 2 — Clarification

- [x] ADR absence de compatibilité FRoG
- [x] Docs actives sans import `.fcc` critique
- [x] `Frog.Legacy` marqué expérimental
- [x] Matrice MariaDB + ADR WPF
- [x] PostgreSQL confirmé en CI

## Phase 3 — Shell éditeur

- [x] Wireframe / responsabilités (`docs/EDITOR_WORKSPACE.md`)
- [x] Shell : menu, toolbar, arbre monde, canvas, tilesets, couches, propriétés, status
- [x] Catalogue cartes via `IMapRepository` / `MapWorkspaceSession` (`MapId`)
- [x] Smoke test Windows automatisé (`Frog.Editor.WindowsSmokeTests` + CI)
- [x] Migration identité moderne (`ModernMapIdentity`)
- [x] Confirmation CI Windows verte (post-push)

## Phase 5 — Playtest

- [x] Editor-triggered playtest (server + client)
- [x] READY validation / token security / lifecycle
- [x] Accepted at `1944d73b6fffa84799d288da555f1005b82f2698`

## Phase 6 — Essential content editors

- [x] Tilesets
- [x] NPCs and monsters
- [x] Items
- [x] Spells and skills
- [x] Classes
- [x] Shops
- [x] Resources and spawns
