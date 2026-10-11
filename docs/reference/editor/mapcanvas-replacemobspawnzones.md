# mapcanvas-replacemobspawnzones

← [Editor](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Remplace les zones du canevas.

*Source : `MapCanvas.ReplaceMobSpawnZones`*

**Signature :** `replacemobspawnzones(document) → void`

**Entrées :**
- `document` (`MobSpawnZoneDocument?`) — null vide le canevas

**Sorties :**
- `effet` (`void`) — sélection perdue si l’id n’existe plus

**Exemple :**
```csharp
canvas.ReplaceMobSpawnZones(document);
// Remplace les zones du canevas.
// replacemobspawnzones(document) → void
```
