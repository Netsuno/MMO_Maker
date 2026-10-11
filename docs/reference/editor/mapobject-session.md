# mapobject-session

← [Editor](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Session éditeur : catalogue et brouillon d’un objet de carte.

*Source : `MapObjectWorkspaceSession`*

**Signature :** `mapobjectsession(repository) → MapObjectWorkspaceSession`

**Membres :**
- `Capabilities` (`ContentRepositoryCapabilities`) — capacités du dépôt
- `CanPersist` (`bool`) — vrai si le dépôt est durable
- `Catalog` (`IReadOnlyList`) — résumés filtrés
- `Current` (`MapObjectDefinition?`) — brouillon ouvert
- `CurrentId` (`Guid?`) — null tant que non enregistré
- `CurrentRevision` (`long`) — révision attendue
- `CurrentStatus` (`ContentPublishStatus`) — brouillon ou publié
- `PublishedRevision` (`long?`) — révision publiée
- `IsDirty` (`bool`) — modifié depuis l’enregistrement
- `SearchFilter` (`string?`) — filtre nom
- `StatusFilter` (`ContentPublishStatus?`) — filtre statut

**Entrées :**
- `repository` (`IMapObjectRepository`) — port

**Sorties :**
- `session` (`MapObjectWorkspaceSession`) — état local

**Exemple :**
```csharp
var session = new MapObjectWorkspaceSession(repository);
await session.RefreshCatalogAsync(ct);
// Session éditeur : catalogue et brouillon d’un objet de carte.
// mapobjectsession(repository) → MapObjectWorkspaceSession
```
