# composedtilesetmembercodec-read

← [PostgreSQL](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Relit members_json. Une chaîne vide donne zéro ligne.

*Source : `ComposedTilesetMemberCodec.Read` — `Row.Id`, `Row.Name`*

**Signature :** `read(json) → IReadOnlyList<ComposedTilesetMemberCodec.Row>`

**Entrées :**
- `json` (`string?`) — colonne members_json

**Sorties :**
- `rows` (`IReadOnlyList<Row>`) — Id et Name

**Exemple :**
```csharp
IReadOnlyList<ComposedTilesetMemberCodec.Row> rows = ComposedTilesetMemberCodec.Read(json);
// Relit members_json. Une chaîne vide donne zéro ligne.
// read(json) → IReadOnlyList<ComposedTilesetMemberCodec.Row>
```
