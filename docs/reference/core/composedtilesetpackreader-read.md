# composedtilesetpackreader-read

← [Core](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Lit une charge CTS1 et vérifie la clé publique épinglée.

*Source : `ComposedTilesetPackReader.Read`*

**Signature :** `read(data, trustedPublicKey) → IReadOnlyList<ComposedTilesetDefinition>`

**Entrées :**
- `data` (`ReadOnlySpan<byte>`) — fichier CTS1
- `trustedPublicKey` (`ReadOnlySpan<byte>`) — clé épinglée

**Sorties :**
- `tilesets` (`IReadOnlyList<ComposedTilesetDefinition>`) — définitions reconstruites

**Refus :**
- magic autre que CTS1
- tileSizePixels autre que 48
- bit de chiffrement
- clé ou signature fausse

**Exemple :**
```csharp
IReadOnlyList<ComposedTilesetDefinition> sets =
    ComposedTilesetPackReader.Read(data, trustedPublicKey);
// Lit une charge CTS1 et vérifie la clé publique épinglée.
// read(data, trustedPublicKey) → IReadOnlyList<ComposedTilesetDefinition>
```
