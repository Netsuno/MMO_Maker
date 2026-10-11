# mobspawnzonedocument

← [Core](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Zones d’une carte, hors blob .fmap et hors Hello.

*Source : `MobSpawnZoneDocument`*

**Signature :** `mobspawnzonedocument → MobSpawnZoneDocument`

**Membres :**
- `FormatVersion` (`int`) — 1
- `MaxZones` (`int`) — 32
- `MaxEntries` (`int`) — 16
- `MaxNameLength` (`int`) — 80
- `MaxLabelLength` (`int`) — 80
- `MinQuantity` (`int`) — 1
- `MaxQuantity` (`int`) — 99
- `MaxRespawnSeconds` (`int`) — 86400
- `Zones` (`IReadOnlyList<MobSpawnZone>`) — zones courantes
- `IsEmpty` (`bool`) — aucune zone

**Entrées :**
- —

**Sorties :**
- `document` (`MobSpawnZoneDocument`) — porteur des rectangles

**Exemple :**
```csharp
MobSpawnZoneDocument doc = MobSpawnZoneDocument.Empty();
string json = doc.ToJson();
// Zones d’une carte, hors blob .fmap et hors Hello.
// mobspawnzonedocument → MobSpawnZoneDocument
```
