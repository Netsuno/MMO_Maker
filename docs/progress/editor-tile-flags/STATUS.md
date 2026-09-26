# STATUS — Drapeaux de tuile (mode tileset VX)

| Champ | Valeur |
| --- | --- |
| **Chantier** | Panneau éditeur : passage, mode échelle, carreaux obscurcissants, interaction, sol blessant, numéro de terrain |
| **Propriétaire** | Netsun |
| **Statut** | MVP éditeur + lecture collision |
| **Tuiles** | 48×48 `TileAsset` — pas de retour à 32 |
| **Protocole** | `FrogWireProtocol.Version` **reste 11** — `.fmap` **reste v5 / v6** |

Les drapeaux sont la meta d’un `TileAssetId` (catalogue `tile-flags.json`, sidecar `{carte}.tileflags.json`). Ils ne sont pas dans le blob carte ni dans Hello. Aucun asset ni rvdata VX.

## Livré

1. **Modèle** — `TileAssetFlags` : passage ○ / × / ★, quatre directions, priorité 0–5 (mode échelle), buisson, comptoir, dégâts (sol, pas un montant), numéro de terrain 0–7.
2. **Éditeur** — sept modes, comme la base Tilesets : Passage (global), Passage (4 directions), Mode échelle, Carreaux obscurcissants, Carreaux d'interaction, Sol blessant, Numéro de terrain. Le mode actif se peint sur la vignette 48×48 ; un clic l’applique.
3. **Lecture** — une tuile × entre dans les cases bloquées. ★ n’affecte pas le passage. Un pas cardinal exige la sortie et l’entrée inverse. Buisson, priorité, comptoir, dégâts et terrain sont interrogeables. Le dessin (translucidité, ordre) et les points de vie ne changent pas.

## Hors scope

Feuilles autotile VX (A1–A5) et rvdata, base de données, bump Hello, peau DA, vague de docs. Le raccord par groupe (rôle sur la tuile 48×48) reste dans `tile-flags.json`.
