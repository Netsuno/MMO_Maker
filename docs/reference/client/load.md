# load

← [Client](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Lit une charge CTS1 déjà téléchargée et refuse un catalogue JSON brut.

*Source : `ComposedTilesetClientLoader.Load`*

**Signature :** `load(payload, trustedPublicKey) → IReadOnlyList<ComposedTilesetDefinition>`

**Entrées :**
- `payload` (`ReadOnlySpan<byte>`) — octets CTS1
- `trustedPublicKey` (`ReadOnlySpan<byte>`) — clé Ed25519 épinglée

**Sorties :**
- `tilesets` (`IReadOnlyList<ComposedTilesetDefinition>`) — tilesets vérifiés

**Refus :**
- corps JSON `{` / `[` (BOM et blancs ignorés)
- signature ou taille de tuile refusée par le lecteur CTS1

**Exemple :**
```csharp
var tilesets = ComposedTilesetClientLoader.Load(payload, trustedPublicKey);
// Catalogue brut non protégé refusé si le corps commence par { ou [
// Lit une charge CTS1 déjà téléchargée et refuse un catalogue JSON brut.
// load(payload, trustedPublicKey) → IReadOnlyList<ComposedTilesetDefinition>
```
