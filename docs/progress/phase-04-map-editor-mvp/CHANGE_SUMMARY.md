# Phase 04 — résumé des changements (gate data safety, itération 2)

## Contrat création / mise à jour

- `MapId = null` ou vide → création, id retourné par `SaveMapResult.Success`
- `MapId` existant → mise à jour atomique (`ExpectedRevision`)
- `MapId` inconnu → `Conflict`
- `MapWorkspaceSession.InitializeAsync` : seed démo avec `MapId = null`

## Persistence PostgreSQL

- Fixtures warp : carte cible créée d’abord, coordonnées dans les bornes
- `TestBeforeCommitAsync` → `PersistenceFailed` (pas d’exception)
- `ChangeTracker.Clear()` après `ExecuteUpdate` avant publish
- Test régression même instance repository (révisions + historique)

## Éditeur

- `MapCanvas` délègue à `MapEditOperations`
- Undo : snapshot avant mutation (visibilité couche, PropertyGrid MouseDown)
- Fermeture WPF : cancel synchrone + prompt async + re-close
- `SaveMap()` wrapper avec capture d’exceptions (plus de `_ =` silencieux)

## Tests ajoutés / corrigés

- PG : 16 tests (100 % vert local)
- Smoke : 7 tests Windows (save commande, close dirty, undo canvas)
- Unit : 129 (`Frog.Tests`)

## Plage

- Gate Phase 3 : `20eedc1`
- Phase 4 : `20eedc1..HEAD`
