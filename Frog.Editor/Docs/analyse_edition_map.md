## Analyse Fonctionnelle : Édition de carte (Map Editor)

---

### 1. Nom de la fonctionnalité
**Édition de carte (Map Editor)**

### 2. Description fonctionnelle (comportement VB6)
L'éditeur de carte permet de créer, modifier, sauvegarder et tester des cartes ("maps") du jeu. L'utilisateur peut placer des tuiles sur différentes couches, configurer les attributs des cases (warp, blocage, etc.), visualiser la carte, et sauvegarder dans un format binaire spécifique.

Interactions principales :
- Sélection de tileset
- Placement manuel de tuiles
- Gomme, pinceau, remplissage
- Navigation dans la carte
- Sauvegarde et chargement

### 3. Fichiers et modules VB6 impliqués
- `frmEditor_Map.frm` : Interface principale d'édition de carte
- `modMapEditor.bas` : Logique d'édition (placement de tuiles, calques...)
- `modMap.bas` : Définition des structures de carte
- `modFileIO.bas` : Sauvegarde et chargement
- `modDirectDraw.bas` : Rendu graphique
- `modTypes.bas` : Types de données partagés
- `modGlobals.bas` : Variables d'état global (curseur, couche active...)

### 4. Données manipulées
- Structures :
  - `MapData` (nom, musique, moralité, largeur/hauteur...)
  - `Tile(Layer)` : ID de tuile, tileset associé, attributs
  - Attributs par case : `TileType`, `Data1`, `Data2`, `Data3`
- Variables globales :
  - `EditorMap`, `CurX`, `CurY`, `EditorTileX`, `EditorTileY`
- Fichiers : `.map` binaires avec en-tête + données compactées

### 5. Dépendances critiques
- Affichage via DirectDraw (rendu des tuiles)
- Système de couches : Ground, Mask, Fringe, Animation, etc.
- Fichiers `tileset.bmp` ou `.png`
- Lecture/écriture binaire pour persistance

### 6. Problèmes à surveiller (VB6 vs C#)
- Rendu DirectDraw obsolète → remplacement par GDI+ ou SkiaSharp
- Couplage étroit entre UI et logique (tout dans le `frmEditor_Map`)
- Gestion de la mémoire manuelle (arrays dynamiques VB6)
- Boucle de rendu bloquante (DoEvents)

### 7. Plan de conversion vers C#
**Structure cible dans `Frog.Editor` :**
- `Forms/MapEditorForm.cs` : interface graphique WinForms
- `Services/MapEditorService.cs` : logique d'édition
- `Models/Map.cs`, `Tile.cs`, `Layer.cs` : structures de données
- `IO/MapSerializer.cs` : lecture/écriture fichiers `.map`
- `Interop/DirectDrawLegacy.cs` ou remplacement

**Étapes :**
1. Découper la logique VB6 en responsabilités C# claires
2. Créer les modèles de données en C#
3. Implémenter le rendu de base (avec GDI+ temporairement)
4. Migrer la logique d'édition
5. Gérer l'interface WinForms (outils, sélection...)
6. Intégrer la sauvegarde et le chargement
7. Ajouter les tests visuels

### 8. Tests à prévoir
- Chargement d’une carte existante
- Placement de tuiles sur différentes couches
- Affichage correct selon couche active
- Sauvegarde et relecture identique d’une carte
- Interactions utilisateur (curseur, scroll, sélection...)
- Performance de rendu sur carte complète

**Statut :** à migrer en priorité  
**Lien projet VB6 :** `frmEditor_Map.frm`, `modMapEditor.bas`, `modMap.bas`  
**Fonction critique :** oui

---

## Analyse Fonctionnelle : Système de couches (Layering)

---

### 1. Nom de la fonctionnalité
**Système de couches (Layering)**

### 2. Description fonctionnelle (comportement VB6)
Le moteur de carte utilise plusieurs couches pour gérer l'affichage et l'interaction avec les tuiles :
- Ground (sol)
- Mask (éléments à moitié transparents)
- Fringe (éléments au-dessus du joueur)
- Animation (tuiles dynamiques)

Chaque case de la carte contient plusieurs tuiles empilées, correspondant à ces couches. L'utilisateur peut sélectionner la couche active et y placer ou modifier les tuiles.

### 3. Fichiers et modules VB6 impliqués
- `modMapEditor.bas` : gestion de la couche active
- `modMap.bas` : structure des couches dans `Map.Tile()`
- `modDirectDraw.bas` : rendu par couche, ordre d'affichage
- `frmEditor_Map.frm` : UI pour changer de couche

### 4. Données manipulées
- `Map.Tile(x, y).Layer(layerIndex).TileSet`, `.TileNum`
- `EditorLayer` (variable globale pour couche active)
- `MAX_LAYERS` (constante définissant le nombre de couches)

### 5. Dépendances critiques
- Rendu graphique ordonné (layer back → front)
- Interaction avec les outils d'édition (placement tuiles, gomme...)
- Sauvegarde de toutes les couches dans les fichiers `.map`

### 6. Problèmes à surveiller (VB6 vs C#)
- Ordre de rendu doit être respecté (rendu correct + perfs)
- UI doit permettre de naviguer entre les couches facilement
- La gestion des couches est manuelle dans VB6, à encapsuler proprement en C#

### 7. Plan de conversion vers C#
**Structure cible dans `Frog.Editor` :**
- `Models/Layer.cs` : structure d'une couche
- `Map.Tile[x, y].Layers[]` : tableau de couches par case
- `MapEditorService` : gestion de la couche active, outils, etc.
- `MapRenderer` (ou service rendu) : gestion de l'ordre des couches
- `Forms/LayerSelectorControl.cs` : UI pour changer de couche

**Étapes :**
1. Créer l'enum `LayerType` en C#
2. Intégrer `Layer[]` dans le modèle `Tile`
3. Adapter le rendu selon l'ordre de couches défini
4. Ajouter l'outil de sélection de couche dans l'UI
5. Relier les outils d'édition à la couche active
6. Vérifier l'enregistrement correct des couches dans `.map`

### 8. Tests à prévoir
- Affichage d'une carte multi-couches
- Placement de tuiles dans chaque couche
- Commutation correcte entre couches
- Sauvegarde/rechargement avec intégrité des couches
- Compatibilité avec l’animation des couches spéciales (le cas échéant)

**Statut :** critique et couplée à l'éditeur de carte  
**Lien projet VB6 :** `modMapEditor.bas`, `modMap.bas`, `modDirectDraw.bas`  
**Fonction critique :** oui

---

## Analyse Fonctionnelle : Tilesets & sélection de tuiles

---

### 1. Nom de la fonctionnalité
**Tilesets & sélection de tuiles**

### 2. Description fonctionnelle (comportement VB6)
L'utilisateur peut sélectionner un tileset graphique, parcourir visuellement ses tuiles, et cliquer sur l’une d’elles pour l’assigner comme tuile active pour l’édition. Ce composant visuel sert de palette graphique.

Comportement :
- Chargement du tileset principal au démarrage
- Affichage dans une `PictureBox`
- Sélection d’une tuile par clic souris
- Indicateur visuel sur la tuile sélectionnée

### 3. Fichiers et modules VB6 impliqués
- `frmEditor_Map.frm` : contient le contrôle d’affichage des tilesets
- `modMapEditor.bas` : met à jour la sélection de tuile
- `modGraphics.bas` : chargement et gestion des tilesets
- `modGlobals.bas` : `EditorTileX`, `EditorTileY`, `EditorTileSelStart`...

### 4. Données manipulées
- Image du tileset (`DD_TileSurf`)
- Coordonnées de sélection de tuile
- ID de la tuile sélectionnée
- Chemin du fichier tileset (configuré dans fichier de config ?)

### 5. Dépendances critiques
- Chargement correct du fichier tileset `.bmp` ou `.png`
- Résolution correcte des tuiles (ex: 32x32)
- Support graphique du moteur (GDI+/DirectDraw)

### 6. Problèmes à surveiller (VB6 vs C#)
- PictureBox en VB6 avec rendu DirectDraw → à remplacer
- Système de sélection (coordonnées x/y calculées à la main)
- Image tileset codée en dur dans VB6 → à configurer dynamiquement
- Pas de support natif du zoom ou survol

### 7. Plan de conversion vers C#
**Structure cible dans `Frog.Editor` :**
- `Controls/TilesetSelector.cs` : contrôle custom WinForms (UserControl)
- `Models/Tileset.cs` : image source + propriétés
- `Services/TileSelectionService.cs` : sélection courante
- `Assets/Tilesets/` : emplacement des fichiers image

**Étapes :**
1. Créer une classe `Tileset` pour charger une image
2. Créer un contrôle custom pour afficher le tileset (grid + souris)
3. Afficher un rectangle de sélection sur la tuile active
4. Mettre à jour les variables de sélection dans le service
5. Connecter le contrôle à la fenêtre `MapEditorForm`
6. Permettre le changement dynamique de tileset à l’avenir

### 8. Tests à prévoir
- Chargement d’un fichier tileset
- Sélection correcte d’une tuile
- Affichage du curseur de sélection
- Intégration avec le pinceau (utilise la bonne tuile)
- Compatibilité avec plusieurs résolutions de tiles

**Statut :** important pour l’édition, dépendant du rendu  
**Lien projet VB6 :** `frmEditor_Map.frm`, `modMapEditor.bas`, `modGraphics.bas`  
**Fonction critique :** oui

---

## Analyse Fonctionnelle : Attributs spéciaux (warp, blocage, animation...)

### 1. Nom de la fonctionnalité
**Attributs spéciaux de cases (tiles)**

### 2. Description fonctionnelle (comportement VB6)
Chaque case d’une carte peut avoir un attribut spécial (e.g. bloqué, téléporteur, ressource, animation, zone de spawn). Ces attributs influencent le gameplay et la navigation.

L’utilisateur peut :
- Choisir un attribut dans une liste déroulante
- Cliquer sur une case pour lui appliquer l’attribut
- Voir visuellement l’attribut sur la carte (coloration ou symbole)

### 3. Fichiers et modules VB6 impliqués
- `frmEditor_Map.frm` : interface de sélection et d’application d’attributs
- `modMapEditor.bas` : logique de placement d’attributs
- `modMap.bas` : structure des attributs de case
- `modTypes.bas` : énumération des attributs (`TileTypeEnum`)

### 4. Données manipulées
- `Map.Tile(x, y).Type`, `.Data1`, `.Data2`, `.Data3`
- Enum `TileTypeEnum` : Blocked, Warp, Item, NPCSpawn, etc.

### 5. Dépendances critiques
- Doit apparaître sur la carte de façon visible (couleur, symbole)
- Sauvegarde/chargement fiable dans les fichiers `.map`
- Interaction avec d'autres systèmes (warps, spawn, zones de combat)

### 6. Problèmes à surveiller (VB6 vs C#)
- Système visuel d’attributs dépendant de DirectDraw
- Couplage fort entre interface et logique
- Données étendues via `Data1/2/3` parfois mal typées

### 7. Plan de conversion vers C#
- `Tile.Type` → Enum propre + structure `TileAttribute`
- `Services/TileAttributeService.cs` : logique de placement
- `Controls/AttributePalette.cs` : sélection visuelle
- Couleurs et icônes de rendu gérés via un `OverlayRenderer`

**Étapes :**
1. Recréer `TileTypeEnum` en C#
2. Créer des classes d’attributs avec description + metadata
3. Ajouter la gestion UI pour sélectionner et appliquer un attribut
4. Adapter le rendu pour montrer les attributs sur la carte
5. Supporter les attributs dans la sauvegarde `.map`

### 8. Tests à prévoir
- Application d’attributs sur la carte
- Sauvegarde/rechargement intègre
- Compatibilité des attributs avec le client/serveur
- Visibilité des attributs (overlay)

**Statut :** important pour la navigation et les mécaniques de jeu
**Lien projet VB6 :** `modMapEditor.bas`, `modMap.bas`, `frmEditor_Map.frm`  
**Fonction critique :** oui

---

## Analyse Fonctionnelle : Sauvegarde / Chargement de carte

### 1. Nom de la fonctionnalité
**Sauvegarde et chargement des cartes (.map)**

### 2. Description fonctionnelle (comportement VB6)
Le système permet de sauvegarder une carte éditée dans un fichier `.map` binaire, et de recharger ce fichier plus tard. Il s’agit d’un format structuré à base de `Type` compressé ou à taille fixe.

Fonctionnalités :
- Enregistrement manuel ou automatique
- Lecture au démarrage
- Vérification de validité du fichier

### 3. Fichiers et modules VB6 impliqués
- `modFileIO.bas` : lecture/écriture
- `modMap.bas` : structure des données de carte
- `modTypes.bas` : définitions

### 4. Données manipulées
- Fichier `.map`
- Structures `MapData`, `Tile()`, `Tile.Layer()`, `Tile.Type`

### 5. Dépendances critiques
- Format binaire strict : ordre et taille des champs critiques
- Nécessité de compatibilité avec client/serveur (interopérabilité)

### 6. Problèmes à surveiller (VB6 vs C#)
- Utilisation de `Put`, `Get` → à migrer vers `BinaryReader`/`Writer`
- Taille fixe des structures → à respecter dans le C#
- Peu ou pas de validation d’intégrité dans le code VB6

### 7. Plan de conversion vers C#
- `IO/MapSerializer.cs` : centralise la lecture/écriture
- `Models/Map`, `Tile`, `Layer`, etc. : utilisés comme base
- Utilisation de `Span<byte>` ou buffers pour plus de performance

**Étapes :**
1. Documenter précisément le format `.map` (ordre, tailles)
2. Créer une fonction `SaveMap(Map map, string path)`
3. Créer une fonction `LoadMap(string path): Map`
4. Ajouter vérifications de version ou de validité
5. Intégrer dans le service d’édition

### 8. Tests à prévoir
- Chargement de fichiers VB6 existants
- Sauvegarde d’une nouvelle carte et relecture
- Comparaison binaire entre fichier VB6 et C# (interop)
- Gestion des erreurs (fichier invalide ou manquant)

**Statut :** critique pour toute l’application
**Lien projet VB6 :** `modFileIO.bas`, `modMap.bas`, `modTypes.bas`
**Fonction critique :** oui

---

## Analyse Fonctionnelle : PNJ (NPC) / Entités dynamiques

### 1. Nom de la fonctionnalité
**Éditeur de PNJ (NPC)**

### 2. Description fonctionnelle (comportement VB6)
Permet de créer des entités non-joueurs avec leurs propriétés :
- Apparence (sprite ID)
- Dialogue ou texte
- Stats de combat
- Déplacement (statique ou aléatoire)

L’éditeur de PNJ assigne aussi les PNJ aux cartes via l’éditeur de map.

### 3. Fichiers et modules VB6 impliqués
- `frmEditor_NPC.frm`
- `modNpcEditor.bas`
- `modMap.bas` (spawn de PNJ sur la carte)
- `modTypes.bas` (structure `NPCRec`, `MapNPC`)

### 4. Données manipulées
- Liste de PNJ (indexés, max défini)
- Structure `NPCRec`
- Affectation sur carte : `Map.MapNPC(x).Num`, `Dir`, etc.

### 5. Dépendances critiques
- Fichier binaire `.npc` ou intégré dans `.dat`
- Synchronisation avec serveur/client pour les apparitions

### 6. Problèmes à surveiller (VB6 vs C#)
- Interface très liée aux structures internes
- Faible modularité du système de spawn
- Pas de vérification de validité (sprite ID invalide, etc.)

### 7. Plan de conversion vers C#
- `Forms/NpcEditorForm.cs` : interface d’édition
- `Models/Npc.cs`, `NpcSpawn.cs`
- `Services/NpcEditorService.cs`
- `IO/NpcSerializer.cs` : gestion fichiers

**Étapes :**
1. Extraire toutes les propriétés d’un PNJ dans une classe dédiée
2. Créer une interface de liste + fiche détail PNJ
3. Gérer la sauvegarde/chargement des PNJ
4. Relier les PNJ aux spawns de carte (coordonnées, direction)
5. Synchroniser avec le serveur

### 8. Tests à prévoir
- Création/modification d’un PNJ
- Affectation à une carte
- Sauvegarde et chargement
- Affichage correct dans la carte avec sprite

**Statut :** important pour le gameplay et les quêtes
**Lien projet VB6 :** `frmEditor_NPC.frm`, `modNpcEditor.bas`, `modMap.bas`  
**Fonction critique :** oui

---

---

## Analyse Fonctionnelle : Objets & Ressources

### 1. Nom de la fonctionnalité
**Éditeur d’objets (Items) et de ressources**

### 2. Description fonctionnelle (comportement VB6)
Permet de créer et configurer les objets et ressources disponibles dans le jeu :
- Objets utilisables, consommables, équipements
- Ressources à collecter (arbres, minerais, etc.)

L’utilisateur peut :
- Définir les propriétés de chaque objet (type, stats, sprite, description)
- Définir les propriétés de récolte (outil requis, temps, etc.)

### 3. Fichiers et modules VB6 impliqués
- `frmEditor_Item.frm`
- `modItemEditor.bas`
- `modTypes.bas` (type `ItemRec`, `ResourceRec`)

### 4. Données manipulées
- Tableaux : `Item()`, `Resource()`
- Enums : `ItemType`, `ResourceType`
- Fichiers binaires `.itm`, `.res`

### 5. Dépendances critiques
- Doit être synchronisé avec le client et serveur (jeu/loot)
- Rendu correct des sprites dans l’éditeur

### 6. Problèmes à surveiller (VB6 vs C#)
- Code très couplé à l’interface
- Énumérations codées en dur avec mauvais noms
- Interface de liste peu claire à moderniser

### 7. Plan de conversion vers C#
- `Forms/ItemEditorForm.cs`, `ResourceEditorForm.cs`
- `Models/Item.cs`, `Resource.cs`
- `Services/ItemEditorService.cs`
- `IO/ItemSerializer.cs`, `ResourceSerializer.cs`

**Étapes :**
1. Reprendre toutes les propriétés des objets
2. Refactoriser les types et catégories
3. Créer une UI WinForms moderne (liste + fiche)
4. Supporter la sauvegarde/chargement
5. Lier aux zones de récolte dans les maps

### 8. Tests à prévoir
- Création, modification, suppression
- Sauvegarde et relecture
- Compatibilité client/serveur

**Statut :** important, dépend des mécaniques de jeu
**Lien projet VB6 :** `frmEditor_Item.frm`, `modItemEditor.bas`, `modTypes.bas`
**Fonction critique :** oui

---

## Analyse Fonctionnelle : Warps / Transitions de maps

### 1. Nom de la fonctionnalité
**Système de Warps (téléporteurs entre maps)**

### 2. Description fonctionnelle (comportement VB6)
Permet de définir des points de transition d’une carte vers une autre :
- Via les attributs `TileType = Warp`
- Configuration de la cible (map ID, X, Y)

Utilisé par les joueurs pour naviguer dans le monde du jeu.

### 3. Fichiers et modules VB6 impliqués
- `modMapEditor.bas` : placement de warp
- `modMap.bas` : définition dans `Tile.Type` et `Data1/2/3`
- `modHandleData.bas` : traitement lors des déplacements joueur

### 4. Données manipulées
- `Tile.Type = Warp`
- `Data1 = Map`, `Data2 = X`, `Data3 = Y`

### 5. Dépendances critiques
- Fonctionne avec système de collision, mouvement joueur
- Validation serveur sur position cible

### 6. Problèmes à surveiller (VB6 vs C#)
- Données mal typées (pas d’objet Warp dédié)
- Mauvais feedback visuel sur la carte (pas d’indication de warp)

### 7. Plan de conversion vers C#
- Créer une classe `WarpAttribute : TileAttribute`
- Interface pour configurer les warps
- Validation visuelle (symbole de warp sur carte)
- Côté serveur : sécuriser la transition

**Étapes :**
1. Ajouter support `Warp` dans `TileAttribute`
2. UI pour configurer MapID/X/Y
3. Affichage graphique de l’attribut
4. Mise à jour de la logique serveur pour déclenchement

### 8. Tests à prévoir
- Placement sur carte
- Sauvegarde et chargement
- Déclenchement en jeu
- Position exacte du joueur après warp

**Statut :** critique pour la navigation
**Lien projet VB6 :** `modMapEditor.bas`, `modMap.bas`, `modHandleData.bas`
**Fonction critique :** oui

---

## Analyse Fonctionnelle : Gestion visuelle (rendu graphique)

### 1. Nom de la fonctionnalité
**Rendu graphique (éditeur + client)**

### 2. Description fonctionnelle (comportement VB6)
Le rendu utilise DirectDraw (2D) pour dessiner la carte, les couches, les sprites, le texte. Il est utilisé dans :
- l’éditeur de map
- le client du jeu

Fonctions clés :
- Double buffering
- Chargement et affichage des surfaces graphiques
- Dessin d’éléments dynamiques (curseur, overlay)

### 3. Fichiers et modules VB6 impliqués
- `modDirectDraw.bas`
- `modGraphics.bas`
- `modMapEditor.bas`

### 4. Données manipulées
- `DDS_BackBuffer`, `DDS_TileSurf`, `DDS_Map`...
- Coordonnées écran / carte

### 5. Dépendances critiques
- DirectDraw est obsolète, non supporté sous .NET
- Couplage fort avec `DoEvents` et timers VB6

### 6. Problèmes à surveiller (VB6 vs C#)
- Pas de portage direct de DirectDraw
- Problèmes de performance si GDI+ est mal utilisé
- Pas de séparation entre rendu, logique, données

### 7. Plan de conversion vers C#
- Créer un service de rendu `IRenderer`
- Utiliser GDI+ (temporaire) ou SkiaSharp/MonoGame plus tard
- Découpler : données → logique → rendu

**Étapes :**
1. Créer une interface `IRenderer`
2. Implémenter `MapRenderer`, `OverlayRenderer`, etc.
3. Charger les assets dans une classe dédiée
4. Utiliser `Bitmap`/`Graphics` dans WinForms comme MVP
5. Prévoir future migration vers moteur + performant

### 8. Tests à prévoir
- Affichage fluide des couches
- Affichage des overlays (curseur, attributs...)
- Performance sur carte complète
- Pas de fuite mémoire lors du rendu

**Statut :** critique pour toute la partie visuelle
**Lien projet VB6 :** `modDirectDraw.bas`, `modGraphics.bas`, `modMapEditor.bas`
**Fonction critique :** oui

---

---

## Analyse Fonctionnelle : Interface utilisateur générale

### 1. Nom de la fonctionnalité
**Interface utilisateur de l’éditeur**

### 2. Description fonctionnelle (comportement VB6)
L’éditeur regroupe plusieurs fenêtres permettant de gérer les différentes entités du jeu (maps, objets, NPC, ressources...). Chaque fenêtre est accessible via un menu principal ou des raccourcis clavier.

Fonctionnalités :
- Navigation entre les éditeurs (carte, NPC, objets...)
- Affichage d’informations sur l’état courant (carte en cours, tuile sélectionnée...)
- Menus, boutons, raccourcis

### 3. Fichiers et modules VB6 impliqués
- `frmEditor.frm` (form principale de l’éditeur)
- Tous les `frmEditor_*`
- `modGeneral.bas`, `modGlobals.bas`

### 4. Données manipulées
- Fenêtres enfants, status bar, menus
- Variables globales (ID sélectionnés, modes...)

### 5. Dépendances critiques
- Organisation cohérente des modules
- Navigation fluide entre les éditeurs
- États de l’interface partagés entre modules (souvent en global)

### 6. Problèmes à surveiller (VB6 vs C#)
- Fenêtres multiples modales / non-modales
- Utilisation abusive de variables globales
- Formulaires trop chargés, peu maintenables

### 7. Plan de conversion vers C#
- `Forms/MainEditorForm.cs`
- Chaque éditeur devient un `UserControl` intégré dynamiquement
- `EditorContext` : service global de navigation et d’état

**Étapes :**
1. Créer `MainEditorForm` avec panneau central dynamique
2. Isoler chaque éditeur en `UserControl`
3. Gérer la navigation via une barre de menu ou une side bar
4. Remplacer les variables globales par un `EditorState` injecté

### 8. Tests à prévoir
- Navigation entre les éditeurs
- Affichage correct des états sélectionnés
- Fermeture/rechargement d’un éditeur

**Statut :** essentiel pour l’ergonomie du logiciel
**Lien projet VB6 :** `frmEditor.frm`, `frmEditor_*`, `modGlobals.bas`
**Fonction critique :** oui

---

## Analyse Fonctionnelle : Chargement de tilesets / images

### 1. Nom de la fonctionnalité
**Chargement des tilesets et images graphiques**

### 2. Description fonctionnelle (comportement VB6)
L’éditeur charge des fichiers image pour les tilesets, animations, ressources, sprites NPC, etc. Ces fichiers sont stockés localement dans des dossiers définis par configuration ou en dur dans le code.

### 3. Fichiers et modules VB6 impliqués
- `modGraphics.bas`
- `modDirectDraw.bas`
- `modGlobals.bas`

### 4. Données manipulées
- `DDS_TileSurf`, `DDS_ResourceSurf`, `DDS_AnimationSurf`, etc.
- Chemins vers les fichiers `.bmp` / `.png`

### 5. Dépendances critiques
- Assets obligatoires pour l’éditeur (sans tileset, l’éditeur est inutilisable)
- Chargement synchronisé avec le rendu graphique

### 6. Problèmes à surveiller (VB6 vs C#)
- Chemins codés en dur → à remplacer par config
- DirectDraw utilise des surfaces propriétaires
- Aucun fallback en cas de fichier manquant

### 7. Plan de conversion vers C#
- `Assets/AssetLoader.cs` : chargement centralisé
- Dossier `Assets/Tilesets/`, `Assets/NPC/`, etc.
- Utilisation de `Bitmap.FromFile()` (ou GDI+/SkiaSharp)

**Étapes :**
1. Définir une structure de répertoires logique
2. Créer une classe pour référencer et charger les assets
3. Ajouter messages d’erreur en cas d’asset manquant
4. Charger dynamiquement les images au démarrage de l’éditeur

### 8. Tests à prévoir
- Chargement correct de tous les types d’images
- Affichage dans l’éditeur
- Tolérance aux fichiers manquants

**Statut :** essentiel pour toute visualisation
**Lien projet VB6 :** `modGraphics.bas`, `modGlobals.bas`
**Fonction critique :** oui

---

## Analyse Fonctionnelle : Interopérabilité client/éditeur/serveur

### 1. Nom de la fonctionnalité
**Interopérabilité des structures entre client, éditeur et serveur**

### 2. Description fonctionnelle (comportement VB6)
Le client, l’éditeur et le serveur partagent les mêmes structures de données (types `Map`, `Item`, `NPC`, etc.) via des modules communs. Cela permet une compatibilité des fichiers et de la logique.

### 3. Fichiers et modules VB6 impliqués
- `modTypes.bas`
- `modEnumerations.bas`
- `modConstants.bas`

### 4. Données manipulées
- Tous les `Type`, `Enum`, `Const` globaux utilisés dans le projet
- Utilisés dans : map, item, npc, warp, etc.

### 5. Dépendances critiques
- Tout changement dans les types doit être répercuté dans les trois couches
- Sauvegardes et échanges réseau dépendent de ces structures

### 6. Problèmes à surveiller (VB6 vs C#)
- Code dupliqué sans mécanisme de synchronisation
- Pas de versionnage ou de gestion d’évolution

### 7. Plan de conversion vers C#
- Créer projet partagé `Frog.Shared`
- Y placer `Models/`, `Enums/`, `Constants.cs`
- Référence par `Frog.Editor`, `Frog.Client`, `Frog.Server`

**Étapes :**
1. Lister tous les types utilisés dans chaque couche
2. Créer des modèles C# bien typés
3. Éviter les types génériques (object, variant...)
4. Ajouter version ou `DataContract` au besoin

### 8. Tests à prévoir
- Validation croisée entre les trois projets
- Compilation sans conflit de type
- Chargement de données identiques depuis chaque module

**Statut :** fondation essentielle
**Lien projet VB6 :** `modTypes.bas`, `modEnumerations.bas`, `modConstants.bas`
**Fonction critique :** oui

---

## Analyse Fonctionnelle : Tests dans le client

### 1. Nom de la fonctionnalité
**Tests dans le client (intégration avec éditeur)**

### 2. Description fonctionnelle (comportement VB6)
Permet de lancer le client depuis l’éditeur pour tester une carte modifiée sans devoir compiler ou transférer manuellement les fichiers. Dans certains cas, le client lit les fichiers directement depuis le disque local (shared folder).

### 3. Fichiers et modules VB6 impliqués
- `frmEditor_Map.frm` ou `frmEditor.frm`
- `modGeneral.bas`
- `modConstants.bas`

### 4. Données manipulées
- Chemin de la carte sauvegardée
- Arguments de lancement du client
- Répertoire partagé de fichiers

### 5. Dépendances critiques
- Fichier `.map` à jour et accessible
- Exécutable client compatible avec les données

### 6. Problèmes à surveiller (VB6 vs C#)
- Lancement par `Shell` → à remplacer par `Process.Start`
- Risques de conflit si la carte n’est pas sauvegardée
- Aucune isolation (le client lit les fichiers "live")

### 7. Plan de conversion vers C#
- Bouton "Tester la carte"
- Vérification de la sauvegarde
- Dossier de test isolé (`/Temp/TestLaunch/`)
- Lancement via `System.Diagnostics.Process`

**Étapes :**
1. Vérifier que la carte en cours est sauvegardée
2. Copier les fichiers nécessaires dans un dossier temporaire
3. Lancer le client avec des arguments de test (ex: `--map=1.map`)
4. (Plus tard) permettre de debugger le client avec des logs

### 8. Tests à prévoir
- Lancement du client depuis l’éditeur
- Chargement de la bonne carte
- Fermeture propre sans affecter les données principales

**Statut :** utile pour tester rapidement sans exporter manuellement
**Lien projet VB6 :** `frmEditor.frm`, `modGeneral.bas`, `modConstants.bas`
**Fonction critique :** non, mais très pratique pour le dev