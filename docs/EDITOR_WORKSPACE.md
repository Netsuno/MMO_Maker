# Espace de travail éditeur — disposition et responsabilités

Chrome livré au tip `35a8217311eb8cf241b108ffe16a1d0bc1a63019`. Détail des contrôles : [inventaire UI](EDITOR_UI_INVENTORY.md).

Hello / `FrogWireProtocol.Version` = **11**. Tuiles de contenu **48×48** (`TileAsset`, carte v6, TilePack, charge CTS1).

Coque hybride : `App.xaml` → `MainWindow` (WPF) héberge `MainForm(embedAsWpfChild: true)`. Le menu et la barre d’état WinForms de `MainForm` ne s’affichent pas dans cette coque. Les libellés ci-dessous sont ceux de `MainWindow.xaml`, sauf mention. ADR-0004.

## Disposition (desktop)

Fenêtre `MMO Maker — Éditeur`, minimum 1280×720, ouverte maximisée.

```text
┌─ Menu ──────────────────────────────────────────────────────────────────┐
│ Fichier    Édition    Ressources    Carte    Affichage                  │
├─ Barre d’icônes (Segoe MDL2) ───────────────────────────────────────────┤
│ Nouvelle  Ouvrir  Enregistrer  Annuler  Rétablir  Zoom +/−  Tester      │
│ chevron « Autres commandes » seulement si la barre déborde               │
├─────────────────┬───────────────────────────────┬───────────────────────┤
│ MONDE — cartes  │                               │ Tuiles | Objets |     │
│ 280 px          │         MapCanvas             │ Entités | Régions     │
│ (180–340)       │    minimap : coin haut-droit  │                       │
│ noms longs : …  │    bandeau titre : masqué     │ feuille ou liste      │
│                 │                               │ du mode actif         │
│                 │                               ├───────────────────────┤
│                 │                               │ COUCHES ~168 px       │
│                 │                               │ toujours visibles     │
│                 │                               ├───────────────────────┤
│                 │                               │ INSPECTEUR            │
│                 │                               │ hauteur 0 si replié   │
│                 │                               │ ~160 px si ouvert     │
├─────────────────┴───────────────────────────────┴───────────────────────┤
│ mode · outil · couche · (x, y) · tuile (a, b) · zoom N %                │
└─────────────────────────────────────────────────────────────────────────┘
```

Colonne droite ~300 px (280–420). Splitters WPF. Largeurs mémorisées dans `editor-workstate.json` (`EditorLocalWorkstate`, `MainWindow.RestoreShellColumnWidths`). La colonne gauche part à **280 px** (`ColLeft`, minimum 180, maximum 340).

La barre d’icônes porte Nouvelle, Ouvrir, Enregistrer, Annuler, Rétablir, Zoom avant, Zoom arrière, Tester. Les glyphes sont Segoe MDL2 Assets, posés par `ToolBar.ButtonStyleKey`. Si la fenêtre est trop étroite pour tous les boutons, un chevron sombre s’affiche ; son infobulle est **Autres commandes**. Il reste masqué tant que tout tient. Les puces de dessin (pinceau, gomme, …) sont dans la radio **Tuiles**, sur le rail droit.

L’arbre de gauche titre **MONDE — cartes**. Un nom long est coupé au mot, avec `…`, et l’infobulle reprend le libellé entier (id court, nom, révision, publié ou brouillon).

Le bandeau interne « CARTE : Nom (W × H) » est encore mis à jour (`UpdateMapChromeLabels`) et reste masqué (`Height` 0). La minimap (`MapMinimapControl`) est dans le coin haut-droit du canevas.

## Responsabilités des panneaux

| Zone | Contrôle | Responsabilité | Ne fait pas |
| --- | --- | --- | --- |
| Menu / icônes | `MainWindow` | Nouvelle carte, ouvrir un fichier, enregistrer, annuler, rétablir, zoom, tester | Accès DB direct |
| Cartes (gauche) | `MapsProjectPanel` | Arbre des cartes du catalogue (`MapId`) | Outils de dessin, SQL |
| Canevas | `MapCanvas` | Rendu, caméra, peinture, gestes | Repository |
| Rail droit | `EditorLeftToolsWpf` | Radios **Tuiles \| Objets \| Entités \| Régions**. Le nom de classe n’a pas changé | Persistance |
| Couches | `LayersProjectPanel` | Visibilité, verrou, opacité, « atténuer les autres », menu Ajouter / Supprimer / Renommer / type / verrou. Dock ~168 px (`RightRailLayoutMath.LayersDockHeight`), y compris en Tuiles et Objets | Sauvegarde PostgreSQL |
| Inspecteur | `MainForm.ResolveInspectorChrome` | Région, entité ou tuile au curseur. ~160 px ouverts (`InspectorExpandedHeight`), hauteur 0 replié | Métadonnées carte en permanence |
| Statut | `EditorStatusFormatter` → `MainWindow.TileStatusText` | Une seule ligne | Second bandeau |

`MapPropertiesBar` et `TransferIssuesPanel` restent masqués. Les propriétés de carte passent par le menu **Carte → Propriétés**. Le `PropertyGrid` (`MainForm._propGrid`) ne s’affiche que pour une tuile inspectée avec l’outil curseur (C).

## Palette (rail droit)

Le mode actif est le préfixe de la ligne de statut (`PaletteModeLabel`).

| Radio | Contenu |
| --- | --- |
| **Tuiles** | Pinceau, Gomme, Remplissage, Rectangle, Ligne, Sélection, options de pot et de rectangle, bouton Pipette, puis la feuille (`TilesetPickerPanelWpf` / palette TileAsset). Le curseur (C) et la Zone (Z) n’ont pas de puce ici. Une tuile composée **publiée** rejoint cette palette ; un brouillon n’y est pas |
| **Objets** | Grille, filtre, facing seulement s’il y a plusieurs variantes. Choisir un objet arme la pose. Le bouton **Placer un objet** (`BtnPlacePrefab`) est masqué. Un objet de carte **publié** (Données de jeu) s’ajoute ici ; un brouillon n’y est pas |
| **Entités** | Départ, PNJ, Coffre, Porte, Auberge, puce Entités |
| **Régions** | Type de tuile (`ComboTileType`) et puce Région (G). Le libellé radio est **Régions** ; l’énumération reste `EditorPaletteMode.Attributes` |

Raccourcis lettre : B E C F R L M D P N G Z, I pipette, Alt+clic. Préfab : clic pose, clic droit efface, Ctrl+D duplique, Échap quitte. Entités (N) : clic pose, glisser déplace, clic droit retire. Région (G) : peint 1–63, 0 efface, overlay des numéros. Zone (Z) : voir plus bas. Le détail des menus livrés est dans l’inventaire.

## Ligne de statut (#137)

Une seule ligne, ordre fixe :

`mode · outil · couche · (x, y) · tuile (a, b) · zoom N %`

Avant survol, la paire monde est `(0, 0)`.

Le suffixe, après cette paire, porte l’avis, la révision, l’état modifié, l’enregistrement, le départ, les compteurs et le catalogue. Pas de second bandeau.

Si PostgreSQL est configuré mais injoignable, l’avis est placé **devant** la ligne :

`PostgreSQL injoignable — édition locale (non enregistrée)` + `    ·    ` + ligne

Le suffixe catalogue devient alors `mémoire (démo — non persistant)`.

## Inspecteur (#137)

Replié (hauteur 0) sans sélection contextuelle. Il s’ouvre (~160 px) pour :

- l’outil Région ;
- l’outil Zone (le panneau **Zone de monstres** mesure 420 px et défile dans ce dock) ;
- une entité posée, ou l’outil Entités ;
- une tuile inspectée au curseur (C).

Un clic pinceau laisse l’inspecteur replié. Les couches restent visibles dans tous ces cas.

## Outil Zone (Z) — lacune du menu livré

La touche Z, sans modificateur, sélectionne `EditorTool.MobZone` dans la coque WPF (`MainWindow.OnPreviewToolHotkey`). Le canevas trace un rectangle ; un glisser gauche déplace la zone, un glisser droit la copie. Le panneau **Zone de monstres** nomme la zone et y pose des monstres (quantité, réapparition en secondes). Le serveur ne les fait réapparaître qu’à l’intérieur de la zone publiée. Hello reste **11**.

Le menu **Carte** de `MainWindow.xaml` n’a pas cette entrée, et le rail droit n’a pas de puce Zone. L’entrée **Outil zone de monstres (Z)** existe seulement sur le `MenuStrip` WinForms, construit quand `MainForm` n’est pas embarqué. `App.xaml` embarque le formulaire : dans l’éditeur livré, on ouvre l’outil avec la touche Z. Parcours : [Zones de monstres](progress/phase-10-beta-release/guides/MOB_ZONES.md). Lacune : [inventaire](EDITOR_UI_INVENTORY.md#outil-zone-z-lacune-connue).

## Objets de carte et tuiles composées

**Ressources → Données de jeu…** ajoute deux catégories après Armures.

- **Objets de carte** (`MapObjectEditorPanel`) : props placables, distincts des objets d’inventaire. Brouillon, puis Publier. Seule la publication entre dans la radio **Objets**, au moment où Données de jeu se ferme (`MainWindow.OpenGameData` → `QueuePublishedMapObjectReload`). Guide : [Objets de carte](progress/phase-10-beta-release/guides/MAP_OBJECTS.md).
- **Tuiles composées** (`ComposedTilesetEditorPanel`) : tuiles **48×48** choisies, pas une feuille découpée. Brouillon, puis Publier. Seule la publication rejoint la palette TileAsset, au chargement du catalogue (démarrage de l’éditeur). Fermer Données de jeu depuis la coque WPF ne relance pas cette fusion : `QueuePublishedComposedTilesetReload` est sur `MainForm.OpenGameData`, menu que `App.xaml` n’affiche pas. Le client charge ensuite la charge signée CTS1 (`GET /content/composed-tilesets`). Un catalogue JSON brut est refusé. Guide : [Tuiles composées](progress/phase-10-beta-release/guides/COMPOSED_TILESETS.md).

## Gestes de sélection (#136)

Outil Sélection (M), en plus du copier-coller clavier (Ctrl+C / X / V, Ctrl+Maj = couche active) :

- clic gauche glissé **dans** le rectangle : **déplace** ;
- clic droit glissé : **copie** ;
- un pas d’annulation chacun ; tampon privé (Ctrl+C n’est pas touché) ; l’origine du déplacement est effacée puis recollée ; la sélection suit la copie ;
- les numéros de région restent en place ;
- Maj = couche active ;
- un glisser **hors** sélection retrace le rectangle ; un clic droit hors sélection efface la sélection ;
- double-clic : pipette sans changer d’outil (le premier demi-clic pinceau, pot, rectangle ou ligne est annulé, puis la pipette pose le tampon).

Note de chantier : [`progress/editor-selection-tools/STATUS.md`](progress/editor-selection-tools/STATUS.md).

## Démarrage (#138, NET-20)

Le thread UI reste libre. `MainForm.OpenMapRepositoryBundleAsync` charge le catalogue avec `Task.Run` + `EditorMapRepositoryFactory.CreateBundleAsync`.

| Situation | Dépôt | Avis | Enregistrer / Publier (WPF) |
| --- | --- | --- | --- |
| PostgreSQL répond | PostgreSQL | aucun | actifs si `AllowsSave` |
| Chaîne configurée, socket / délai / E/S / Npgsql | `InMemoryMapRepository` + `InMemoryDemo` | texte exact ci-dessus | inactifs (`AllowsSave` faux) |
| Chaîne absente | même dépôt démo | **sans** cet avis | inactifs |
| `FROG_EDITOR_FORCE_IN_MEMORY=1` | mémoire test | **sans** cet avis | selon `AllowsSave` de la mémoire test |

Les libellés WPF restent « Enregistrer (PostgreSQL) » et « Publier (PostgreSQL)… », y compris quand les commandes sont inactives. Une boîte d’erreur au démarrage du catalogue n’apparaît que si l’exception n’est pas ce repli.

Le rail droit ne réécrit plus les minimums du `SplitContainer` à chaque `SizeChanged` (`RightRailLayoutMath`, `MainWindow.QueueShellLayout`, `MainForm.TryAssignSplitDistance`). #142 et #143 diffèrent encore ce layout tant que le handle de l’hôte WPF existe, et plafonnent la hauteur de la bande d’outils (`PaletteChromeHeight`) pour que le premier affichage ne reboucle pas.

## Couche application

- Ports : `IMapRepository` (`ListSummariesAsync`, `LoadByIdAsync`, `SaveAsync`).
- Identité : `MapId` (`Guid`) — pas de `LegacyId` dans le chemin actif.
- Session : `MapWorkspaceSession` orchestre catalogue + carte courante (pas d’UI).
- Composition : `EditorMapRepositoryFactory` choisit PostgreSQL (chaîne) ou mémoire (démo hors DB), comme le tableau de démarrage.
- Formulaires / code-behind : **aucun** `DbContext` / `Npgsql` / `MySqlConnection`.

## Carte démo

Au démarrage, la session ouvre une carte démo moderne (mémoire ou seed PostgreSQL si le catalogue est vide).

## Smoke Windows

- Projet : `tests/Frog.Editor.WindowsSmokeTests`
- CI : job `build-and-test` (Windows) avec `FROG_EDITOR_FORCE_IN_MEMORY=1` (mémoire test, pas l’avis hors ligne)
- Script manuel : `scripts/windows-editor-smoke.ps1`
