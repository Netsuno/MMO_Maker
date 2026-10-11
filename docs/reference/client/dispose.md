# dispose

← [Client](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Libère le transport HTTP si le chargeur le possède.

*Source : `ComposedTilesetClientLoader.Dispose`*

**Signature :** `dispose() → void`

**Entrées :**
- —

**Sorties :**
- `effet` (`void`) — transport possédé disposé

**Exemple :**
```csharp
using var loader = new ComposedTilesetClientLoader(options);
await loader.SyncAsync(ct);
// Libère le transport HTTP si le chargeur le possède.
// dispose() → void
```
