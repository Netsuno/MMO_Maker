# LEGACY_FORMATS.md — référence historique seulement

> **ADR-0003 :** ce document décrit une expérimentation FRoG `.fcc`.  
> Il **n’est pas** une exigence de livraison du MMO Maker. Aucun importeur ni parité binaire n’est requis.

---


Spécifications **vérifiées depuis le code source** `Alexoune001/FRoG-Creator-OSE-V0.6.3` et **contrôlées sur fixtures** `.fcc` du même dépôt.  
Les points encore non prouvés au niveau octet sont marqués **INCONNU / À PROUVER**.

---

## 1. Carte — fichier `map{N}.fcc`

### 1.1 Fichiers et routines

| Rôle | Fichier | Membres |
| --- | --- | --- |
| Types | `*/Modules Sources/modTypes.bas` | `TileRec`, `MapRec`, `NpcMapRec` / `NpcMap`, `TILE_TYPE_*`, `MAP_MORAL_*`, `DIR_*`, `PIC_X`/`PIC_Y` |
| I/O éditeur | `Editeur/Modules Sources/modDatabase.bas` | `SaveLocalMap`, `LoadMap` |
| I/O serveur | `Serveur/Modules Sources/modDatabase.bas` | `SaveMap`, `LoadMap`, `LoadMaps` |
| Dimensions | `Serveur/Modules Sources/modGeneral.bas`, `Editeur/.../modGameLogic.bas` | `MAX_MAPX` / `MAX_MAPY` ; `ReDim Map(i).Tile(0 To MAX_MAPX, 0 To MAX_MAPY)` |

Chemin : `{App.Path}\maps\map{MapNum}.fcc` (casse `maps` / `Maps` selon composant).  
**Pas** d’extension `.map` dans ce code OSE 0.6.3.

Persistance : `Open … For Binary` puis **`Put #f, , Map(MapNum)`** / **`Get #f, , Map(MapNum)`** sur l’UDT entier (pas d’écriture champ-à-champ).

### 1.2 Constantes explicites (extraites, non renumérotées)

Tuile **32×32** : `PIC_X = 32`, `PIC_Y = 32`.

| Symbole | Valeur |
| --- | ---: |
| `MAP_MORAL_NONE` | 0 |
| `MAP_MORAL_SAFE` | 1 |
| `MAP_MORAL_NO_PENALTY` | 2 |
| `DIR_DOWN` | 0 |
| `DIR_LEFT` | 1 |
| `DIR_RIGHT` | 2 |
| `DIR_UP` | 3 |
| `MAX_MAP_NPCS` | 15 |

#### `TILE_TYPE_*` (Byte)

| Nom | Valeur | Data1 / Data2 / Data3 (observé dans l’éditeur) |
| --- | ---: | --- |
| `WALKABLE` | 0 | — |
| `BLOCKED` | 1 | — |
| `WARP` | 2 | map / X / Y (`EditorWarpMap/X/Y`) |
| `ITEM` | 3 | item num / value / (souvent 0) |
| `NPCAVOID` | 4 | — |
| `KEY` | 5 | item num / take flag |
| `KEYOPEN` | 6 | X / Y ; message dans `String1` |
| `HEAL` | 7 | — |
| `KILL` | 8 | — |
| `SHOP` | 9 | shop id… |
| `CBLOCK` | 10 | — |
| `ARENA` | 11 | — |
| `SOUND` | 12 | — |
| `SPRITE_CHANGE` | 13 | — |
| `SIGN` | 14 | — |
| `DOOR` | 15 | — |
| `NOTICE` | 16 | — |
| *(CHEST commenté)* | 17 | retiré |
| `CLASS_CHANGE` | 18 | — |
| `SCRIPTED` | 19 | — |
| `NPC_SPAWN` | 20 | — |
| `BANK` | 21 | — |
| `COFFRE` | 22 | — |
| `PORTE_CODE` | 23 | — |
| `BLOCK_MONTURE` | 24 | — |
| `BLOCK_NIVEAUX` | 25 | — |
| `TOIT` | 26 | — |
| `BLOCK_GUILDE` | 27 | — |
| `BLOCK_TOIT` | 28 | — |
| `BLOCK_DIR` | 29 | — |
| `CRAFT` | 30 | — |
| `METIER` | 31 | — |

Les attributs non listés dans la colonne Data restent **à caractériser** avant mapping domaine (ne pas inventer).

### 1.3 `TileRec` (ordre des champs source)

```text
Ground, Mask, Anim, Mask2, M2Anim, Mask3, M3Anim As Long     ' couches sol
Fringe, FAnim, Fringe2, F2Anim, Fringe3, F3Anim As Long     ' couches fringe
Type As Byte
Data1, Data2, Data3 As Long
String1, String2, String3 As String   ' longueur variable
Light As Long
GroundSet, MaskSet, AnimSet, Mask2Set, M2AnimSet, Mask3Set, M3AnimSet As Byte
FringeSet, FAnimSet, Fringe2Set, F2AnimSet, Fringe3Set, F3AnimSet As Byte
```

Couches graphiques (ordre de dessin historique typique) : Ground → Mask/Anim → Mask2/M2Anim → Mask3/M3Anim → Fringe… ; chaque `*Set` sélectionne le tileset.

### 1.4 `MapRec` (ordre des champs source)

```text
name As String * 40          ' fixe, souvent paddé espaces
Revision As Long             ' Int32 LE
Moral As Byte
Up, Down, Left, Right As Long
Music As String              ' variable (longueur Int32 LE + octets ANSI)
BootMap As Long
BootX As Byte
BootY As Byte
Shop As Long
Indoors As Byte
tile() As TileRec            ' dynamique 2D
Npc(1 To 15) As Long
Npcs(1 To 15) As NpcMapRec   ' éditeur ; serveur: NpcMap (mêmes champs)
PanoInf As String * 50
TranInf As Byte
PanoSup As String * 50
TranSup As Byte
Fog As Integer               ' Int16
FogAlpha As Byte
guildSoloView, petView, traversable, meteo, frequenceMeteo As Byte
```

`ReDim … (0 To MAX_MAPX, 0 To MAX_MAPY)` : bornes inclusives. Défaut serveur fréquent : **30×30** (31×31 cellules) si scrolling activé ; sinon 19×14.

### 1.5 Encodage binaire observé (fixtures)

Fixtures : `fixtures/legacy/maps/map{1,2,3}.fcc` (copie depuis `Editeur/Maps`, taille **85190** octets chacune).

En-tête `map1.fcc` (« Carte de test ») :

| Offset | Contenu mesuré |
| ---: | --- |
| 0–39 | `String*40` nom |
| 40–43 | `Revision` = 22 |
| 44 | `Moral` = 0 |
| 45–60 | `Up,Down,Left,Right` = 0,2,0,0 |
| 61–64 | longueur `Music` = 0 |
| 65–75 | `BootMap/X/Y`, `Shop`, `Indoors` (Pack Byte sans padding détecté avant le tableau) |
| 76–91 | descripteur tableau 2D VB : `(cElements=31, lLbound=0)` × 2 |
| 92… | 961 enregistrements tuile de **88 octets** chacun (taille déduite ; détail String/Set **À PROUVER**) |

Warp observé (type=2 à l’octet 52 du record 88 o) : `Data1=map`, `Data2=x`, `Data3=y` — cohérent avec `modGameLogic` éditeur.

**INCONNU / À PROUVER avant reader final :**

1. Composition exacte des 88 octets (alignement après `Type`, forme des `String*`, présence de tous les `*Set`).
2. Ordre de stockage des dimensions du tableau (premier index = X confirmé sur échantillons d’attributs, à figer par golden master).
3. Encodage `Boolean` dans `NpcMapRec` (2 octets VB) et taille exacte du trailer post-tuiles.
4. Comportement si `Music` ou `String1..3` non vides (longueur + page de codes Windows-1252 probable).
5. Fichiers produits avec `MAX_MAPX/Y` ≠ 30.

**Politique d’erreur :** tout champ non décodé avec certitude doit apparaître dans le rapport d’import ; interdiction de valeur par défaut silencieuse.

### 1.6 Exemple hexadécimal court (`map1.fcc`)

```text
0000: 43 61 72 74 65 20 64 65 20 74 65 73 74 20 ...  ' "Carte de test" + espaces
0028: 16 00 00 00                                      ' Revision 22
002C: 00                                               ' Moral
002D: 00 00 00 00  02 00 00 00  00 00 00 00  00 00 00 00  ' UDLR
...
004C: 1F 00 00 00  00 00 00 00  1F 00 00 00  00 00 00 00  ' array 31×31
```

### 1.7 Relation avec le format C# actuel `.fmap`

Le dépôt MMO_Maker utilise un format **moderne** `FMAP` v3/v4 (`Frog.Core.IO.MapSerializer`) — **incompatible** octet-à-octet avec `.fcc`.  
L’importeur legacy (`Frog.Legacy`, à créer) doit produire le modèle domaine, pas un simple renommage de fichier.

---

## 2. Prochaines formats

Items (`.itm` / équivalent), NPC, quêtes (`.fcq` vu dans `LoadQuete`) : **non caractérisés** dans cette révision.
