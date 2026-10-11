# composedtilesetloadresult

← [Client](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Résultat d’un chargement CTS1 côté client.

*Source : `ComposedTilesetLoadResult`, `ComposedTilesetLoadKind`*

**Signature :** `composedtilesetloadresult(kind, detail) → ComposedTilesetLoadResult`

**Membres :**
- `Kind` (`ComposedTilesetLoadKind`) — issue du chargement
- `Detail` (`string`) — motif ou décompte

**Entrées :**
- `kind` (`ComposedTilesetLoadKind`) — Loaded, Rejected ou Unavailable
- `detail` (`string`) — texte court, sans clé

**Sorties :**
- `result` (`ComposedTilesetLoadResult`) — paire immuable

**Exemple :**
```csharp
var result = new ComposedTilesetLoadResult(ComposedTilesetLoadKind.Unavailable, "Aucun tileset composé publié.");
// Résultat d’un chargement CTS1 côté client.
// composedtilesetloadresult(kind, detail) → ComposedTilesetLoadResult
```
