# computesha256hex

← [Core](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Empreinte SHA-256 hex des octets, même calcul que le tileset feuille.

*Source : `MapObjectDefinition.ComputeSha256Hex`*

**Signature :** `computesha256hex(bytes) → string`

**Entrées :**
- `bytes` (`ReadOnlySpan<byte>`) — PNG ou autre source

**Sorties :**
- `hex` (`string`) — 64 caractères

**Exemple :**
```csharp
string hex = MapObjectDefinition.ComputeSha256Hex(png);
// Empreinte SHA-256 hex des octets, même calcul que le tileset feuille.
// computesha256hex(bytes) → string
```
