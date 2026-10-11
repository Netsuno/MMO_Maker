# composedtilesetclientpath

← [Server](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Expose le GET de la charge CTS1 sur le canal HTTP de contenu.

*Source : `TilePackContentHttp.ComposedTilesetClientPath` — `HandleAsync` GET*

**Signature :** `composedtilesetclientpath → string`

**Entrées :**
- —

**Sorties :**
- `path` (`string`) — `/content/composed-tilesets`

**Refus :**
- service non injecté → 503
- graine absente → 503
- assemblage refusé → 500

**Exemple :**
```csharp
// GET TilePackContentHttp.ComposedTilesetClientPath
// ctor : ComposedTilesetProtectedLoad? composed = null
// composed null → HTTP 503 « Charge tilesets indisponible. »
```
