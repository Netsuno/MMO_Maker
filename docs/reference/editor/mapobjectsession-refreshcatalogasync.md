# mapobjectsession-refreshcatalogasync

← [Editor](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Recharge le catalogue avec les filtres courants.

*Source : `MapObjectWorkspaceSession.RefreshCatalogAsync`*

**Signature :** `refreshcatalogasync(cancellationToken?) → Task`

**Entrées :**
- `cancellationToken` (`CancellationToken`) — annulation

**Sorties :**
- `catalog` (`IReadOnlyList`) — écrit dans Catalog

**Exemple :**
```csharp
await session.RefreshCatalogAsync(ct);
// Recharge le catalogue avec les filtres courants.
// refreshcatalogasync(cancellationToken?) → Task
```
