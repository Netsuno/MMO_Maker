# Objets de carte

← [Guides](README.md) · [Quickstart auteur](CREATOR_QUICKSTART.md) · [Espace de travail](../../../EDITOR_WORKSPACE.md) · [Inventaire UI](../../../EDITOR_UI_INVENTORY.md) · [Modèle de données](../../../DATA_MODEL.md)

> Parcours peindre / enregistrer une carte : [quickstart auteur](CREATOR_QUICKSTART.md). Ce guide couvre la catégorie **Objets de carte** (#146).

À la fin de ce guide, tu as publié un prop. Il apparaît dans la radio **Objets** du rail droit, et un clic le pose sur la carte.

Hello / protocole **v11**. Éditeur = **Windows 11 x64**. Un objet de carte est un prop placable. La catégorie **Objets** du même formulaire reste l’inventaire. Les tuiles de contenu (`TileAsset`, carte v6, TilePack) restent **48×48** : l’empreinte ci-dessous compte des cases de carte, et les champs pixels décrivent l’image du prop.

PostgreSQL doit répondre. Sinon la ligne de statut de Données de jeu affiche `Backend : mémoire (démo — non persistant)`, et **Enregistrer brouillon**, **Publier** et **Supprimer** restent inactifs.

## Prérequis

- Éditeur du même build que le serveur (protocole **v11**)
- Chaîne PostgreSQL joignable (`AllowsSave`)
- Une image pour le prop (PNG, JPEG, BMP, GIF ou WebP). **Importer…** la copie dans le projet

## 1. Ouvrir la catégorie

**Ressources → Données de jeu…** (`MainWindow.CmdGameData`). La fenêtre s’intitule **Données de jeu** (960×640). Le chargement affiche `Initialisation des données de jeu…`.

La liste de gauche (200 px) se termine par **Armes**, **Armures**, **Objets de carte**, **Tuiles composées**. **Objets de carte** est l’index 12 (`GameDataForm.MapObjectCategoryIndex`), après Armures. Le panneau est `MapObjectEditorPanel`.

<!-- CAPTURE: Fenêtre Données de jeu, liste de gauche avec Objets de carte sélectionné après Armures, barre de statut en bas -->
*Capture à venir.*

## 2. Lire le formulaire

À gauche du panneau (260 px) : filtre **Tous / Brouillon / Publié**, champ `Rechercher…`, puis la liste. Chaque ligne est `Nom [Draft]` ou `Nom [Published]` : ce sont les noms de `ContentPublishStatus`, pendant que le filtre au-dessus est en français.

À droite :

| Champ | Rôle |
| --- | --- |
| Nom | 1–120 caractères |
| Chemin logique | relatif, unique, sans `..` ni séparateur Windows (ex. `prefabs/lampe.png`) |
| Identifiant | pose dans la palette. Minuscules, chiffres et tirets (`obj-` + guid sans tirets pour un brouillon neuf) |
| Aperçu | vignette 128×128 du chemin logique |
| Largeur tuiles / Hauteur tuiles | empreinte sur la carte, 1–32 cases |
| Largeur px / Hauteur px | taille de l’image, remplie par l’import |
| SHA-256 | 64 caractères hex, rempli par l’import |
| État | id, révision, statut, révision publiée |

Boutons du bas, dans cet ordre : **Nouveau**, **Dupliquer**, **Importer…**, **Enregistrer brouillon**, **Publier**, **Supprimer**.

<!-- CAPTURE: Panneau Objets de carte après Nouveau : champs Nom, Chemin logique, Identifiant, aperçu, empreinte en tuiles, taille en pixels, SHA-256, et la rangée de boutons -->
*Capture à venir.*

## 3. Créer le brouillon

1. **Nouveau**. Le statut passe à `Nouveau brouillon`. Le nom part sur `Nouvel objet`, le chemin sur `prefabs/…png`, l’identifiant sur `obj-` suivi de l’id.
2. **Importer…**. Le dialogue s’intitule **Importer un asset projet** (filtre Images). Le chemin logique, le SHA-256 et la taille en pixels se remplissent. Le statut passe à `Asset importé (non enregistré)`.
3. Ajuste le nom et l’empreinte en tuiles. Une empreinte hors 1–32 affiche `Empreinte hors plage (1–32 tuiles).` L’identifiant refusé affiche `Identifiant de placement invalide (minuscules, chiffres et tirets).`
4. **Enregistrer brouillon**. Le statut passe à `Brouillon enregistré rev=N`. La liste gagne une ligne `Nom [Draft]`.

Un changement de ligne avec des modifications en cours demande `Modifications non enregistrées. Continuer ?` (**Objets de carte**). Fermer la fenêtre avec un formulaire sale demande `Modifications non enregistrées. Fermer quand même ?`

**Dupliquer** copie la fiche ouverte (`Copie créée`). **Supprimer** retire la fiche enregistrée.

<!-- CAPTURE: Après Enregistrer brouillon : ligne de liste Nom [Draft], barre de statut « Brouillon enregistré rev=… », validation vide -->
*Capture à venir.*

## 4. Publier

**Publier**, avec PostgreSQL joignable. Le statut passe à `Publié rev=N`. La liste affiche `Nom [Published]`.

La publication écrit le snapshot (`content.map_object_published_snapshots`, migration `MapObjectDraftPublish`, `20261004120000`). Un brouillon reste dans `content.map_objects` et n’entre pas dans la palette. Détail des colonnes : [modèle de données](../../../DATA_MODEL.md).

Un chemin déjà pris affiche `Chemin logique déjà utilisé.` Une révision qui a bougé affiche `Conflit de révision (courante=N).`

<!-- CAPTURE: Après Publier : liste Nom [Published], barre de statut « Publié rev=… », champ État avec publié renseigné -->
*Capture à venir.*

## 5. Le voir dans la palette Objets

Ferme **Données de jeu**. La coque livrée (`MainWindow.OpenGameData`) appelle alors `QueuePublishedMapObjectReload`. La radio **Objets** reçoit les props déjà présents plus les objets publiés (`ReloadPublishedMapObjectsIntoPaletteAsync`, `MapObjectPlacementCatalog.Merge`). Le PNG publié est recopié sous `Prefabs/` pour la vignette.

La radio **Objets** montre :

- le filtre `Rechercher (nom ou id)` ;
- le compteur `N objets` ;
- la grille (nom affiché = nom publié, identifiant de placement en méta) ;
- le facing **Sud / Ouest / Est / Nord** seulement s’il y a plusieurs variantes. Un objet de carte publié n’a que la variante Sud : le facing reste masqué ;
- **Dupliquer** (Ctrl+D). Le bouton **Placer un objet** (`BtnPlacePrefab`) reste masqué (`Visibility=Collapsed`).

Choisir la vignette arme la pose. Le texte passe à `Clic pour placer — Échap pour annuler`.

<!-- CAPTURE: Rail droit, radio Objets cochée, grille avec le prop publié, texte « Clic pour placer — Échap pour annuler », bouton Placer un objet absent -->
*Capture à venir.*

## 6. Le poser sur la carte

1. Ouvre une carte.
2. Radio **Objets**, clique le prop publié.
3. Clic gauche sur une case : il se pose (`EditorTool.Prefab`).
4. Clic droit sur le prop : il s’efface.
5. **Dupliquer** ou Ctrl+D : une copie décalée de l’empreinte.
6. Échap quitte la pose. Dans le filtre, Échap vide d’abord le texte.

Touche P sélectionne aussi l’outil prefab. La ligne de statut porte l’outil **Prefab (objet)**.

**Fichier → Enregistrer (PostgreSQL)** puis **Publier (PostgreSQL)…** enregistrent la carte, comme dans le [quickstart](CREATOR_QUICKSTART.md). L’objet de carte, lui, est déjà publié par l’étape 4 : sans cette publication, la grille ne le liste pas.

## Tu es prêt si…

- [ ] **Objets de carte** est bien après Armures, et **Objets** (inventaire) est une autre ligne
- [ ] Un brouillon enregistré affiche `[Draft]` et n’apparaît pas dans la radio **Objets**
- [ ] Après **Publier** et la fermeture de Données de jeu, le prop est dans la grille
- [ ] Un clic le pose, un clic droit l’efface, Échap quitte la pose
- [ ] PostgreSQL injoignable : **Enregistrer brouillon** et **Publier** restent inactifs, statut `mémoire (démo — non persistant)`

## Et après ?

- [Données de jeu](GAME_DATA.md) — les 14 catégories de la fenêtre
- [Tuiles composées](COMPOSED_TILESETS.md) — autre catégorie de Données de jeu
- [Zones de monstres](MOB_ZONES.md)
- [Quickstart auteur](CREATOR_QUICKSTART.md) § Objets
- [Espace de travail](../../../EDITOR_WORKSPACE.md) — radio **Objets**
