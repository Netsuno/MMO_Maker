# composedtilesetpackwriter-write

← [Core](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Écrit la charge CTS1 signée Ed25519, tuile annoncée 48.

*Source : `ComposedTilesetPackWriter.Write`*

**Signature :** `write(tilesets, privateSeed) → byte[]`

**Entrées :**
- `tilesets` (`IReadOnlyList<ComposedTilesetDefinition>`) — tilesets publiés
- `privateSeed` (`ReadOnlySpan<byte>`) — graine de la même clé que le frogpack

**Sorties :**
- `file` (`byte[]`) — corps + hash + clé + signature

**Refus :**
- tilesets null
- signature d’une longueur inattendue

**Exemple :**
```csharp
byte[] file = ComposedTilesetPackWriter.Write(tilesets, privateSeed);
// en-tête : magic CTS1, version 1, Hello 11, tileSize 48
// Écrit la charge CTS1 signée Ed25519, tuile annoncée 48.
// write(tilesets, privateSeed) → byte[]
```
