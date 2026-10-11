# composedtilesetsession-savecurrentasync

← [Editor](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Enregistre le brouillon ou publie, révision optimiste.

*Source : `ComposedTilesetWorkspaceSession.SaveCurrentAsync`*

**Signature :** `savecurrentasync(intent, cancellationToken?) → SaveComposedTilesetResult`

**Entrées :**
- `intent` (`SaveContentIntent`) — SaveDraft ou Publish
- `cancellationToken` (`CancellationToken`) — annulation

**Sorties :**
- `result` (`SaveComposedTilesetResult`) — Success, Conflict, ValidationFailed, PersistenceFailed ou NotDurable

**Refus :**
- aucun courant
- dépôt non durable
- enregistrement déjà en cours

**Exemple :**
```csharp
var result = await session.SaveCurrentAsync(SaveContentIntent.Publish, ct);
// Enregistre le brouillon ou publie, révision optimiste.
// savecurrentasync(intent, cancellationToken?) → SaveComposedTilesetResult
```
