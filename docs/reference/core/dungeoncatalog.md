# DungeonCatalog

← [Core](README.md) · Tip : `a6edd821` · #33 scaffolding

Catalogue MVP **fixe** (2 templates). Persistence PG = TODO.

| Définition | Kind | Min party | TemplateMapId |
| --- | --- | --- | --- |
| **Ruines du Marais** | Dungeon | 1 | 1 |
| **Crypte du Roi** | Raid | 2 | 1 |

## Méthodes

| Fonction | Signature | Une ligne |
| --- | --- | --- |
| find | `find(id)` | Définition ou null |
| ofkind | `ofkind(kind)` | Filtre Dungeon/Raid |
| All | propriété | Liste fixe |

**Exemple :**
```csharp
var def = DungeonCatalog.Find(id); // Ruines du Marais / Crypte du Roi
var raids = DungeonCatalog.OfKind(InstanceHubKind.Raid);
// Catalogue MVP fixe (2 templates). Persistence PG = TODO
```

**Honnêteté :** pas de cartes instance dédiées publiées ; spawn tiles stub.
