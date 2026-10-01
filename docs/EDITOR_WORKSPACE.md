# Espace de travail éditeur — disposition et responsabilités

Chrome livré au tip d’inventaire `8aec6e64` (reprise NET-12 sur `15623c79`). Détail des contrôles : [inventaire UI](EDITOR_UI_INVENTORY.md).

Hello / `FrogWireProtocol.Version` = **11**. Tuiles `TileAsset` **48×48** (`TileAssetMetrics.TargetTileSizePixels`). `WorldMetrics.DefaultTileSizePixels` reste 32 (taille source). Carte v6 pour une identité `TileAsset`.

Coque hybride : `App.xaml` → `MainWindow` (WPF) héberge `MainForm(embedAsWpfChild: true)`. Le menu et la barre d’état WinForms de `MainForm` ne s’affichent pas dans cette coque. Les libellés ci-dessous sont ceux de `MainWindow.xaml`, sauf mention. ADR-0004.

## Disposition (desktop)

Fenêtre `MMO Maker — Éditeur`, minimum 1280×720, ouverte maximisée.

```text
┌─ Menu ──────────────────────────────────────────────────────────────────┐
│ Fichier    Édition    Ressources    Carte    Affichage                  │
├─ Barre d’icônes (Segoe MDL2) ───────────────────────────────────────────┤
│ Nouvelle  Ouvrir  Enregistrer  Annuler  Rétablir  Zoom +/−  Tester      │
├─────────────────┬───────────────────────────────┬───────────────────────┤
│ CARTES          │                               │ Tuiles | Objets |     │
│ ~200 px         │         MapCanvas             │ Entités | Régions     │
│ (180–280)       │    minimap : coin haut-droit  │                       │
│ arbre seulement │    bandeau titre : masqué     │ feuille ou liste      │
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

Colonne droite ~300 px (280–420). Splitters WPF. Largeurs mémorisées dans `editor-workstate.json` (`EditorLocalWorkstate`, `MainWindow.RestoreShellColumnWidths`).

La barre d’icônes porte Nouvelle, Ouvrir, Enregistrer, Annuler, Rétablir, Zoom avant, Zoom arrière, Tester. Les puces de dessin (pinceau, gomme, …) sont dans la radio **Tuiles**, sur le rail droit.

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
| **Tuiles** | Pinceau, Gomme, Remplissage, Rectangle, Ligne, Sélection, options de pot et de rectangle, bouton Pipette, puis la feuille (`TilesetPickerPanelWpf` / `TileAssetWorkbench`). Le curseur (C) n’a pas de puce ici |
| **Objets** | Grille, filtre, facing seulement s’il y a plusieurs variantes. Choisir un objet arme la pose. Le bouton **Placer un objet** (`BtnPlacePrefab`) est masqué |
| **Entités** | Départ, PNJ, Coffre, Porte, Auberge, puce Entités |
| **Régions** | Type de tuile (`ComboTileType`) et puce Région (G). Le libellé radio est **Régions** ; l’énumération reste `EditorPaletteMode.Attributes` |

Raccourcis lettre : B E C F R L M D P N G, I pipette, Alt+clic. Préfab : clic pose, clic droit efface, Ctrl+D duplique, Échap quitte. Entités (N) : clic pose, glisser déplace, clic droit retire. Région (G) : peint 1–63, 0 efface, overlay des numéros. Le détail des menus livrés est dans l’inventaire.

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
- une entité posée, ou l’outil Entités ;
- une tuile inspectée au curseur (C).

Un clic pinceau laisse l’inspecteur replié. Les couches restent visibles dans tous ces cas.

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

Le rail droit ne réécrit plus les minimums du `SplitContainer` à chaque `SizeChanged` (`RightRailLayoutMath`, `MainWindow.QueueShellLayout`, `MainForm.TryAssignSplitDistance`).

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
