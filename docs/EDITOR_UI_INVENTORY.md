# Inventaire UI — Frog.Editor

Audit au tip `8aec6e64eda0184abc5214cfa81d5216ec331b0d` (`main`).

Baseline chrome : [#135](https://github.com/Netsuno/MMO_Maker/pull/135) G0–G2, [#136](https://github.com/Netsuno/MMO_Maker/pull/136) G3, [#137](https://github.com/Netsuno/MMO_Maker/pull/137) G4, [#138](https://github.com/Netsuno/MMO_Maker/pull/138) démarrage. Hello / `FrogWireProtocol.Version` = **11**. Tuiles `TileAsset` **48×48**, carte v6. Ce fichier liste ce qui est dans le code et ce que les docs affirment. Il ne réécrit pas les guides (NET-12).

Chemin livré : `App.xaml` → `MainWindow` (WPF) héberge `MainForm(embedAsWpfChild: true)`. Le menu et la barre d’état WinForms de `MainForm` ne s’affichent pas dans cette coque. Les libellés ci-dessous sont ceux de `MainWindow.xaml`, sauf mention.

`documenté ?` :

| Valeur | Sens |
| --- | --- |
| non | aucun guide ni wiki ne décrit le contrôle au tip |
| partiel | une note de chantier ou une API existe, pas un guide auteur à jour |
| faux | un doc affirme encore le contraire |

## Chrome

| Control/Feature | code location | documenté ? | notes |
| --- | --- | --- | --- |
| Fenêtre `MMO Maker — Éditeur`, min 1280×720, maximisée | `Frog.Editor/MainWindow.xaml` | partiel | `docs/EDITOR_WORKSPACE.md` décrit encore le wireframe Phase 3 |
| Barre d’icônes (Segoe MDL2) : Nouvelle, Ouvrir, Enregistrer, Annuler, Rétablir, Zoom +/−, Tester | `MainWindow.xaml` ToolBar | faux | Le wireframe montre une barre d’outils générique au-dessus d’outils texte à gauche. Les puces de dessin ne sont plus dans cette barre (#135) |
| Menu Fichier, Édition, Ressources, Carte, Affichage | `MainWindow.xaml` | partiel | Les noms de menus existent dans le wireframe. Le contenu (voir plus bas) n’y est pas |
| Colonne gauche ~200 px (180–280) : arbre des cartes seulement | `MainWindow.xaml` `ColLeft` ; `MapsProjectPanel` ; `MainForm` `_leftColumnPanel` | faux | `EDITOR_WORKSPACE.md` met outils + types de tuile dans la colonne gauche |
| Centre : `MapCanvas` + minimap coin haut-droit. Bandeau titre carte masqué (`Height` 0) | `MainForm` `_wfMapDockPanel`, `_mapHeader`, `MapMinimapControl` | partiel | Le wireframe montre le titre « CARTE : Nom (W × H) » et la minimap dans le canevas. Le titre interne est encore mis à jour (`UpdateMapChromeLabels`) mais le bandeau n’est pas affiché |
| Colonne droite ~300 px (280–420) | `MainWindow.xaml` `ColRight` | faux | Le wireframe y place tuiles, couches et PropertyGrid comme trois blocs toujours ouverts |
| Largeurs des colonnes mémorisées | `EditorLocalWorkstate` via `MainWindow.RestoreShellColumnWidths` | partiel | `EDITOR_WORKSPACE.md` le dit, sans le nouveau gabarit |

## Palette de modes (rail droit)

Radios dans `Frog.Editor/Panels/EditorLeftToolsWpf.xaml` (le nom de classe n’a pas changé en G4). Le mode actif est le préfixe de la barre d’état (`PaletteModeLabel`).

| Control/Feature | code location | documenté ? | notes |
| --- | --- | --- | --- |
| Radio **Tuiles** | `ModeTiles` | non | Pinceau, Gomme, Remplissage, Rectangle, Ligne, Sélection, options de pot / rectangle, bouton Pipette, puis la feuille |
| Radio **Objets** | `ModeObjects` | partiel | `docs/progress/prefabs/STATUS.md` décrit l’outil P et le clic droit qui efface encore (`MapCanvas`, outil Prefab). La grille visuelle, le filtre, le facing seulement s’il y a plusieurs variantes, et le bouton **Placer un objet** masqué (`BtnPlacePrefab` `Visibility=Collapsed`) ne sont pas dans un guide. Choisir un objet arme la pose |
| Radio **Entités** | `ModeEntities` | partiel | Départ, PNJ, Coffre, Porte, Auberge, puce Entités. Les chantiers spawn / presets existent ; pas le regroupement dans cette radio |
| Radio **Régions** (plus « Attributs ») | `ModeAttributes` `Content="Régions"` ; enum `EditorPaletteMode.Attributes` | non | Type de tuile (`ComboTileType`) + puce Région (G). #137 renomme la radio, pas le nom de type |
| Feuille / TileAsset sous Tuiles | `MainForm` `_btnPaletteSheet`, `_btnPaletteAsset`, `TilesetPickerPanelWpf`, `TileAssetWorkbench` | partiel | Chantiers tileset / TileAsset. Pas dans le quickstart |
| Couches (~168 px) toujours visibles, y compris en Tuiles et Objets | `LayersProjectPanel` ; `RightRailLayoutMath.LayersDockHeight` | partiel | Visibilité, verrou, opacité, « atténuer les autres », menu Ajouter / Supprimer / Renommer / type / verrou. Le wireframe dit « visibilité / verrou » sans le dock compact |
| Inspecteur replié (hauteur 0) sans sélection contextuelle | `MainForm.ResolveInspectorChrome` ; `InspectorExpandedHeight` = 160 | non | S’ouvre pour l’outil Région, une entité posée ou l’outil Entités, ou une tuile inspectée au curseur (C). Un clic pinceau ne laisse pas la grille ouverte. `MapPropertiesBar` et `TransferIssuesPanel` restent masqués |
| PropertyGrid tuile | `MainForm._propGrid` | faux | Le wireframe en fait le panneau permanent des métadonnées carte |

## Gestes canevas

| Control/Feature | code location | documenté ? | notes |
| --- | --- | --- | --- |
| Sélection (M) : clic gauche glissé **dans** le rectangle **déplace** ; clic droit glissé **copie** | `MapCanvas.TryBeginSelectionDrag`, `TryRelocateSelection` | non | Un pas d’annulation chacun. Tampon privé : Ctrl+C n’est pas touché. L’origine du déplacement est effacée puis recollée (chevauchement sûr). La sélection suit la copie. Les numéros de région restent en place. Maj = couche active, comme Ctrl+Maj sur le presse-papiers. Un glisser **hors** sélection retrace le rectangle. Clic droit hors sélection efface la sélection |
| Double-clic : pipette sans changer d’outil | `MapCanvas.FinishDoubleClickStamp` | non | Le premier demi-clic (pinceau, pot, rectangle, ligne) est annulé, puis la pipette pose le tampon |
| Ctrl+C / X / V toutes les couches ; Ctrl+Maj = couche active ; Q / H / V ; Suppr | menus Édition ; `EditorToolHotkeys` | partiel | `docs/progress/editor-selection-tools/STATUS.md` (PR #53, format carte encore cité en v5). Pas le déplacement/copie au glisser (#136) |
| Raccourcis lettre B E C F R L M D P N G, I pipette, Alt+clic | `EditorToolHotkeys` | partiel | Le curseur (C) n’a pas de puce dans la palette Tuiles |
| Prefab : clic pose, clic droit efface, Ctrl+D duplique, Échap quitte | `MapCanvas` outil Prefab ; `EditorLeftToolsWpf` | partiel | Distinct du clic droit de la sélection |
| Entités (N) : clic pose, glisser déplace, clic droit retire | `EditorToolHotkeys.StatusHint` | partiel | |
| Région (G) : peint 1–63, 0 efface ; overlay des numéros | `MapRegionsPanel`, `MapRegionLabels` | partiel | `docs/progress/editor-map-regions/STATUS.md`. Le panneau est dans l’inspecteur repliable, pas une colonne fixe |
| Ctrl+clic droit sur la carte : menu contexte | `MainForm.OnTileContextMenuRequested` | non | Dupliquer (si prefab), PNJ rapide, Coffre, Porte, Auberge, Événements carte (cette tuile). Le menu WinForms autonome a une entrée désactivée qui le rappelle ; le menu WPF livré ne l’a pas |
| Zoom molette, Ctrl++/Ctrl+−, réinitialiser 100 % | `MapCanvas`, menu Affichage | partiel | |
| Marqueurs et noms d’événements ; aperçu tuiles animées ; raccorder les autotiles | menu Affichage, cases cochées par défaut | partiel | `docs/progress/editor-tile-anim/STATUS.md`. Libellé autotile : `TileAssetFlagLabels.JoinMenu` = « Raccorder les autotiles » |

## Statut et démarrage (NET-20, #138)

| Control/Feature | code location | documenté ? | notes |
| --- | --- | --- | --- |
| Une seule ligne de statut | `EditorStatusFormatter` ; `MainWindow.TileStatusText` ; `MainForm.PushEditorStatusLine` | non | Ordre : `mode · outil · couche · (x, y) · tuile (a, b) · zoom N %`. Avant survol, la paire monde est (0, 0). Suffixe : avis, révision, modifié, enregistrement, départ, compteurs, catalogue. Pas de second bandeau |
| Avis préfixé si PostgreSQL est configuré mais injoignable | `EditorDatabaseAvailability.OfflineNotice` ; `MainForm.NoteOfflineDatabase` | non | Texte exact : `PostgreSQL injoignable — édition locale (non enregistrée)`. Il est placé **devant** la ligne (`notice + "    ·    " + ligne`). Le suffixe catalogue devient `mémoire (démo — non persistant)` |
| Repli `InMemoryDemo` sans bloquer le thread UI | `MainForm.OpenMapRepositoryBundleAsync` (`Task.Run` + `CreateBundleAsync`) | non | Exception socket, délai, E/S ou Npgsql → `InMemoryMapRepository` démo. Chaîne absente → même dépôt démo, **sans** cet avis. `FROG_EDITOR_FORCE_IN_MEMORY=1` → mémoire test, pas cet avis. Save/Publish : `AllowsSave` est faux en démo, donc les commandes WPF sont désactivées ; les libellés WPF restent « Enregistrer (PostgreSQL) » / « Publier (PostgreSQL)… » |
| Le layout du rail ne reboucle plus au démarrage | `RightRailLayoutMath` ; `MainWindow.QueueShellLayout` ; `MainForm.TryAssignSplitDistance` | non | Les minimums de `SplitContainer` ne sont plus réécrits à chaque `SizeChanged` (cause du gel UI au tip G4, corrigé par #138). L’init catalogue ne montre une boîte d’erreur que si l’exception n’est pas ce repli |

## Menus livrés (WPF)

Le menu WinForms de `MainForm` (fenêtre non embarquée, pas le démarrage `App.xaml`) diffère : il a « Importer un asset projet… » et n’a pas « Actualiser le catalogue ». Ce n’est pas la coque lancée.

| Control/Feature | code location | documenté ? | notes |
| --- | --- | --- | --- |
| Fichier : Nouvelle carte…, Ouvrir fichier…, Enregistrer (PostgreSQL), Publier (PostgreSQL)…, Exporter .fmap…, Lancer le client…, Tester (playtest)…, Arrêter le test, Quitter | `MainWindow.xaml` | partiel | Quickstart auteur cite Nouvelle carte / Enregistrer / publish, pas le reste. Playtest GUI manuel reste « accepted / skipped » dans les guides P10. Pas de menu MariaDB (#83) |
| Édition : Annuler, Rétablir, copier/couper/coller zone, rotation, miroirs, pipette, Dupliquer l’objet, modèles | `MainWindow.xaml` | partiel | Modèles : `docs/progress/editor-map-templates/STATUS.md`. Dialogue `MapTemplateListDialog` |
| Ressources : Données de jeu…, Événements communs…, image tuiles, feuille TileAsset, animer / retirer l’animation, Groupe d’autotile de la tuile… | `MainWindow.xaml` | partiel | Données de jeu : chantier Phase 6 + fiches plus récentes, pas un guide |
| Carte : Valider, Propriétés, Taille et décalage…, passer en TileAsset v6, Vérifier les transferts…, outils (raccourcis), warp, Actualiser le catalogue (F5), PNJ rapide, Coffre, Porte, Auberge, Événements carte, Contenu Phase 8, Événements communs, Actualiser marqueurs | `MainWindow.xaml` | partiel | « Vérifier les transferts… » ouvre encore la liste ; le panneau n’est plus ancré sous l’inspecteur |

## Dialogues et éditeurs (hors chrome carte)

| Control/Feature | code location | documenté ? | notes |
| --- | --- | --- | --- |
| Données de jeu : Tilesets, NPCs / monstres, Objets, Sorts / compétences, Classes, Héros, Compétences, Système, Boutiques, Ressources / spawns, Armes, Armures | `GameDataForm` | partiel | Boutons Nouveau, Dupliquer, Enregistrer brouillon, Publier, Supprimer, Importer…. Hors ligne : dépôts mémoire démo (`GameDataInitializationService`). Notes : `gamedata-heros`, `gamedata-systeme`, PR #114 pour Armes/Armures. Quickstart dit seulement « via les formulaires » |
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

| Document | Claim vs tip `8aec6e64` |
| --- | --- |
| `docs/EDITOR_WORKSPACE.md` | Wireframe Phase 3 : outils à gauche, titre de carte, tuiles + PropertyGrid à droite. **Faux pour le chrome.** Bandeau ajouté vers cet inventaire |
| `docs/ARCHITECTURE.md` | Coque WPF + îlots WinForms : encore vrai (ADR-0004). Pointe le workspace Phase 3 |
| `docs/progress/phase-10-beta-release/guides/CREATOR_QUICKSTART.md` | Parcours Fichier → peindre → enregistrer → publier. Ne dit pas la palette, le statut unique, ni le repli hors ligne. Lien ajouté vers cet inventaire |
| Wiki GitHub `Home`, `Auteur`, `Référence-Éditeur`, `Référence-Editor` | Miroir figé (tip docs cité `bf1677aa` / branche `cursor/phase10-beta-release`). `Auteur` recopie le quickstart. Pas de chrome #135–#138. Hors de ce PR (dépôt wiki séparé) |
| `docs/reference/editor/` | Quatre fiches Save/Publish, tip `ea116afa`. L’une décrit un dialogue supprimé |
| `docs/progress/phase-10-beta-release/KNOWN_ISSUES.md` | Affirmait le menu « Publier vers MariaDB… » encore visible. Retiré en #83 ; ligne corrigée ici |
| `docs/progress/editor-*/STATUS.md` et `COMMAND_CATALOG.md` | Notes de chantier, pas des guides. Plusieurs citent un format ou un shell d’avant #135 |
| `Frog.Editor/Docs/analyse_edition_map.md` | Analyse VB6. Pas l’UI C# actuelle |

## Écarts pour NET-12 (dans l’ordre)

1. Réécrire le wireframe de `docs/EDITOR_WORKSPACE.md` sur la disposition réelle : barre d’icônes, cartes à gauche, canevas, radios Tuiles | Objets | Entités | Régions, couches compactes, inspecteur repliable. Le bandeau de ce PR ne remplace pas cette page.
2. Réécrire `CREATOR_QUICKSTART.md` (et ensuite le wiki `Auteur`) : peindre depuis la radio Tuiles, objets/entités/régions, enregistrer seulement si PostgreSQL répond, avis `PostgreSQL injoignable — édition locale (non enregistrée)` sinon. Ne plus parler d’un publish MariaDB.
3. Décrire la ligne de statut unique et l’inspecteur (#137) : quand il s’ouvre (~160 px), quand il est masqué, couches toujours là (~168 px).
4. Décrire les gestes sélection (#136) à côté du copier-coller déjà noté dans `editor-selection-tools/STATUS.md` : glisser gauche déplace, glisser droit copie, double-clic pipette, régions immobiles, Maj = couche active.
5. Décrire le démarrage #138 : thread UI libre, `Task.Run` pour le catalogue, repli `InMemoryDemo`, commandes Enregistrer/Publier inactives en démo, libellés WPF qui restent « PostgreSQL ».
6. Aligner le wiki (`Home`, `Auteur`, `Référence*`) sur `docs/` au tip, pas sur `bf1677aa`. Le wiki se déclare miroir ; la source reste le dépôt.
7. Étendre le texte auteur des commandes d’événement au-delà de `COMMAND_CATALOG.md` (s’arrête à `call_common_event`) pour la palette `MapEventCommandPalette`, PRs #103 et #108–#133. Les fiches `docs/progress/*/STATUS.md` ne suffisent pas comme guide.
8. Couvrir dans les guides, pas seulement en STATUS, les surfaces éditeur #84–#133 encore absentes du quickstart : Héros, Compétences, Système, Armes, Armures, régions/rencontres, drapeaux de tuile, autotile, taille et décalage, audio carte, événements communs, duplication de prefab, copier-coller de zone (#118).
9. Retirer ou marquer historique `docs/reference/editor/publishmapdialogtryvalidate.md` (dialogue absent). Le reste de `docs/reference/editor/` est au tip `ea116afa` — plutôt NET-9 si la reprise des fiches API est séparée.
10. Captures : NET-11, après le texte. Ne pas inventer de PNG dans le rattrapage.
