# Référence

Fonctions documentées : `nom(args)` · entrées/sorties typées.  
Classement **par sous-projet**, puis **A–Z**.

> Au fil de l’eau. Absent / prévu ≠ livré. Tip inventaire : `2af2dd22` (depuis `a6edd821`). Hello `FrogWireProtocol.Version` = **11**. Propriétaire : **Netsun**.

Pipeline tuiles de contenu : [48×48](pipeline-tuiles-48.md) — `TileAsset` → TilePack → carte v6 → tuiles composées → CTS1. `WorldMetrics.DefaultTileSizePixels` = **32** est la grille monde, pas la tuile de contenu.

## Sous-projets (assemblies)

| Sous-projet | Page | Contenu (1 ligne) |
| --- | --- | --- |
| Client | [Client](client/README.md) | `FrogGameClient` Send*/Connect*, charge CTS1 |
| Server | [Server](server/README.md) | Auth, map, move, zones publiées, GET CTS1 |
| Editor | [Editor](editor/README.md) | Save/Publish, objets de carte, zones, tuiles composées |
| Core | [Core](core/README.md) | Formules, codecs, CTS1, zones, objets de carte |
| PostgreSQL | [PostgreSQL](postgresql/README.md) | Account, Character, Inventory, Map, objets, tuiles composées |

## Comment lire

1. Ouvrir le sous-projet  
2. Lire la **Signature**  
3. Parcourir **Entrées** / **Sorties**  
4. Lire **Exemple** (C# court) — après Refus s’il y en a

Gabarit DA : `_gabarit-api.md`. UI boutons → Guides (`guides/UI-CLIENT-MainShell.md`), pas ici.
