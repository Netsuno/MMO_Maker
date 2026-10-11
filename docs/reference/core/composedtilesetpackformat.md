# composedtilesetpackformat

← [Core](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

En-tête de la charge signée CTS1. Ce n’est pas un opcode Hello.

*Source : `ComposedTilesetPackFormat`*

**Signature :** `composedtilesetpackformat → constantes`

**Membres :**
- `Magic` (`string`) — CTS1
- `HttpPath` (`string`) — GET /content/composed-tilesets
- `FormatVersion` (`ushort`) — 1
- `FlagsNone` (`ushort`) — 0
- `FlagEncrypted` (`ushort`) — bit frogpack, refusé ici
- `HeaderLength` (`int`) — 20 octets
- `MaxTilesets` (`int`) — 1024
- `MaxNameUtf8Bytes` (`int`) — 480

**Entrées :**
- —

**Sorties :**
- `magic` (`string`) — CTS1

**Exemple :**
```csharp
bool raw = ComposedTilesetPackFormat.IsUnprotectedCatalog(payload);
string path = ComposedTilesetPackFormat.HttpPath; // /content/composed-tilesets
// En-tête de la charge signée CTS1. Ce n’est pas un opcode Hello.
// composedtilesetpackformat → constantes
```
