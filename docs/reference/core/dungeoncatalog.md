# DungeonCatalog

← [Core](README.md) · Tip : `d6e59759` · #33 scaffolding

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

**Honnêteté :** pas de cartes instance dédiées publiées ; spawn tiles stub.
