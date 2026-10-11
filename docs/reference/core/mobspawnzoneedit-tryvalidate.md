# mobspawnzoneedit-tryvalidate

← [Core](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Vérifie toutes les zones avant écriture PostgreSQL.

*Source : `MobSpawnZoneEdit.TryValidate`*

**Signature :** `tryvalidate(document, mapWidth, mapHeight, out error) → bool`

**Entrées :**
- `document` (`MobSpawnZoneDocument`) — zones de la requête
- `mapWidth` (`int`) — largeur carte
- `mapHeight` (`int`) — hauteur carte

**Sorties :**
- `ok` (`bool`) — document acceptable
- `error` (`out string?`) — première erreur

**Refus :**
- rectangle hors carte
- plafond zones ou lignes
- quantité ou délai hors plage

**Exemple :**
```csharp
if (!MobSpawnZoneEdit.TryValidate(document, map.Width, map.Height, out var error)) return;
// Vérifie toutes les zones avant écriture PostgreSQL.
// tryvalidate(document, mapWidth, mapHeight, out error) → bool
```
