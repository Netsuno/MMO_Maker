# mapobjectsession-openasync

← [Editor](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Ouvre un objet de carte enregistré.

*Source : `MapObjectWorkspaceSession.OpenAsync`*

**Signature :** `openasync(mapObjectId, cancellationToken?) → bool`

**Entrées :**
- `mapObjectId` (`Guid`) — identifiant
- `cancellationToken` (`CancellationToken`) — annulation

**Sorties :**
- `opened` (`bool`) — faux si vide ou absent

**Refus :**
- Guid.Empty
- id inconnu

**Exemple :**
```csharp
bool opened = await session.OpenAsync(id, ct);
// Ouvre un objet de carte enregistré.
// openasync(mapObjectId, cancellationToken?) → bool
```
