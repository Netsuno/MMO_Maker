# composedtileset-session

← [Editor](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Session éditeur : catalogue et brouillon d’un tileset composé.

*Source : `ComposedTilesetWorkspaceSession`*

**Signature :** `composedtilesetsession(repository) → ComposedTilesetWorkspaceSession`

**Membres :**
- `Capabilities` (`ContentRepositoryCapabilities`) — capacités du dépôt
- `Catalog` (`IReadOnlyList`) — résumés filtrés
- `Current` (`ComposedTilesetDefinition?`) — brouillon ouvert
- `CurrentId` (`Guid?`) — null tant que non enregistré
- `CurrentRevision` (`long`) — révision attendue
- `CurrentStatus` (`ContentPublishStatus`) — brouillon ou publié
- `PublishedRevision` (`long?`) — révision publiée
- `IsDirty` (`bool`) — modifié depuis l’enregistrement
- `SearchFilter` (`string?`) — filtre nom
- `StatusFilter` (`ContentPublishStatus?`) — filtre statut

**Entrées :**
- `repository` (`IComposedTilesetRepository`) — port

**Sorties :**
- `session` (`ComposedTilesetWorkspaceSession`) — état local

**Exemple :**
```csharp
var session = new ComposedTilesetWorkspaceSession(repository);
await session.RefreshCatalogAsync(ct);
// Session éditeur : catalogue et brouillon d’un tileset composé.
// composedtilesetsession(repository) → ComposedTilesetWorkspaceSession
```
