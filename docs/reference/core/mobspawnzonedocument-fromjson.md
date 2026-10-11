# mobspawnzonedocument-fromjson

← [Core](README.md) · [Référence](../README.md) · Tip : `2af2dd22` · Propriétaire : Netsun

Relit le JSON des zones. Une version autre que 1 est refusée.

*Source : `MobSpawnZoneDocument.FromJson`*

**Signature :** `fromjson(json) → MobSpawnZoneDocument`

**Entrées :**
- `json` (`string`) — colonne mob_spawn_zones_json

**Sorties :**
- `document` (`MobSpawnZoneDocument`) — zones lues

**Refus :**
- JSON illisible
- version ≠ 1
- plus de 32 zones
- identifiant vide ou en double

**Exemple :**
```csharp
MobSpawnZoneDocument document = MobSpawnZoneDocument.FromJson(json);
// Relit le JSON des zones. Une version autre que 1 est refusée.
// fromjson(json) → MobSpawnZoneDocument
```
