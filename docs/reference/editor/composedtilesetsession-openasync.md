# composedtilesetsession-openasync

← [Editor](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Ouvre un tileset composé enregistré.

*Source : `ComposedTilesetWorkspaceSession.OpenAsync`*

**Signature :** `openasync(tilesetId, cancellationToken?) → bool`

**Entrées :**
- `tilesetId` (`Guid`) — identifiant
- `cancellationToken` (`CancellationToken`) — annulation

**Sorties :**
- `opened` (`bool`) — faux si vide ou absent

**Refus :**
- Guid.Empty
- id inconnu

**Exemple :**
```csharp
bool opened = await session.OpenAsync(id, ct);
// Ouvre un tileset composé enregistré.
// openasync(tilesetId, cancellationToken?) → bool
```
