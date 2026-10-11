# syncasync

← [Client](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Télécharge GET /content/composed-tilesets et vérifie la signature.

*Source : `ComposedTilesetClientLoader.SyncAsync`*

**Signature :** `syncasync(cancellationToken?) → ComposedTilesetLoadResult`

**Entrées :**
- `cancellationToken` (`CancellationToken`) — annulation du GET

**Sorties :**
- `kind` (`ComposedTilesetLoadKind`) — Loaded, Rejected ou Unavailable
- `detail` (`string`) — motif court ou nombre de tilesets

**Refus :**
- clé publique absente ou invalide
- URL de contenu invalide
- 404 ou 503 : aucun tileset publié
- corps vide ou non OK

**Exemple :**
```csharp
ComposedTilesetLoadResult result = await loader.SyncAsync(ct);
if (result.Kind != ComposedTilesetLoadKind.Loaded) return;
// Tilesets = la liste acceptée
```
