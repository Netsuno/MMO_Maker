# composedtilesetsession-deletecurrentasync

← [Editor](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Supprime le courant s’il a déjà un id.

*Source : `ComposedTilesetWorkspaceSession.DeleteCurrentAsync`*

**Signature :** `deletecurrentasync(cancellationToken?) → DeleteComposedTilesetResult`

**Entrées :**
- `cancellationToken` (`CancellationToken`) — annulation

**Sorties :**
- `result` (`DeleteComposedTilesetResult`) — Success, NotFound ou PersistenceFailed

**Refus :**
- pas encore d’id

**Exemple :**
```csharp
var result = await session.DeleteCurrentAsync(ct);
// Supprime le courant s’il a déjà un id.
// deletecurrentasync(cancellationToken?) → DeleteComposedTilesetResult
```
