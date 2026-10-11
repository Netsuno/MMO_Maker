# isunprotectedcatalog

← [Core](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Détecte un catalogue JSON brut, que CTS1 refuse.

*Source : `ComposedTilesetPackFormat.IsUnprotectedCatalog`*

**Signature :** `isunprotectedcatalog(payload) → bool`

**Entrées :**
- `payload` (`ReadOnlySpan<byte>`) — octets reçus

**Sorties :**
- `raw` (`bool`) — vrai si `{` ou `[` après BOM et blancs

**Exemple :**
```csharp
if (ComposedTilesetPackFormat.IsUnprotectedCatalog(payload))
    throw new FrogPackRejectedException("Catalogue brut non protégé refusé.");
// Détecte un catalogue JSON brut, que CTS1 refuse.
// isunprotectedcatalog(payload) → bool
```
