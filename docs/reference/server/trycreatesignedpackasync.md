# trycreatesignedpackasync

← [Server](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Assemble la charge CTS1 signée des tilesets composés publiés.

*Source : `ComposedTilesetProtectedLoad.TryCreateSignedPackAsync`*

**Signature :** `trycreatesignedpackasync(cancellationToken?) → byte[]?`

**Entrées :**
- `cancellationToken` (`CancellationToken`) — annulation de la lecture catalogue

**Sorties :**
- `pack` (`byte[]?`) — null si la graine Ed25519 manque

**Refus :**
- graine privée absente → null
- catalogue publié vide → charge CTS1 encore écrite (zéro tileset)

**Exemple :**
```csharp
byte[]? pack = await composed.TryCreateSignedPackAsync(ct);
// null : TilePackOptions sans graine privée
// Assemble la charge CTS1 signée des tilesets composés publiés.
// trycreatesignedpackasync(cancellationToken?) → byte[]?
```
