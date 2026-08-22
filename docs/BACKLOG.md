# Backlog actif — MMO Maker

Aligné sur `PRD_MMO_Maker_CSharp.md` v2.1 et ADR-0003.

## Hors backlog (différé / non bloquant)

- Import / export `.fcc` FRoG
- `Frog.LegacyImporter`
- Parité VB6 / golden masters FRoG
- Compatibilité protocole VB6
- Nouvelles fonctionnalités MariaDB

## Phase 2 — Clarification (cette phase)

- [x] ADR absence de compatibilité FRoG
- [x] Docs actives sans import `.fcc` critique
- [x] `Frog.Legacy` marqué expérimental
- [x] Matrice MariaDB + ADR WPF
- [x] PostgreSQL confirmé en CI

## Phase 3 — Shell éditeur (prochaine, non commencée)

- Wireframe / responsabilités des panneaux
- Shell WinForms/WPF existant : arbre, canvas, tilesets, couches, propriétés, status
- Smoke test Windows
- Pas d’accès DB dans les formulaires

## Phase 4 — Map Editor MVP

- Création / peinture / collision / warp / undo
- Sauvegarde + publication PostgreSQL via `Frog.Application`
- Playtest déclenché depuis l’éditeur (Phase 5)
