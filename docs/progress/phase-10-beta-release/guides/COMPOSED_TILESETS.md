# Tuiles composées

← [Guides](README.md) · [Quickstart auteur](CREATOR_QUICKSTART.md) · [Espace de travail](../../../EDITOR_WORKSPACE.md) · [Inventaire UI](../../../EDITOR_UI_INVENTORY.md) · [Modèle de données](../../../DATA_MODEL.md)

> Peinture d’une carte : [quickstart auteur](CREATOR_QUICKSTART.md). Ce guide couvre **Tuiles composées** (#148).

À la fin de ce guide, tu as publié un tileset fait de tuiles **48×48** déjà choisies. Au prochain démarrage de l’éditeur, son nom est dans la palette TileAsset. Un client du même serveur charge la charge signée **CTS1**.

Hello / protocole **v11** (`FrogWireProtocol.Version`). Tuiles de contenu **48×48** : `TileAsset` (`TileAssetMetrics.TargetTileSizePixels`), carte v6 (`tileSizePixels` 48), TilePack, charge CTS1. Cette page ne découpe pas une feuille.

PostgreSQL doit répondre. Sinon le statut affiche `Backend : mémoire (démo — non persistant)`, et **Enregistrer brouillon**, **Publier** et **Supprimer** restent inactifs.

## Prérequis

- Éditeur du même build que le serveur et le client (protocole **v11**)
- Chaîne PostgreSQL joignable
- Un ou plusieurs PNG qui font déjà **48×48** pixels. Une feuille plus grande est refusée

## 1. Ouvrir la catégorie

**Ressources → Données de jeu…**. Dans la liste de gauche, **Tuiles composées** est la dernière entrée, index 13 (`GameDataForm.ComposedTilesetCategoryIndex`), après **Objets de carte**. Le panneau est `ComposedTilesetEditorPanel`.

La fenêtre s’intitule **Données de jeu**.

<!-- CAPTURE: Fenêtre Données de jeu, Tuiles composées sélectionné en bas de la liste, après Objets de carte -->
*Capture à venir.*

## 2. Lire le formulaire

Filtre **Tous / Brouillon / Publié**, champ `Rechercher…`, liste `Nom [Draft]` ou `Nom [Published]` (noms de `ContentPublishStatus`).

| Zone | Rôle |
| --- | --- |
| Nom | 1–120 caractères. Un brouillon neuf part sur `Nouveau tileset` |
| Chemin logique | unique, relatif (ex. `tiles/composed/{id}.tileset`) |
| Tuiles choisies | liste ordonnée. L’ordre est celui de la palette |
| État | id, révision, statut, révision publiée |

Sous la liste : **Ajouter une tuile…**, **Monter**, **Descendre**, **Retirer**.

En bas : **Nouveau**, **Dupliquer**, **Enregistrer brouillon**, **Publier**, **Supprimer**.

<!-- CAPTURE: Panneau Tuiles composées après Nouveau : Nom « Nouveau tileset », chemin tiles/composed/….tileset, liste Tuiles choisies vide, boutons Ajouter une tuile, Monter, Descendre, Retirer -->
*Capture à venir.*

## 3. Choisir des tuiles 48×48

1. **Nouveau**. Statut : `Nouveau brouillon`.
2. **Ajouter une tuile…**. Le dialogue s’intitule **Choisir une tuile 48×48**, filtre `PNG (*.png)`.
3. Chaque PNG accepté devient un `TileAsset` : l’identifiant est l’empreinte des pixels **48×48**. La ligne affiche le nom de fichier, tronqué à 48 caractères. Statut : `Tuile ajoutée (non enregistré)`.
4. **Monter** / **Descendre** réordonnent. **Retirer** enlève la ligne. Statut : `Modifié (non enregistré)`.

Un PNG illisible affiche `PNG illisible. Choisissez une tuile 48×48, pas une feuille.` Une image d’une autre taille affiche `Une tuile de contenu fait 48×48. Cette page ne découpe pas une feuille.` Publier sans tuile affiche `Ajoutez au moins une tuile.`

Le bouton **Importer une feuille…** du plan de travail TileAsset (radio **Tuiles**, bouton **TileAsset**) est un autre écran : il découpe une feuille. Il ne remplit pas cette liste.

<!-- CAPTURE: Dialogue Ouvrir intitulé « Choisir une tuile 48×48 », filtre PNG, devant le panneau Tuiles composées -->
*Capture à venir.*

<!-- CAPTURE: Liste Tuiles choisies avec deux lignes numérotées, boutons Monter et Descendre, validation vide -->
*Capture à venir.*

<!-- CAPTURE: Boîte « Tuile » avec le texte « Une tuile de contenu fait 48×48. Cette page ne découpe pas une feuille. » -->
*Capture à venir.*

Changer de ligne avec des modifications demande `Modifications non enregistrées. Continuer ?` (**Tuiles composées**). Fermer la fenêtre sale demande `Modifications non enregistrées. Fermer quand même ?`

## 4. Enregistrer, puis publier

**Enregistrer brouillon** : statut `Brouillon enregistré rev=N`, ligne `Nom [Draft]`.

**Publier** : statut `Publié rev=N`, ligne `Nom [Published]`. Les pixels partent dans `content.tiles`. L’ordre des membres (identifiant `TileAsset`, nom d’affichage) part dans `content.composed_tilesets.members_json`. Le snapshot est `content.composed_tileset_published_snapshots` (migration `ComposedTilesetDraftPublish`, `20261005010000`). `content.tilesets` (feuilles) n’est pas modifié. Hello reste **11**.

Un brouillon n’est pas dans `ListPublishedAsync`. Seul le snapshot publié est relu.

Chemin déjà pris : `Chemin logique déjà utilisé.` Révision dépassée : `Conflit de révision (courante=N).`

<!-- CAPTURE: Après Publier : liste Nom [Published], barre de statut « Publié rev=… » -->
*Capture à venir.*

## 5. Le retrouver dans la palette TileAsset

La fusion s’appelle `TileAssetCatalogue.MergePublishedComposedTilesets` (`ComposedTilesetPlacement.Merge`) :

- les palettes locales, sans identifiant serveur, restent ;
- les lignes publiées s’ajoutent, sous le nom publié ;
- un brouillon n’est pas passé à cette fusion.

Dans la coque livrée, cet appel part à la fin de `MainForm.InitializeWorkspaceCoreAsync` (`ReloadPublishedComposedTilesetsIntoPaletteAsync`), une fois le catalogue de cartes ouvert. Fermer **Données de jeu** depuis le menu WPF (`MainWindow.OpenGameData`) relance le rechargement des [objets de carte](MAP_OBJECTS.md) et ne relance pas celui des tuiles composées. `QueuePublishedComposedTilesetReload` est branché sur `MainForm.OpenGameData`, menu WinForms que `App.xaml` n’affiche pas. **F5** (**Carte → Actualiser le catalogue**) recharge les cartes, pas ce tileset.

Donc : publie, ferme Données de jeu, **rouvre l’éditeur**. Au démarrage, le combo **Tileset de travail** se place sur le premier tileset publié (le catalogue publié est trié par nom). Les palettes locales restent dans la liste.

Pour peindre :

1. Radio **Tuiles**.
2. Bouton **TileAsset** (à côté de **Feuille**). Le plan de travail titre **Catalogue** et **Tileset de travail**.
3. Choisis le nom publié dans le combo. La liste numérote les tuiles dans l’ordre enregistré.
4. Clique une tuile : le pinceau retient son `TileAssetId`.
5. **Pinceau** (B) sur la carte.

Si la carte est encore une feuille v5, **Carte → Passer cette carte en TileAsset (v6)…** écrit la taille **48** lorsque la carte n’a pas de coordonnées de feuille. Le statut devient `Carte en TileAsset (v6, 48 px). Le pinceau pose des TileAssetId.` Une carte qui a déjà des `Src` reste en v5, avec l’avertissement `Cette carte contient des coordonnées de feuille (Src). Elle reste en v5.`

<!-- CAPTURE: Radio Tuiles, bouton TileAsset enfoncé, bannière Tileset de travail, combo sur le nom publié, liste numérotée des tuiles 48×48, canevas -->
*Capture à venir.*

## 6. Côté client (CTS1)

Le client télécharge la charge après la connexion TCP, à la réception du catalogue publié, et en entrant sur une carte TileAsset sans paquet vérifié (`MainShellForm.SyncTilePackAsync`, puis `ComposedTilesetClientLoader.SyncAsync`).

- Requête : `GET /content/composed-tilesets` (`ComposedTilesetPackFormat.HttpPath`)
- Magic **CTS1**, signature Ed25519, même clé que le frogpack
- Tuile annoncée : **48×48**
- Un catalogue JSON brut est refusé (`Catalogue brut non protégé refusé.`)
- Le journal client affiche `Tilesets composés vérifiés : N tileset(s).` quand la charge est acceptée, ou `Tilesets composés : ` suivi du motif de refus ou d’absence
- Aucun tileset publié, ou service indisponible : `Aucun tileset composé publié.`

Hello TCP ne porte pas cette charge. Il reste **11**.

<!-- CAPTURE: Journal du client après connexion, ligne « Tilesets composés vérifiés : N tileset(s). », sans clé ni jeton -->
*Capture à venir.*

## Tu es prêt si…

- [ ] **Ajouter une tuile…** accepte un PNG **48×48** et refuse une feuille
- [ ] Le brouillon affiche `[Draft]` et n’est pas dans le combo **Tileset de travail**
- [ ] Après **Publier** et une réouverture de l’éditeur, le nom publié est dans ce combo, tuiles dans l’ordre
- [ ] Le pinceau pose ces `TileAssetId` sur une carte v6
- [ ] Le journal client confirme la charge CTS1, et Hello reste **11**

## Et après ?

- [Objets de carte](MAP_OBJECTS.md)
- [Zones de monstres](MOB_ZONES.md)
- [Quickstart auteur](CREATOR_QUICKSTART.md) § Peindre
- [Espace de travail](../../../EDITOR_WORKSPACE.md) — palette TileAsset
