# Référence — Editor

← [Référence](../README.md)

Fonctions Editor, A–Z (1 fichier / fonction). Tip : `2af2dd22`.

## Index

| Fonction | Signature | Une ligne |
| --- | --- | --- |
| [canexecutesaveorpublish](canexecutesaveorpublish.md) | `canexecutesaveorpublish()` | Save/Publish autorisés ? |
| [publishmap](publishmap.md) | `publishmap()` | Lance publish PG (UI) |
| [publishmapdialogtryvalidate](publishmapdialogtryvalidate.md) | *(absent)* | Historique : dialogue MariaDB retiré (#83). Save/Publish = fiches voisines |
| [savemap](savemap.md) | `savemap()` | Enregistre brouillon PG (UI) |

## Objets de carte, zones, tuiles composées (#146–#148)

Objets de carte (#146), zones (#147), tuiles composées (#148), A–Z. Les sessions et catalogues de placement vivent dans `Frog.Application` ; ils sont appelés par l’éditeur. `MobSpawnZonesPanel`, `PaletteChromeHeight` et `ShellLayoutDeferral` sont `internal`. #145 ne change pas de membre C# public (`PaletteHint` change avec la touche Z, #147). Pipeline : [tuiles 48×48](../pipeline-tuiles-48.md).

| Fonction | Signature | Une ligne |
| --- | --- | --- |
| [composedtileset-session](composedtileset-session.md) | `composedtilesetsession(repository) → ComposedTilesetWorkspaceSession` | Session éditeur : catalogue et brouillon d’un tileset composé |
| [composedtileseteditorpanel](composedtileseteditorpanel.md) | `composedtileseteditorpanel(session, capabilities) → ComposedTilesetEditorPanel` | Panneau Données de jeu des tuiles composées 48×48 |
| [composedtileseteditorpanel-initializeasync](composedtileseteditorpanel-initializeasync.md) | `initializeasync() → Task` | Charge le catalogue des tuiles composées dans la liste |
| [composedtilesetplacement-merge](composedtilesetplacement-merge.md) | `merge(local, published) → IReadOnlyList<WorkingTileset>` | Garde les palettes locales et ajoute les tilesets publiés |
| [composedtilesetsession-adoptnewdraft](composedtilesetsession-adoptnewdraft.md) | `adoptnewdraft(definition) → void` | Prend un brouillon neuf, révision 0, sale |
| [composedtilesetsession-deletecurrentasync](composedtilesetsession-deletecurrentasync.md) | `deletecurrentasync(cancellationToken?) → DeleteComposedTilesetResult` | Supprime le courant s’il a déjà un id |
| [composedtilesetsession-duplicatecurrent](composedtilesetsession-duplicatecurrent.md) | `duplicatecurrent() → ComposedTilesetDefinition` | Duplique le courant en nouveau brouillon |
| [composedtilesetsession-markdirty](composedtilesetsession-markdirty.md) | `markdirty() → void` | Marque le brouillon modifié |
| [composedtilesetsession-openasync](composedtilesetsession-openasync.md) | `openasync(tilesetId, cancellationToken?) → bool` | Ouvre un tileset composé enregistré |
| [composedtilesetsession-refreshcatalogasync](composedtilesetsession-refreshcatalogasync.md) | `refreshcatalogasync(cancellationToken?) → Task` | Recharge le catalogue avec les filtres courants |
| [composedtilesetsession-savecurrentasync](composedtilesetsession-savecurrentasync.md) | `savecurrentasync(intent, cancellationToken?) → SaveComposedTilesetResult` | Enregistre le brouillon ou publie, révision optimiste |
| [editorcomposedtilesetrepositoryfactory-createbundle](editorcomposedtilesetrepositoryfactory-createbundle.md) | `createbundle() → EditorComposedTilesetRepositoryBundle` | Ouvre le dépôt de tuiles composées (PostgreSQL, sinon mémoire) |
| [editormapobjectrepositoryfactory-createbundle](editormapobjectrepositoryfactory-createbundle.md) | `createbundle() → EditorMapObjectRepositoryBundle` | Ouvre le dépôt d’objets de carte (PostgreSQL, sinon mémoire) |
| [editortool-mobzone](editortool-mobzone.md) | `EditorTool.MobZone → 11` | Outil Zone : un rectangle de monstres, sans peindre de tuile |
| [gamedatarepositoryset-mapobject](gamedatarepositoryset-mapobject.md) | `mapobject → EditorMapObjectRepositoryBundle` | Expose les dépôts objets de carte et tuiles composées |
| [mapcanvas-clearmobzoneselection](mapcanvas-clearmobzoneselection.md) | `clearmobzoneselection() → void` | Oublie la zone sélectionnée |
| [mapcanvas-clipmobspawnzonestomap](mapcanvas-clipmobspawnzonestomap.md) | `clipmobspawnzonestomap() → int` | Retire les zones hors de la carte ouverte |
| [mapcanvas-mobspawnzones](mapcanvas-mobspawnzones.md) | `mobspawnzones → MobSpawnZoneDocument` | Expose le document de zones peint sur le canevas |
| [mapcanvas-mobspawnzoneschanged](mapcanvas-mobspawnzoneschanged.md) | `mobspawnzoneschanged → event Action?` | Signal après un changement des zones |
| [mapcanvas-mobspawnzoneselectionchanged](mapcanvas-mobspawnzoneselectionchanged.md) | `mobspawnzoneselectionchanged → event Action?` | Signal après un changement de sélection de zone |
| [mapcanvas-replacemobspawnzones](mapcanvas-replacemobspawnzones.md) | `replacemobspawnzones(document) → void` | Remplace les zones du canevas |
| [mapcanvas-selectedmobspawnzone](mapcanvas-selectedmobspawnzone.md) | `selectedmobspawnzone → MobSpawnZone?` | Zone sélectionnée, ou null |
| [mapcanvas-selectmobspawnzone](mapcanvas-selectmobspawnzone.md) | `selectmobspawnzone(id) → void` | Sélectionne une zone déjà dans le document |
| [mapobject-session](mapobject-session.md) | `mapobjectsession(repository) → MapObjectWorkspaceSession` | Session éditeur : catalogue et brouillon d’un objet de carte |
| [mapobjecteditorpanel](mapobjecteditorpanel.md) | `mapobjecteditorpanel(session, capabilities) → MapObjectEditorPanel` | Panneau Données de jeu des objets de carte |
| [mapobjecteditorpanel-initializeasync](mapobjecteditorpanel-initializeasync.md) | `initializeasync() → Task` | Charge le catalogue des objets de carte dans la liste |
| [mapobjectplacementcatalog-materializesprites](mapobjectplacementcatalog-materializesprites.md) | `materializesprites(published, directory) → int` | Écrit les PNG publiés dans le dossier Prefabs |
| [mapobjectplacementcatalog-merge](mapobjectplacementcatalog-merge.md) | `merge(baseCatalog, published) → PrefabCatalog` | Ajoute les objets publiés au catalogue prefab. Le même id remplace |
| [mapobjectplacementcatalog-toprefab](mapobjectplacementcatalog-toprefab.md) | `toprefab(source) → PrefabDefinition` | Projette un objet de carte en prefab face sud |
| [mapobjectsession-adoptnewdraft](mapobjectsession-adoptnewdraft.md) | `adoptnewdraft(definition) → void` | Prend un brouillon neuf, révision 0, sale |
| [mapobjectsession-cleardirty](mapobjectsession-cleardirty.md) | `cleardirty() → void` | Efface le drapeau modifié sans écrire |
| [mapobjectsession-deletecurrentasync](mapobjectsession-deletecurrentasync.md) | `deletecurrentasync(cancellationToken?) → DeleteMapObjectResult` | Supprime le courant s’il a déjà un id |
| [mapobjectsession-duplicatecurrent](mapobjectsession-duplicatecurrent.md) | `duplicatecurrent() → MapObjectDefinition` | Duplique le courant en nouveau brouillon |
| [mapobjectsession-markdirty](mapobjectsession-markdirty.md) | `markdirty() → void` | Marque le brouillon modifié |
| [mapobjectsession-openasync](mapobjectsession-openasync.md) | `openasync(mapObjectId, cancellationToken?) → bool` | Ouvre un objet de carte enregistré |
| [mapobjectsession-refreshcatalogasync](mapobjectsession-refreshcatalogasync.md) | `refreshcatalogasync(cancellationToken?) → Task` | Recharge le catalogue avec les filtres courants |
| [mapobjectsession-savecurrentasync](mapobjectsession-savecurrentasync.md) | `savecurrentasync(intent, cancellationToken?) → SaveMapObjectResult` | Enregistre le brouillon ou publie, révision optimiste |
| [mergepublishedcomposedtilesets](mergepublishedcomposedtilesets.md) | `mergepublishedcomposedtilesets(published) → void` | Fusionne les tilesets composés publiés dans la palette TileAsset |
| [palettehint](palettehint.md) | `palettehint → string` | Liste les raccourcis d’outils, touche Z comprise |
