# mobspawnzonedocument-tojson

← [Core](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Sérialise en JSON camelCase (formatVersion, zones, entries).

*Source : `MobSpawnZoneDocument.ToJson`*

**Signature :** `tojson() → string`

**Entrées :**
- —

**Sorties :**
- `json` (`string`) — document indenté

**Exemple :**
```csharp
string json = document.ToJson();
// Sérialise en JSON camelCase (formatVersion, zones, entries).
// tojson() → string
```
