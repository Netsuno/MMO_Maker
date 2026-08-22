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

## Phase 4 — Map Editor MVP

- [x] Peinture / collision / warp / undo (canvas existant)
- [x] Sauvegarde + publication PostgreSQL via session (draft / publish séparés)
- [x] Corrections gate data safety (mémoire, concurrence, prompts, tests)
- [x] Dialogue destination warp (limites carte cible)
- [x] Tests unitaires session + édition + smoke save + intégration PG
- [ ] Playtest déclenché depuis l’éditeur (Phase 5)
