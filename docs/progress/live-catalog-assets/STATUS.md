# STATUS — Live catalog tilesets and prefabs

| Champ | Valeur |
| --- | --- |
| **Chantier** | Client live (PostgreSQL) matérialise tilesets et prefabs après Publier |
| **Propriétaire** | Netsun |
| **Statut** | `PublishedCatalogResult` porte les PNG au-delà de 64 KiB ; sidecars client écrits au login / au chargement de carte |
| **Protocole** | `FrogWireProtocol.Version` **reste 11** |

Le catalogue publié (tileset `png_bytes` + prefab `pngBase64` / placements) était construit, puis l’envoi jetait `JSON catalogue publié trop grand` (préfixe `UInt16`) et l’exception était avalée. Le client ne recevait rien : `Tilesets/` et `Maps/` restaient vides.

`MapPersistenceMapper` ne fusionne plus les couches de même `LayerType`. « Sol » et « Tombe » (toutes deux Ground) restent deux couches ; les tuiles d’une même cellule ne se superposent plus au boot (`Tuiles superposées`). Les cellules nouvelles portent `layerIndex`. Les JSON déjà publiés sans cet index répartissent les charges utiles dans l’ordre du catalogue.

Les catalogues ≤ 65534 octets gardent le préfixe historique. Au-delà, frames `0xFFFF` réassemblées (≤ 1 MiB chacune). `PrefabMaps.runtimeMapId` suit `runtime_map_bindings`. Le client écrit `Tilesets/{id}.png`, `Tilesets/manifest.json`, `Maps/{nom}.tilesets.json`, `Prefabs/` et `Maps/{nom}.prefabs.json`.
