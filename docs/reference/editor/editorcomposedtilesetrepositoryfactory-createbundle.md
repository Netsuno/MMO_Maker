# editorcomposedtilesetrepositoryfactory-createbundle

← [Editor](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Ouvre le dépôt de tuiles composées (PostgreSQL, sinon mémoire).

*Source : `EditorComposedTilesetRepositoryFactory.CreateBundle`*

**Signature :** `createbundle() → EditorComposedTilesetRepositoryBundle`

**Entrées :**
- —

**Sorties :**
- `repository` (`IComposedTilesetRepository`) — port d’écriture
- `publishedCatalog` (`IPublishedComposedTilesetCatalog`) — lus publiés
- `capabilities` (`ContentRepositoryCapabilities`) — durable ou démo

**Exemple :**
```csharp
var bundle = EditorComposedTilesetRepositoryFactory.CreateBundle();
// Ouvre le dépôt de tuiles composées (PostgreSQL, sinon mémoire).
// createbundle() → EditorComposedTilesetRepositoryBundle
```
