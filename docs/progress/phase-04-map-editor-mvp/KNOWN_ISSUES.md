# Phase 04 — problèmes connus

## Acceptés pour gate

- Export `.fmap` reste un chemin secondaire (non PostgreSQL).
- Publication MariaDB reste héritage / hors scope Phase 4.
- Undo/redo testé via `MapEditOperations` + session ; pas de replay souris automatisé.

## Hors scope Phase 4

- Playtest serveur (Phase 5).
- Chargement runtime de la dernière révision publiée côté serveur (Phase 5).
