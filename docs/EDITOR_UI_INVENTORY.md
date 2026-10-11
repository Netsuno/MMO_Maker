# Inventaire UI — Frog.Editor

Audit au tip `35a8217311eb8cf241b108ffe16a1d0bc1a63019` (`main`).

Baseline chrome : [#135](https://github.com/Netsuno/MMO_Maker/pull/135) G0–G2, [#136](https://github.com/Netsuno/MMO_Maker/pull/136) G3, [#137](https://github.com/Netsuno/MMO_Maker/pull/137) G4, [#138](https://github.com/Netsuno/MMO_Maker/pull/138) démarrage. Depuis l’audit `8aec6e64` : [#142](https://github.com/Netsuno/MMO_Maker/pull/142) et [#143](https://github.com/Netsuno/MMO_Maker/pull/143) (fin du gel au premier affichage), [#145](https://github.com/Netsuno/MMO_Maker/pull/145) (icônes, chevron, noms de carte, colonne gauche), [#146](https://github.com/Netsuno/MMO_Maker/pull/146) (objets de carte), [#147](https://github.com/Netsuno/MMO_Maker/pull/147) (zones de monstres), [#148](https://github.com/Netsuno/MMO_Maker/pull/148) (tuiles composées). Hello / `FrogWireProtocol.Version` = **11**. Tuiles de contenu `TileAsset` **48×48**, carte v6, TilePack et charge CTS1. Ce fichier liste ce qui est dans le code et ce que les guides affirment.

La colonne « documenté ? » est relue au tip contre `EDITOR_WORKSPACE.md` et le quickstart auteur. Le wiki GitHub reste un miroir séparé (pas repris ici).

Chemin livré : `App.xaml` → `MainWindow` (WPF) héberge `MainForm(embedAsWpfChild: true)`. Le menu et la barre d’état WinForms de `MainForm` ne s’affichent pas dans cette coque. Les libellés ci-dessous sont ceux de `MainWindow.xaml`, sauf mention.

`documenté ?` :

| Valeur | Sens |
| --- | --- |
| oui | `EDITOR_WORKSPACE.md` ou le quickstart auteur décrit le contrôle tel qu’il est dans le code |
| non | aucun guide ne décrit le contrôle au tip |
| partiel | une note de chantier, l’inventaire, ou une phrase courte — pas le parcours auteur |
| faux | un doc affirme encore le contraire |

## Chrome

| Control/Feature | code location | documenté ? | notes |
| --- | --- | --- | --- |
| Fenêtre `MMO Maker — Éditeur`, min 1280×720, maximisée | `Frog.Editor/MainWindow.xaml` | oui | |
| Barre d’icônes (Segoe MDL2 Assets, `ToolBar.ButtonStyleKey`) : Nouvelle, Ouvrir, Enregistrer, Annuler, Rétablir, Zoom +/−, Tester | `MainWindow.xaml` ToolBar | oui | Sans cette clé de style, les glyphes privés s’affichent en carrés vides. Les puces de dessin ne sont pas dans cette barre (#135, #145) |
| Chevron de débordement, infobulle **Autres commandes** | `MainWindow.xaml` `OverflowButton` / `PART_ToolBarOverflowPanel` | oui | Masqué (`Visibility=Collapsed`) tant que `HasOverflowItems` est faux. Premier plan `#FFEBEEF5`. Les boutons qui ne tiennent pas passent dans le menu du chevron (#145) |
| Menu Fichier, Édition, Ressources, Carte, Affichage | `MainWindow.xaml` | partiel | Les noms sont dans l’espace de travail. Le détail des entrées est plus bas |
| Colonne gauche **280 px** par défaut (min 180, max 340) : arbre des cartes seulement | `MainWindow.xaml` `ColLeft` ; `MapsProjectPanel` | oui | En-tête **MONDE — cartes**. Défaut XAML `Width="280"` |
| Nom de carte long : troncature au mot (`…`) et infobulle du libellé entier | `MapsProjectPanel.xaml` `TextTrimming="WordEllipsis"` `ToolTip="{Binding}"` ; défilement horizontal `Disabled` | oui | Libellé : 8 caractères d’id, nom, `· rN (publié\|brouillon)` (`FormatEntry`). Un défilement horizontal Auto mesurait le texte en largeur infinie et empêchait la troncature (#145) |
| Centre : `MapCanvas` + minimap coin haut-droit. Bandeau titre carte masqué (`Height` 0) | `MainForm` `_wfMapDockPanel`, `_mapHeader`, `MapMinimapControl` | oui | Le titre interne est encore mis à jour (`UpdateMapChromeLabels`) mais le bandeau n’est pas affiché |
| Colonne droite ~300 px (280–420) | `MainWindow.xaml` `ColRight` | oui | Radios, contenu du mode, couches, inspecteur repliable — pas trois blocs toujours ouverts |
| Largeurs des colonnes mémorisées | `EditorLocalWorkstate` via `MainWindow.RestoreShellColumnWidths` | oui | Lecture si gauche ≥ 180 et droite ≥ 200. Le `MaxWidth` XAML borne ensuite la colonne |

## Palette de modes (rail droit)

Radios dans `Frog.Editor/Panels/EditorLeftToolsWpf.xaml` (le nom de classe n’a pas changé en G4). Le mode actif est le préfixe de la barre d’état (`PaletteModeLabel`).

| Control/Feature | code location | documenté ? | notes |
| --- | --- | --- | --- |
| Radio **Tuiles** | `ModeTiles` | oui | Pinceau, Gomme, Remplissage, Rectangle, Ligne, Sélection, options de pot / rectangle, bouton Pipette, puis la feuille. Pas de puce Zone |
| Radio **Objets** | `ModeObjects` | oui | Grille, filtre, facing seulement s’il y a plusieurs variantes, bouton **Placer un objet** masqué (`BtnPlacePrefab` `Visibility=Collapsed`). Choisir un objet arme la pose. Les objets de carte **publiés** s’y ajoutent (#146) ; un brouillon n’y est pas |
| Radio **Entités** | `ModeEntities` | oui | Départ, PNJ, Coffre, Porte, Auberge, puce Entités |
| Radio **Régions** (plus « Attributs ») | `ModeAttributes` `Content="Régions"` ; enum `EditorPaletteMode.Attributes` | oui | Type de tuile (`ComboTileType`) + puce Région (G). #137 renomme la radio, pas le nom de type |
| Feuille / TileAsset sous Tuiles | `MainForm` `_btnPaletteSheet`, `_btnPaletteAsset`, `TilesetPickerPanelWpf`, `TileAssetCatalogue` | oui | Les tuiles composées **publiées** rejoignent cette palette (`MergePublishedComposedTilesets`). Un brouillon n’y est pas (#148) |
| Couches (~168 px) toujours visibles, y compris en Tuiles et Objets | `LayersProjectPanel` ; `RightRailLayoutMath.LayersDockHeight` | oui | Visibilité, verrou, opacité, « atténuer les autres », menu Ajouter / Supprimer / Renommer / type / verrou |
| Inspecteur replié (hauteur 0) sans sélection contextuelle | `MainForm.ResolveInspectorChrome` ; `InspectorExpandedHeight` = 160 | oui | S’ouvre pour l’outil Région, l’outil Zone, une entité posée ou l’outil Entités, ou une tuile inspectée au curseur (C). Un clic pinceau laisse l’inspecteur replié. `MapPropertiesBar` et `TransferIssuesPanel` restent masqués |
| PropertyGrid tuile | `MainForm._propGrid` | oui | Visible seulement pour une tuile inspectée avec l’outil curseur (C) |

## Gestes canevas

| Control/Feature | code location | documenté ? | notes |
| --- | --- | --- | --- |
| Sélection (M) : clic gauche glissé **dans** le rectangle **déplace** ; clic droit glissé **copie** | `MapCanvas.TryBeginSelectionDrag`, `TryRelocateSelection` | oui | Un pas d’annulation chacun. Tampon privé : Ctrl+C n’est pas touché. L’origine du déplacement est effacée puis recollée. La sélection suit la copie. Les numéros de région restent en place. Maj = couche active. Un glisser **hors** sélection retrace le rectangle. Clic droit hors sélection efface la sélection |
| Double-clic : pipette sans changer d’outil | `MapCanvas.FinishDoubleClickStamp` | oui | Le premier demi-clic (pinceau, pot, rectangle, ligne) est annulé, puis la pipette pose le tampon |
| Ctrl+C / X / V toutes les couches ; Ctrl+Maj = couche active ; Q / H / V ; Suppr | menus Édition ; `EditorToolHotkeys` | oui | La note de chantier `editor-selection-tools/STATUS.md` cite encore un format plus ancien ; le guide auteur décrit le geste actuel |
| Raccourcis lettre B E C F R L M D P N G Z, I pipette, Alt+clic | `EditorToolHotkeys` ; `MainWindow.OnPreviewToolHotkey` | oui | Z = outil Zone. Le curseur (C) et la Zone (Z) n’ont pas de puce dans la palette Tuiles. Z est ignoré dans une zone de texte |
| Prefab : clic pose, clic droit efface, Ctrl+D duplique, Échap quitte | `MapCanvas` outil Prefab ; `EditorLeftToolsWpf` | oui | Distinct du clic droit de la sélection |
| Entités (N) : clic pose, glisser déplace, clic droit retire | `EditorToolHotkeys.StatusHint` | oui | |
| Région (G) : peint 1–63, 0 efface ; overlay des numéros | `MapRegionsPanel`, `MapRegionLabels` | oui | Le panneau est dans l’inspecteur repliable, pas une colonne fixe |
| Ctrl+clic droit sur la carte : menu contexte | `MainForm.OnTileContextMenuRequested` | non | Dupliquer (si prefab), PNJ rapide, Coffre, Porte, Auberge, Événements carte (cette tuile). Le menu WinForms autonome a une entrée désactivée qui le rappelle ; le menu WPF livré ne l’a pas |
| Zoom molette, Ctrl++/Ctrl+−, réinitialiser 100 % | `MapCanvas`, menu Affichage | partiel | |
| Marqueurs et noms d’événements ; aperçu tuiles animées ; raccorder les autotiles | menu Affichage, cases cochées par défaut | partiel | `docs/progress/editor-tile-anim/STATUS.md`. Libellé autotile : `TileAssetFlagLabels.JoinMenu` = « Raccorder les autotiles » |

## Statut et démarrage (NET-20, #138)

| Control/Feature | code location | documenté ? | notes |
| --- | --- | --- | --- |
| Une seule ligne de statut | `EditorStatusFormatter` ; `MainWindow.TileStatusText` ; `MainForm.PushEditorStatusLine` | oui | Ordre : `mode · outil · couche · (x, y) · tuile (a, b) · zoom N %`. Avant survol, la paire monde est (0, 0). Suffixe : avis, révision, modifié, enregistrement, départ, compteurs, catalogue. Pas de second bandeau |
| Avis préfixé si PostgreSQL est configuré mais injoignable | `EditorDatabaseAvailability.OfflineNotice` ; `MainForm.NoteOfflineDatabase` | oui | Texte exact : `PostgreSQL injoignable — édition locale (non enregistrée)`. Il est placé **devant** la ligne (`notice + "    ·    " + ligne`). Le suffixe catalogue devient `mémoire (démo — non persistant)` |
| Repli `InMemoryDemo` sans bloquer le thread UI | `MainForm.OpenMapRepositoryBundleAsync` (`Task.Run` + `CreateBundleAsync`) | oui | Exception socket, délai, E/S ou Npgsql → `InMemoryMapRepository` démo. Chaîne absente → même dépôt démo, **sans** cet avis. `FROG_EDITOR_FORCE_IN_MEMORY=1` → mémoire test, pas cet avis. Save/Publish : `AllowsSave` est faux en démo, donc les commandes WPF sont désactivées ; les libellés WPF restent « Enregistrer (PostgreSQL) » / « Publier (PostgreSQL)… » |
| Le layout du rail ne reboucle plus au démarrage | `RightRailLayoutMath` ; `ShellLayoutDeferral` ; `PaletteChromeHeight` ; `MainWindow.QueueShellLayout` ; `MainForm.TryAssignSplitDistance` | partiel | #138 cesse de réécrire les minimums du `SplitContainer` à chaque `SizeChanged`. #142 et #143 diffèrent encore ce layout tant que le handle de l’hôte WPF existe, et plafonnent la hauteur de la bande d’outils (`PaletteChromeHeight`, budget 16) pour que `ElementHost` ne rappelle pas `PerformLayout` sur la même pile. L’init catalogue ne montre une boîte d’erreur que si l’exception n’est pas le repli hors ligne |

## Outil Zone (Z) — lacune connue

`EditorTool.MobZone`. Libellé outil : **Zone**. Panneau : **Zone de monstres** (`MobSpawnZonesPanel`, hauteur 420, défile dans le dock inspecteur de 160 px). Indication : « Tracez le rectangle. Glisser gauche déplace, glisser droit copie. Les monstres n’apparaissent que dans la zone. »

| Control/Feature | code location | documenté ? | notes |
| --- | --- | --- | --- |
| Touche Z sélectionne l’outil dans la coque livrée | `EditorToolHotkeys.TryResolveWpf` ; `MainWindow.OnPreviewToolHotkey` → `MainForm.SelectEditorTool` | oui | Pas de modificateur. Le panneau s’ouvre (`InspectorChrome.MobZone`) |
| Menu **Carte** WPF : pas d’entrée Zone | `MainWindow.xaml` menu `_Carte` | oui | Les autres outils (gomme, remplissage, rectangle, ligne, départ, prefab, entités, région) y sont. **Zone n’y est pas** |
| Puces du rail droit : pas de Zone | `EditorLeftToolsWpf.xaml` | oui | Radios Tuiles, Objets, Entités, Régions seulement |
| Menu WinForms autonome : **Outil zone de monstres (Z)** | `MainForm` construit le `MenuStrip` seulement si `embedAsWpfChild` est faux | oui | Ce menu n’est pas celui de `App.xaml`. Dans l’éditeur livré, l’outil n’est donc joignable que par la touche Z |
| Gestes | `MapCanvas` outil `MobZone` | oui | Rectangle nouveau ; glisser gauche déplace ; glisser droit copie. Les numéros de région et la peinture de tuile ne sont pas ce geste |
| Contenu du panneau | `MobSpawnZonesPanel` ; `MobSpawnZoneLabels` | partiel | Nom, liste des zones, monstre (catalogue publié ou saisie libre), identifiant, quantité, réapparition en secondes, Ajouter, Retirer, Supprimer la zone, Actualiser. Le quickstart ne déroule pas ce formulaire |
| Persistance | `world.maps.mob_spawn_zones_json` et la même colonne sur `world.map_published_snapshots` | partiel | JSON de la carte, brouillon et snapshot publié. Hors `.fmap`, hors Hello. Voir `DATA_MODEL.md` |
| Réapparition serveur, à l’intérieur de la zone seulement | `MobSpawnZoneHostedService` ; `MobSpawnZoneKeeper` | partiel | Tic d’une seconde. Le délai part à la mort observée. Chaque apparition passe par `SpawnMonsterAsync` (même monstre que le reste du combat). Une tuile hors zone n’est pas ordonnée. Hello reste **11** |

`ToolPalette` (non livré) énumère `EditorTool` et listerait donc Zone. Ce n’est pas une surface de l’éditeur lancé.

## Menus livrés (WPF)

Le menu WinForms de `MainForm` (fenêtre non embarquée, pas le démarrage `App.xaml`) diffère : il a « Importer un asset projet… », il a « Outil zone de monstres (Z) », et il n’a pas « Actualiser le catalogue ». Ce n’est pas la coque lancée.

| Control/Feature | code location | documenté ? | notes |
| --- | --- | --- | --- |
| Fichier : Nouvelle carte…, Ouvrir fichier…, Enregistrer (PostgreSQL), Publier (PostgreSQL)…, Exporter .fmap…, Lancer le client…, Tester (playtest)…, Arrêter le test, Quitter | `MainWindow.xaml` | partiel | Quickstart auteur cite Nouvelle carte / Enregistrer / publish, pas le reste. Playtest GUI manuel reste « accepted / skipped » dans les guides P10. Pas de menu MariaDB (#83) |
| Édition : Annuler, Rétablir, copier/couper/coller zone, rotation, miroirs, pipette, Dupliquer l’objet, modèles | `MainWindow.xaml` | partiel | Modèles : `docs/progress/editor-map-templates/STATUS.md`. Dialogue `MapTemplateListDialog` |
| Ressources : Données de jeu…, Événements communs…, image tuiles, feuille TileAsset, animer / retirer l’animation, Groupe d’autotile de la tuile… | `MainWindow.xaml` | partiel | Données de jeu : chantier Phase 6 + fiches plus récentes, pas un guide |
| Carte : Valider, Propriétés, Taille et décalage…, passer en TileAsset v6, Vérifier les transferts…, outils (raccourcis, **sans Zone**), warp, Actualiser le catalogue (F5), PNJ rapide, Coffre, Porte, Auberge, Événements carte, Contenu Phase 8, Événements communs, Actualiser marqueurs | `MainWindow.xaml` | partiel | « Vérifier les transferts… » ouvre encore la liste ; le panneau n’est plus ancré sous l’inspecteur. L’absence de l’outil Zone dans ce menu est la lacune ci-dessus |

## Dialogues et éditeurs (hors chrome carte)

| Control/Feature | code location | documenté ? | notes |
| --- | --- | --- | --- |
| Données de jeu : Tilesets, NPCs / monstres, Objets, Sorts / compétences, Classes, Héros, Compétences, Système, Boutiques, Ressources / spawns, Armes, Armures, **Objets de carte**, **Tuiles composées** | `GameDataForm` | partiel | Boutons Nouveau, Dupliquer, Enregistrer brouillon, Publier, Supprimer. Hors ligne : dépôts mémoire démo (`GameDataInitializationService`). « Objets » reste l’inventaire. « Objets de carte » (index 12) et « Tuiles composées » (index 13) sont après Armures |
| Objets de carte : brouillon / publication, props placables | `MapObjectEditorPanel` | partiel | Nom, chemin logique, identifiant de placement, empreinte en tuiles, taille en pixels, SHA-256, aperçu, Importer…. Publier alimente la radio **Objets** (`ReloadPublishedMapObjectsIntoPaletteAsync`). Un brouillon n’y va pas. Distinct des objets d’inventaire (#146) |
| Tuiles composées : tuiles 48×48 choisies, pas une feuille découpée | `ComposedTilesetEditorPanel` | partiel | Nom, chemin logique, liste ordonnée (Ajouter une tuile…, Monter, Descendre, Retirer). Publier alimente la palette TileAsset (`ReloadPublishedComposedTilesetsIntoPaletteAsync`). Les palettes locales (sans identifiant serveur) restent. Le client charge la charge signée **CTS1** (`ComposedTilesetClientLoader`, `GET /content/composed-tilesets`) ; un catalogue JSON brut est refusé. Hello reste **11** (#148) |
| Contenu Phase 8 : Dialogue, Quête, Événement commun, Métier, Recette, Région, Profil météo | `Phase8ContentBrowseDialog` | partiel | Nécessite PostgreSQL. Wiki « Référence — Éditeur » renvoie au catalogue Phase 8, pas à cet écran |
| Événements carte, pages, palette de commandes | `MapEventsBrowseDialog`, `MapEventPageEditorDialog`, `MapEventCommandPaletteBar` | faux | `docs/progress/phase-08-quests-events-advanced-creation/COMMAND_CATALOG.md` s’arrête à `call_common_event`. La palette code (`MapEventCommandPalette`) ajoute choix, images, déplacement/teinte d’image, fondu, teinte, tremblement, flash, défilement, animation, audio, boutique, or, objets, interrupteur, variable, branche, météo, niveau, EXP, paramètres, compétences, équipement, nom, classe, récupération, PV/PM (#86, #103, #108–#112, #115, #123–#126, #128, #130–#133) |
| PNJ rapide, coffre, porte, auberge | `QuickTalkingNpcDialog`, `QuickEventPresetDialog` | partiel | PostgreSQL requis, puis clic sur la carte |
| Propriétés carte, taille et décalage, destination de warp, spawn playtest, navigateur audio | `MapPropertiesDialog`, `MapResizeShiftDialog`, `WarpDestinationDialog`, `PlaytestSpawnDialog`, `AudioResourceBrowserDialog` | partiel | BGM/SE carte : PR #92. Audio : `docs/progress/audio/STATUS.md` côté client, navigateur éditeur #105 peu repris |
| Drapeaux TileAsset (passage, échelle, buisson, interaction, sol blessant, terrain) | `TileAssetFlagsPanel` | partiel | `docs/progress/editor-tile-flags/STATUS.md` |
| Événements communs | `CommonEventsEditorDialog` | partiel | #96. Aussi dans le menu Ressources |

## Présents dans le projet, pas une surface livrée

| Control/Feature | code location | documenté ? | notes |
| --- | --- | --- | --- |
| `ToolPalette`, `TileTypePalette`, `PaletteView` | `Frog.Editor/Controls/` | non | Aucune instanciation. Ne pas les décrire comme l’UI |
| `Phase8JsonEditorPanel` | `Forms/Phase8/Phase8JsonEditorPanel.cs` | partiel | `KNOWN_ISSUES.md` Phase 10 : fichier non branché. Toujours vrai |
| `Form1` | `Frog.Editor/Form1.cs` | non | Formulaire vide, pas le shell |
| `PublishMapDialog.TryValidate` | absent | faux | `docs/reference/editor/publishmapdialogtryvalidate.md` documente encore le dialogue MariaDB. Le fichier n’est plus dans `Frog.Editor` |

## Ce que les docs affirment

| Document | Claim vs tip `35a82173` |
| --- | --- |
| `docs/EDITOR_WORKSPACE.md` | Chrome réel : barre d’icônes et chevron, cartes à gauche (280 px), troncature des noms, radios à droite, inspecteur, touche Z, lacune du menu WPF. Aligné dans cette reprise |
| `docs/ARCHITECTURE.md` | Coque WPF + îlots WinForms : encore vrai (ADR-0004). Pointe l’espace de travail et cet inventaire |
| `docs/progress/phase-10-beta-release/guides/CREATOR_QUICKSTART.md` | Parcours peindre → enregistrer → publier, palette, statut, repli hors ligne. Colonne gauche **280 px**, chevron, noms tronqués, touche Z. Pas un tutoriel du panneau Zone ni des formulaires Objets de carte / Tuiles composées |
| Wiki GitHub `Home`, `Auteur`, `Référence-Éditeur`, `Référence-Editor` | Miroir figé (tip docs cité `bf1677aa` / branche `cursor/phase10-beta-release`). Hors de ce dépôt |
| `docs/reference/editor/` | Quatre fiches Save/Publish, tip `ea116afa`. L’une décrit un dialogue supprimé |
| `docs/progress/phase-10-beta-release/KNOWN_ISSUES.md` | Note historique : menu MariaDB retiré en #83, absent déjà au tip `8aec6e64`. Non réécrite ici |
| `docs/progress/editor-*/STATUS.md` et `COMMAND_CATALOG.md` | Notes de chantier, pas des guides. Elles gardent le texte de leur époque |
| `docs/DATA_MODEL.md` | Tables et colonnes de `MapObjectDraftPublish`, `MapMobSpawnZonesJson`, `ComposedTilesetDraftPublish` ajoutées à ce tip |
| `Frog.Editor/Docs/analyse_edition_map.md` | Analyse VB6. Pas l’UI C# actuelle |

## Écarts encore ouverts

Repris de l’audit `8aec6e64` après la reprise NET-12 (écarts 1–5 traités dans les guides ; écart 9 marqué historique). Restent :

1. Aligner le wiki (`Home`, `Auteur`, `Référence*`) sur `docs/` au tip, pas sur `bf1677aa`. Le wiki se déclare miroir ; la source reste le dépôt.
2. Étendre le texte auteur des commandes d’événement au-delà de `COMMAND_CATALOG.md` (s’arrête à `call_common_event`) pour la palette `MapEventCommandPalette`. Les fiches `docs/progress/*/STATUS.md` ne suffisent pas comme guide.
3. Couvrir dans les guides, pas seulement en STATUS, les surfaces éditeur encore résumées : Héros, Compétences, Système, Armes, Armures, régions/rencontres, drapeaux de tuile, autotile, taille et décalage, audio carte, événements communs, et le détail des formulaires Objets de carte / Tuiles composées / Zone de monstres.
4. `docs/reference/editor/publishmapdialogtryvalidate.md` décrit un dialogue absent. Le reste de `docs/reference/editor/` est au tip `ea116afa`.
5. **Lacune produit, pas un oubli de doc :** dans la coque WPF livrée, l’outil Zone n’a ni entrée de menu Carte ni puce de rail. Il s’ouvre avec la touche Z. Le menu WinForms autonome l’a (`Outil zone de monstres (Z)`) mais `App.xaml` ne lance pas cette fenêtre. Ce lot ne change pas le code.
6. Captures : ne pas inventer de PNG. Les guides auteur citent des images déjà dans le dépôt ; ce rattrapage n’en ajoute pas.
