# mobspawnzone

← [Core](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Rectangle de tuiles. Les monstres n’apparaissent qu’à l’intérieur.

*Source : `MobSpawnZone`*

**Signature :** `mobspawnzone → MobSpawnZone`

**Membres :**
- `Id` (`Guid`) — zone
- `Name` (`string`) — nom, max 80
- `TileX` (`int`) — coin
- `TileY` (`int`) — coin
- `Width` (`int`) — largeur en cases
- `Height` (`int`) — hauteur en cases
- `Entries` (`List<MobSpawnEntry>`) — lignes, max 16

**Entrées :**
- —

**Sorties :**
- `zone` (`MobSpawnZone`) — rectangle mutable

**Exemple :**
```csharp
zone.Entries.Add(entry);
// Rectangle de tuiles. Les monstres n’apparaissent qu’à l’intérieur.
// mobspawnzone → MobSpawnZone
```
