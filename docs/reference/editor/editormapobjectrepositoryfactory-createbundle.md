# editormapobjectrepositoryfactory-createbundle

← [Editor](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Ouvre le dépôt d’objets de carte (PostgreSQL, sinon mémoire).

*Source : `EditorMapObjectRepositoryFactory.CreateBundle`*

**Signature :** `createbundle() → EditorMapObjectRepositoryBundle`

**Entrées :**
- —

**Sorties :**
- `repository` (`IMapObjectRepository`) — port d’écriture
- `publishedCatalog` (`IPublishedMapObjectCatalog`) — lus publiés
- `capabilities` (`ContentRepositoryCapabilities`) — durable ou démo

**Exemple :**
```csharp
EditorMapObjectRepositoryBundle bundle = EditorMapObjectRepositoryFactory.CreateBundle();
// Ouvre le dépôt d’objets de carte (PostgreSQL, sinon mémoire).
// createbundle() → EditorMapObjectRepositoryBundle
```
