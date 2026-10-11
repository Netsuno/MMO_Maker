# Zones de monstres

← [Guides](README.md) · [Quickstart auteur](CREATOR_QUICKSTART.md) · [Espace de travail](../../../EDITOR_WORKSPACE.md) · [Inventaire UI — lacune Z](../../../EDITOR_UI_INVENTORY.md#outil-zone-z-lacune-connue) · [Modèle de données](../../../DATA_MODEL.md)

> Enregistrer et publier la carte : [quickstart auteur](CREATOR_QUICKSTART.md). Ce guide couvre l’outil Zone (#147).

À la fin de ce guide, tu as tracé une zone, tu y as mis un monstre publié, et tu as publié la carte. Le serveur fait réapparaître ce monstre à l’intérieur du rectangle publié.

Hello / protocole **v11**. La zone n’est pas dans le Hello, ni dans le fichier `.fmap`. Les tuiles de contenu restent **48×48** (`TileAsset`, carte v6, TilePack).

## Prérequis

- Éditeur Windows, coque `MMO Maker — Éditeur` (`App.xaml` → `MainWindow`, `MainForm` embarqué)
- Une carte ouverte
- Un monstre publié dans **Données de jeu → NPCs / monstres** (`NpcKind.Monster`, **Publier**). Le serveur ne fait apparaître qu’un monstre publié (`FindNpcAsync` → `ListPublishedAsync`)
- PostgreSQL joignable pour **Enregistrer** et **Publier** la carte

## 1. Ouvrir l’outil

Dans la fenêtre livrée, l’outil s’ouvre avec la touche **Z**, sans modificateur (`EditorToolHotkeys.TryResolveWpf` → `EditorTool.MobZone`, `MainWindow.OnPreviewToolHotkey`).

Z est ignoré dans un champ de texte, et ignoré si Ctrl, Alt ou Maj est enfoncé.

Le menu **Carte** de `MainWindow.xaml` liste gomme, remplissage, rectangle, ligne, départ, prefab, entités et région. Il n’a pas d’entrée Zone. Le rail droit n’a pas de puce Zone (radios **Tuiles | Objets | Entités | Régions** seulement). La lacune est notée dans l’[inventaire](../../../EDITOR_UI_INVENTORY.md#outil-zone-z-lacune-connue) et dans les [écarts encore ouverts](../../../EDITOR_UI_INVENTORY.md#ecarts-encore-ouverts) (point 5).

L’entrée **Outil zone de monstres (Z)** existe sur le `MenuStrip` de `MainForm`, construit seulement quand le formulaire n’est pas embarqué. `App.xaml` embarque le formulaire : cette entrée n’est pas celle de l’éditeur lancé.

Après Z, aucune puce de dessin n’est allumée. Le rail peut afficher la radio **Tuiles** (`ModeForTool` range Zone avec les outils de peinture) et l’indication de statut de la zone. L’inspecteur s’ouvre.

<!-- CAPTURE: Menu Carte déroulé, outils gomme à région visibles, aucune ligne Zone, focus hors champ de texte -->
*Capture à venir.*

## 2. Tracer le rectangle

Libellé d’outil : **Zone**. Indication du panneau :

`Tracez le rectangle. Glisser gauche déplace, glisser droit copie. Les monstres n’apparaissent que dans la zone.`

La ligne de statut unique place cette phrase dans le segment outil, après le mode. Z bascule le mode sur **Tuiles**, donc la ligne commence par `Tuiles ·` puis :

`Zone (Z) · aucune sélection · tracez un rectangle · glisser gauche déplace · glisser droit copie`

Avec une zone choisie, `aucune sélection` devient `« Nom »`. Sur une carte TileAsset, le segment se termine par ` · TileAsset`. La même phrase est recopiée dans l’indication bleue du rail **Tuiles**.

Gestes sur le canevas (`MapCanvas`, outil `MobZone`) :

| Geste | Effet |
| --- | --- |
| Clic gauche glissé **en dehors** d’une zone | nouveau rectangle, borné à la carte. Un simple clic fait une case |
| Clic gauche glissé **sur** une zone | la déplace. Sans mouvement, elle est seulement sélectionnée |
| Clic droit glissé **sur** une zone | la copie (nouvel id, nom d’origine suivi de ` copie`, ex. `Zone 1 copie`) |
| Clic droit **en dehors** | ne trace rien |

La première zone s’appelle `Zone 1`, puis `Zone 2`, etc. Plafond : 32 zones (`Trop de zones (32 maximum).`). Une zone qui sortirait de la carte est refusée (`La zone sort de la carte.`).

Le rectangle et les monstres voyagent avec la carte (`AttachCurrentMobSpawnZonesToWorkspace` avant Enregistrer et Publier). Ils ne sont pas dans l’export `.fmap`.

<!-- CAPTURE: Canevas avec un rectangle de zone nommé Zone 1, ligne de statut commençant par Tuiles · Zone (Z), inspecteur ouvert -->
*Capture à venir.*

## 3. Le panneau Zone de monstres

L’inspecteur s’ouvre (~160 px, `InspectorExpandedHeight`) dès que l’outil est Zone (`InspectorChrome.MobZone`). Le panneau `MobSpawnZonesPanel` mesure 420 px et défile dans ce dock (`AutoScroll`). Titre : **Zone de monstres**.

| Contrôle | Libellé |
| --- | --- |
| Liste | **Zones** — le nom. Sous le nom, le rectangle `(x, y) largeur×hauteur` |
| Nom | **Nom** — 1–80 caractères |
| Liste | **Monstres** — `nom · quantité N · S s` |
| Combo | **Monstre** — première ligne **Saisie libre**, puis les noms du catalogue |
| Texte | **Identifiant** — guid du monstre |
| Nombre | **Quantité** — vivants en même temps, 1–99, départ à 1 |
| Nombre | **Réapparition (s)** — délai après la mort, 0–86400, départ à 30 |
| Boutons | **Ajouter**, **Retirer**, **Supprimer la zone**, **Actualiser** |

Le combo est rempli par `MapEncounterTroopCatalog.TryLoadAsync` : toutes les fiches `NpcKind.Monster`, brouillon compris (pas de filtre Publié). S’il est vide, l’indication est `Aucun monstre publié — saisissez un identifiant.` S’il a des lignes : `Monstres du catalogue. La saisie libre reste possible.` **Actualiser** relance ce chargement.

Choisir un nom remplit **Identifiant**. **Ajouter** exige un guid (`Choisissez un monstre.` sinon). Plafond : 16 monstres par zone. **Retirer** enlève la ligne choisie. **Supprimer la zone** retire le rectangle.

Sans zone sélectionnée, **Ajouter** affiche `Aucune zone. Tracez un rectangle sur la carte.`

<!-- CAPTURE: Inspecteur Zone de monstres défilé : liste Zones, Nom, liste Monstres, combo Monstre, Identifiant, Quantité 1, Réapparition (s) 30, boutons Ajouter, Retirer, Supprimer la zone, Actualiser -->
*Capture à venir.*

## 4. Publier la carte

La zone n’a pas de bouton Publier à elle. Elle est une colonne de la carte.

1. **Fichier → Enregistrer (PostgreSQL)** écrit le brouillon, y compris `world.maps.mob_spawn_zones_json` (migration `MapMobSpawnZonesJson`, `20261004233000`).
2. **Fichier → Publier (PostgreSQL)…** copie la même colonne sur `world.map_published_snapshots`.

Le serveur lit le snapshot publié (`PostgresPublishedWorldCatalog.ListMobSpawnZonesAsync` : cartes dont `PublishedSnapshotId` est renseigné, jointure sur le snapshot). Un brouillon de carte, ou une zone éditée après la dernière publication, n’est pas celui que le serveur parcourt. Republie la carte après un changement de zone.

Hello reste **11**.

<!-- CAPTURE: Après Publier (PostgreSQL), ligne de statut de l’éditeur avec la carte publiée, zone encore sélectionnée dans l’inspecteur -->
*Capture à venir.*

## 5. Réapparition sur le serveur

`MobSpawnZoneHostedService` tourne en tâche de fond. Un tic dure une seconde (`Interval`).

- Il charge les zones du snapshot publié.
- **Quantité** est le nombre de vivants pour cette ligne. Le manque est comblé.
- La case est tirée au hasard **dans** le rectangle (`MobSpawnZoneEdit.PickTile`). Une case hors zone n’est pas ordonnée (`MobSpawnZoneKeeper`).
- Chaque apparition passe par `CombatGameplayService.SpawnMonsterAsync`, le même monstre que le reste du combat. Un guid qui n’est pas un monstre publié ne produit pas d’instance.
- Le délai **Réapparition (s)** part à la mort observée. Avant l’échéance, cette place ne revient pas. Le tic d’une seconde borne la précision.

Le client voit ces monstres comme les autres (paquet de position, kind monstre). Le protocole Hello ne change pas : il reste **11**.

## Tu es prêt si…

- [ ] Z ouvre l’outil, le menu **Carte** livré ne l’a pas, le rail n’a pas de puce Zone
- [ ] Un rectangle `Zone 1` se trace, se déplace au glisser gauche, se copie au glisser droit
- [ ] Le panneau **Zone de monstres** défile dans l’inspecteur et accepte un monstre
- [ ] Après **Publier** la carte, le serveur ne fait réapparaître ce monstre qu’à l’intérieur du rectangle
- [ ] Hello reste **11**

## Et après ?

- [Objets de carte](MAP_OBJECTS.md) · [Tuiles composées](COMPOSED_TILESETS.md)
- [Quickstart auteur](CREATOR_QUICKSTART.md) § Zone de monstres
- [Inventaire — lacune Z](../../../EDITOR_UI_INVENTORY.md#outil-zone-z-lacune-connue)
- [Modèle de données](../../../DATA_MODEL.md) — `mob_spawn_zones_json`
