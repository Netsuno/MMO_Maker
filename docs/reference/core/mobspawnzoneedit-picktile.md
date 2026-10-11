# mobspawnzoneedit-picktile

← [Core](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Choisit une case uniforme dans le rectangle.

*Source : `MobSpawnZoneEdit.PickTile`*

**Signature :** `picktile(zone, random) → (int X, int Y)`

**Entrées :**
- `zone` (`MobSpawnZone`) — largeur et hauteur ≥ 1
- `random` (`Random`) — tirage

**Sorties :**
- `tile` (`(int X, int Y)`) — case intérieure

**Refus :**
- rectangle de largeur ou hauteur < 1

**Exemple :**
```csharp
var (x, y) = MobSpawnZoneEdit.PickTile(zone, Random.Shared);
// Choisit une case uniforme dans le rectangle.
// picktile(zone, random) → (int X, int Y)
```
