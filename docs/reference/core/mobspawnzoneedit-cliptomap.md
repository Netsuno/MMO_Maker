# mobspawnzoneedit-cliptomap

← [Core](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Retire les zones qui ne tiennent plus sur la carte.

*Source : `MobSpawnZoneEdit.ClipToMap`*

**Signature :** `cliptomap(document, mapWidth, mapHeight) → int`

**Entrées :**
- `document` (`MobSpawnZoneDocument`) — zones
- `mapWidth` (`int`) — largeur
- `mapHeight` (`int`) — hauteur

**Sorties :**
- `removed` (`int`) — zones retirées

**Exemple :**
```csharp
int removed = MobSpawnZoneEdit.ClipToMap(document, map.Width, map.Height);
// Retire les zones qui ne tiennent plus sur la carte.
// cliptomap(document, mapWidth, mapHeight) → int
```
