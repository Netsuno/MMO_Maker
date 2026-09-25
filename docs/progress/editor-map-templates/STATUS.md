# STATUS — Modèles de carte (éditeur)

| Champ | Valeur |
| --- | --- |
| **Chantier** | Enregistrer un rectangle (sélection ou carte) et le reposer |
| **Propriétaire** | Netsun |
| **Statut** | MVP éditeur |
| **Protocole** | `FrogWireProtocol.Version` **reste 11** — `.fmap` TileAsset **reste v6 / 48 px** |

Le modèle est un fichier local `map-templates.json` (à côté du catalogue TileAsset). Il n’entre pas dans le blob carte, le protocole, ni la publication.

## Livré

1. **Menu Édition** — « Enregistrer la sélection comme modèle… », « Enregistrer la carte comme modèle… », « Poser un modèle… » (curseur ou origine 0,0). Libellés français, coque WPF et menu WinForms.
2. **TileAsset** — le modèle stocke des `TileAssetId` (48 px), pas la case du tileset de travail. Une carte feuille (v5) ne se mélange pas avec une carte TileAsset. Une carte TileAsset qui n’est pas en 48 px est refusée.
3. **Couches et trous** — toutes les couches du rectangle, comme le collage. Les couches verrouillées ou absentes sont ignorées. Les cases vides du rectangle effacent la destination.
4. **Prefabs** — ceux dont l’origine est dans le rectangle sont repris (position relative) et reposés via le catalogue déjà chargé. L’annulation restaure les tuiles (une entrée). Les prefabs restent hors de cette pile, comme une pose directe.

## Hors scope

Marketplace, bump Hello, conversion 32→48, publication PostgreSQL du modèle.
