# Phase 04 — Map Editor MVP

## Identification

- Date : 2026-08-22
- Branche : `cursor/phase0-baseline-audit-02c7`
- Commit d’implémentation Phase 3 (immuable) : `3fc6530`
- Plage revue : `3fc6530..HEAD` (Phase 4 Map Editor MVP)
- PR : #2

## Verdict proposé

**READY FOR REVIEW** — peinture/collision/warp/undo opérationnels ; sauvegarde et publication PostgreSQL branchées ; tests verts.

## Livré

| Exigence | Livrable |
| --- | --- |
| Peinture / collision / warp / undo | `MapCanvas` (existant) + événement `MapEdited` |
| Sauvegarde PostgreSQL (Draft) | `MapWorkspaceSession.SaveCurrentAsync` + `MainForm.SaveMap` |
| Publication PostgreSQL | `MainForm.PublishMap` → `MapPublishStatus.Published` |
| Warp destination | `WarpDestinationDialog` + défaut `DefaultWarpTargetMapId` |
| État modifié | `IsDirty`, prompt changement catalogue |
| Export fichier | `ExportMapToFile` (.fmap, secondaire) |
| Fix persistence | `PostgresMapRepository` mise à jour enfants (ExecuteDelete + insert) |

## Non commencé (Phase 5+)

- Playtest serveur depuis carte publiée PG
- Nouvelles fonctions MariaDB

## Décision requise

Accepter Phase 4 et autoriser Phase 5 (playtest), ou demander ajustements UX.
