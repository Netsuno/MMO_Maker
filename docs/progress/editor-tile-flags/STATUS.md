# STATUS — Drapeaux de tuile (mode tileset VX)

| Champ | Valeur |
| --- | --- |
| **Chantier** | Panneau éditeur : passage 4 dir., priorité, buisson, comptoir, dégâts |
| **Propriétaire** | Netsun |
| **Statut** | MVP éditeur + lecture collision |
| **Tuiles** | 48×48 `TileAsset` — pas de retour à 32 |
| **Protocole** | `FrogWireProtocol.Version` **reste 11** — `.fmap` **reste v5 / v6** |

Les drapeaux sont la meta d’un `TileAssetId` (catalogue `tile-flags.json`, sidecar `{carte}.tileflags.json`). Ils ne sont pas dans le blob carte ni dans Hello.

## Livré

1. **Modèle** — `TileAssetFlags` : Nord / Sud / Est / Ouest, priorité 0–5, buisson, comptoir, dégâts (sol, pas un montant).
2. **Éditeur** — panneau français sur le tileset TileAsset. ○ tout ouvert, × tout bloqué. Le surlignage de la vignette montre les côtés fermés.
3. **Lecture** — une tuile × entre dans les cases bloquées. Un pas cardinal exige la sortie et l’entrée inverse. Buisson, priorité, comptoir et dégâts sont interrogeables. Le dessin (translucidité, ordre) et les points de vie ne changent pas.

## Hors scope

Autotiles A1–A5, base de données, bump Hello, peau DA, vague de docs.
