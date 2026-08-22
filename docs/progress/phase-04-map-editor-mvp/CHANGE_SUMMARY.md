# Phase 04 — résumé des changements (gate data safety)

## Application

- `MapRepositoryCapabilities` : `IsDurablePersistence`, `AllowsSave`, libellés UI
- `SaveMapIntent`, `SaveMapResult.NotDurable`, `SaveMapResult.PersistenceFailed`
- `MapWorkspaceSession` : `CanPersist`, mutex save, `SaveCurrentAsync(SaveMapIntent)`, init démo locale sans fausse persistance
- `MapWarpValidator`, `MapEditOperations` (logique testable sans UI)
- `IMapRepository` : `LoadPublishedByIdAsync`, `ListPublicationHistoryAsync`

## Éditeur

- `IEditorDialogService` injectable (smoke + tests)
- Save/Publish : pas de `_ =` silencieux, état occupé, menus désactivés si non persistant
- Prompts Enregistrer / Ignorer / Annuler (changement carte, nouvelle carte, ouverture fichier, fermeture)
- Dirty + undo sur PropertyGrid, visibilité couche, opérations couche
- `WarpDestinationDialog` : limites X/Y selon carte cible sélectionnée

## Persistence

- Migration `DraftPublishSeparation` : snapshots publiés immuables + historique
- `PostgresMapRepository` : `ExecuteUpdate` atomique sur `Id + Revision`, validation warp avant écriture
- `InMemoryMapRepository` : séparation draft/publish pour tests

## Tests

- `MapPersistenceModeTests`, `MapEditOperationsTests`
- PG : concurrence 2 DbContext, draft/publish immuable, warp hors limites
- Smoke : 1 test via `MainForm.SaveMapAsync()`

## Plage

- Gate Phase 3 : `20eedc1`
- Phase 4 : `20eedc1..HEAD`
