# mobspawnzonedocument-tryfromjson

← [Core](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Relit le JSON. Une chaîne vide donne un document vide.

*Source : `MobSpawnZoneDocument.TryFromJson`*

**Signature :** `tryfromjson(json, out document) → bool`

**Entrées :**
- `json` (`string?`) — null ou blanc → document vide

**Sorties :**
- `ok` (`bool`) — faux seulement si InvalidDataException
- `document` (`out MobSpawnZoneDocument`) — toujours assigné

**Exemple :**
```csharp
if (!MobSpawnZoneDocument.TryFromJson(json, out var document)) return;
// Relit le JSON. Une chaîne vide donne un document vide.
// tryfromjson(json, out document) → bool
```
