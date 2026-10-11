# composedtilesetmembercodec-write

← [PostgreSQL](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Écrit members_json : id et nom, sans les pixels.

*Source : `ComposedTilesetMemberCodec.Write`*

**Signature :** `write(tiles) → string`

**Entrées :**
- `tiles` (`IReadOnlyList<ComposedTileRef>`) — ordre conservé

**Sorties :**
- `json` (`string`) — tableau camelCase id, name

**Exemple :**
```csharp
string json = ComposedTilesetMemberCodec.Write(tiles);
// Écrit members_json : id et nom, sans les pixels.
// write(tiles) → string
```
