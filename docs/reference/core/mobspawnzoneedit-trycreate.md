# mobspawnzoneedit-trycreate

← [Core](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Ajoute un rectangle au document.

*Source : `MobSpawnZoneEdit.TryCreate`*

**Signature :** `trycreate(document, mapWidth, mapHeight, tileX, tileY, width, height, out zone, out error) → bool`

**Entrées :**
- `document` (`MobSpawnZoneDocument`) — zones déjà présentes
- `mapWidth` (`int`) — largeur
- `mapHeight` (`int`) — hauteur
- `tileX` (`int`) — coin
- `tileY` (`int`) — coin
- `width` (`int`) — cases
- `height` (`int`) — cases

**Sorties :**
- `ok` (`bool`) — zone ajoutée
- `zone` (`out MobSpawnZone`) — créée
- `error` (`out string?`) — motif

**Refus :**
- 32 zones déjà là
- rectangle hors carte

**Exemple :**
```csharp
if (!MobSpawnZoneEdit.TryCreate(document, w, h, tx, ty, rw, rh, out var zone, out var error)) return;
// Ajoute un rectangle au document.
// trycreate(document, mapWidth, mapHeight, tileX, tileY, width, height, out zone, out error) → bool
```
