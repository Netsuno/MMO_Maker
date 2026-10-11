# Données de jeu

← [Guides](README.md) · [Quickstart auteur](CREATOR_QUICKSTART.md) · [Espace de travail](../../../EDITOR_WORKSPACE.md) · [Inventaire UI](../../../EDITOR_UI_INVENTORY.md) · [Modèle de données](../../../DATA_MODEL.md)

> Peindre une carte : [quickstart auteur](CREATOR_QUICKSTART.md). Objets de carte, tuiles composées et zones de monstres ont leur guide. Cette page décrit la fenêtre entière.

À la fin de ce guide, tu sais ce que chaque catégorie de **Données de jeu** enregistre, ce qui est un brouillon, et ce que le serveur lit vraiment.

Hello / protocole **v11** (`FrogWireProtocol.Version`). Éditeur = **Windows 11 x64**. Les tuiles de contenu (`TileAsset`, carte v6, TilePack, tuiles composées) restent **48×48**. La catégorie **Tilesets** est une feuille : son champ **Taille tuile** n’est pas cette tuile de contenu (voir plus bas).

PostgreSQL doit répondre. Sinon la barre de statut finit sur `mémoire (démo — non persistant)`, et **Enregistrer brouillon**, **Publier** et **Supprimer** restent inactifs (`AllowsSave` faux). **Nouveau**, **Dupliquer** et **Importer…** restent utilisables : rien n’est écrit en base.

## Ouvrir la fenêtre

**Ressources → Données de jeu…** (`MainWindow.CmdGameData` → `MainWindow.OpenGameData`). La fenêtre s’intitule **Données de jeu** (960×640, sans bouton réduire). Le chargement affiche `Initialisation des données de jeu…`.

La liste de gauche fait 200 px. Elle a **14** entrées, dans cet ordre (`GameDataForm`) :

| Index | Libellé | Panneau |
| --- | --- | --- |
| 0 | Tilesets | `TilesetEditorPanel` |
| 1 | NPCs / monstres | `NpcEditorPanel` |
| 2 | Objets | `ItemEditorPanel` |
| 3 | Sorts / compétences | `SpellEditorPanel` |
| 4 | Classes | `ClassEditorPanel` |
| 5 | Héros | `ActorEditorPanel` |
| 6 | Compétences | `SkillEditorPanel` |
| 7 | Système | `SystemEditorPanel` |
| 8 | Boutiques | `ShopEditorPanel` |
| 9 | Ressources / spawns | `ResourceAndSpawnEditorPanel` |
| 10 | Armes | `EquipItemEditorPanel` (`ItemType.Weapon`) |
| 11 | Armures | `EquipItemEditorPanel` (`ItemType.Armor`) |
| 12 | Objets de carte | `MapObjectEditorPanel` |
| 13 | Tuiles composées | `ComposedTilesetEditorPanel` |

L’ouverture sélectionne **Tilesets**. Quand l’init se termine, la barre affiche `Prêt — PostgreSQL` ou `Prêt — mémoire (démo — non persistant)`.

<!-- CAPTURE: Fenêtre Données de jeu 960×640, liste de gauche des 14 catégories avec Tilesets sélectionné, bandeau « Initialisation » déjà remplacé par la barre de statut « Prêt — PostgreSQL » -->
*Capture à venir.*

## Brouillon, publication, listes

Presque chaque fiche a, à gauche (260 px, 280 pour les interrupteurs, 320 pour les spawns) :

- le filtre **Tous / Brouillon / Publié** ;
- le champ `Rechercher…` ;
- la liste.

Chaque ligne est `Nom [Draft]` ou `Nom [Published]`. Ce sont les noms de `ContentPublishStatus` (`Draft`, `Published`). Le filtre au-dessus est en français. Un brouillon n’entre pas dans les catalogues publiés que le serveur consulte.

<!-- CAPTURE: Liste d’une catégorie avec une ligne « Nom [Draft] » et une ligne « Nom [Published] », filtre « Brouillon / Publié » en français au-dessus -->
*Capture à venir.*

Boutons du bas, quand ils existent : **Nouveau**, **Dupliquer**, parfois **Importer…**, **Enregistrer brouillon**, **Publier**, **Supprimer**.

| Geste | Ce que tu vois |
| --- | --- |
| **Nouveau** | `Nouveau brouillon` |
| **Dupliquer** | `Copie créée` |
| Champ modifié | `Modifié (non enregistré)` |
| **Enregistrer brouillon** | `Brouillon enregistré rev=N` |
| **Publier** | `Publié rev=N` |
| Révision qui a bougé | `Conflit de révision (courante=N).` |
| **Importer…** réussi | `Asset importé (non enregistré)` (le libellé exact dépend de la catégorie) |

Changer de ligne avec des modifications en cours demande `Modifications non enregistrées. Continuer ?` (titre = le nom du panneau). Fermer la fenêtre sale demande `Modifications non enregistrées. Fermer quand même ?` (titre **Données de jeu**).

Le champ **État** rappelle l’identifiant, la révision, le statut et la révision publiée.

Les combos qui citent une autre fiche (sort de départ, arme, héros du groupe, article de boutique, ressource d’un spawn) ne proposent que le **publié**. Un brouillon de cette autre fiche n’y apparaît pas. Il faut le publier, puis rouvrir ou rafraîchir la liste.

## Trois écarts à connaître

**Fermer Données de jeu ne recharge pas tout.** La coque livrée (`MainWindow.OpenGameData`) appelle seulement `QueuePublishedMapObjectReload`. La radio **Objets** reçoit les props déjà présents plus les objets de carte **publiés**. Les tuiles composées **publiées** rejoignent la palette TileAsset au **démarrage** de l’éditeur (`ReloadPublishedComposedTilesetsIntoPaletteAsync` dans l’init de `MainForm`). `QueuePublishedComposedTilesetReload` n’est branché que sur `MainForm.OpenGameData`, et `App.xaml` n’ouvre pas cette fenêtre. Détail : [Objets de carte](MAP_OBJECTS.md), [Tuiles composées](COMPOSED_TILESETS.md).

**Le combo monstre liste les brouillons.** Zones de monstres et régions chargent `MapEncounterTroopCatalog.TryLoadAsync` : toutes les fiches `NpcKind.Monster`, sans filtre de statut. Le serveur, lui, ne fait apparaître qu’un monstre **publié** (`CombatGameplayService.SpawnMonsterAsync` lit `IPublishedNpcCatalog`). Un brouillon choisi dans le combo ne sort pas en jeu. Détail : [Zones de monstres](MOB_ZONES.md).

**Les listes parlent anglais, le filtre parle français.** `[Draft]` / `[Published]` viennent de l’énumération. **Tous / Brouillon / Publié** est le libellé du filtre. Plusieurs combos de type affichent aussi le nom d’énumération anglais (`Npc`, `Monster`, `Consumable`, `Spell`, `Self`…) : c’est noté dans la catégorie.

## 0. Tilesets

Feuille d’images pour la palette entière de l’éditeur (`TilesetCache`, alias **Palette éditeur**). Ce n’est pas le catalogue TileAsset des tuiles choisies une par une.

| Champ | Rôle |
| --- | --- |
| Nom | 1–120 caractères. Défaut `Nouveau tileset` |
| Chemin logique | relatif, unique, sans `..` ni séparateur Windows. Défaut `tiles/….png` |
| Aperçu | vignette 128×128 |
| Taille tuile | 8–256. Voir l’écart ci-dessous |
| Largeur px / Hauteur px | 8–8192. Doivent être des multiples de la taille, sinon `Dimensions non multiples de la taille de tuile.` |
| SHA-256 | 64 caractères hex, rempli par l’import |
| Palette éditeur | alias entier 0–99999. 0 = non assigné. L’import en choisit un s’il est vide |
| État | id, révision, statut |

**Importer…** ouvre **Importer un asset projet**. Le serveur charge les tilesets **publiés** (`PublishedTilesetConsumer`). L’éditeur les reprend dans le cache au démarrage (`HydrateTilesetCacheFromPublishedAsync`).

À **Nouveau**, **Taille tuile**, **Largeur px** et **Hauteur px** partent de la métrique monde (`WorldMetrics.DefaultTileSizePixels`, 32, même défaut que `TilesetDefinition.TileSizePixels`). Ce n’est pas la tuile de contenu. Pour peindre en **48×48**, voir [Tuiles composées](COMPOSED_TILESETS.md).

<!-- CAPTURE: Panneau Tilesets après Nouveau : Taille tuile, Largeur px et Hauteur px sur la métrique monde, distincte d’une tuile de contenu 48×48, boutons Nouveau Dupliquer Importer Enregistrer brouillon Publier Supprimer -->
*Capture à venir.*

## 1. NPCs / monstres

Une fiche sert de PNJ (`Npc`) ou de monstre (`Monster`). Le serveur ne fait apparaître que les monstres **publiés** (`FindNpcAsync` + `Kind == Monster`). Les PNJ de dialogue et les presets carte (PNJ rapide, coffre, porte, auberge) sont d’autres écrans : menu **Carte**.

| Champ | Rôle |
| --- | --- |
| Nom | 1–120. Défaut `Nouveau NPC` |
| Type | combo `Npc` puis `Monster` (noms d’énumération, pas « PNJ » / « Monstre ») |
| Chemin sprite | relatif, 1–500, sans `..` |
| Aperçu | 128×128 |
| Niveau | 1–99 |
| Notes | jusqu’à 2000 caractères |
| Alias éditeur | 0–99999. 0 = aucun. Sert aux anciennes références `map_npc_spawns` |
| État | id, révision, statut |

**Importer…** est dans la rangée du bas, avec **Nouveau**, **Dupliquer**, **Enregistrer brouillon**, **Publier** et **Supprimer**.

Le combo des zones et des régions montre le **nom** de chaque monstre, brouillon compris. L’identifiant posé sur la zone est celui de la fiche. Sans publication, `SpawnMonsterAsync` rend null.

<!-- CAPTURE: Panneau NPCs / monstres, combo Type ouvert sur Npc et Monster en anglais, filtre de statut en français -->
*Capture à venir.*

## 2. Objets

Inventaire : consommable, arme, armure, clé, quête. Ce n’est pas un prop de carte. Les armes et les armures de cette liste sont les **mêmes** fiches que les catégories **Armes** et **Armures** (même dépôt `Item`, sessions filtrées à part).

| Champ | Rôle |
| --- | --- |
| Nom | 1–120. Défaut `Nouvel objet` |
| Type | `Consumable`, `Weapon`, `Armor`, `Key`, `Quest` (noms anglais ; `Unknown` est absent). Défaut `Consumable` |
| Chemin icône | relatif, 1–500 |
| Aperçu | 128×128 |
| Pile maximum | 1–999 |
| Prix d’achat / Prix de vente | 0–999999999 |
| Description | jusqu’à 4000 caractères |
| État | id, révision, statut |

Le serveur lit le catalogue **publié** (`IPublishedItemCatalog`) : boutique, récompenses, commandes d’objet, équipement. Un brouillon n’est pas achetable ni donnable par ces chemins.

<!-- CAPTURE: Panneau Objets, combo Type avec Consumable Weapon Armor Key Quest en anglais -->
*Capture à venir.*

## 3. Sorts / compétences

Même dépôt que **Compétences** (`SpellDefinition`). Ici la liste n’est **pas** filtrée : un sort et une compétence y apparaissent tous les deux. **Compétences** ne montre que `SpellKind.Skill`.

| Champ | Rôle |
| --- | --- |
| Nom | 1–120. Défaut `Nouveau sort` |
| Type | `Spell` ou `Skill` (anglais) |
| Coût en mana | 0 ou plus |
| Recharge (ms) | 0 ou plus |
| Cible | `Self`, `SingleEnemy`, `SingleAlly`, `AoE` (anglais). Défaut `SingleEnemy` |
| Chemin icône | relatif |
| Aperçu | 128×128 |
| Description | jusqu’à 4000 |
| État | id, révision, statut |

Le combat ne résout qu’un sort **publié** (`IPublishedSpellCatalog`). La création de personnage copie le sort de départ de la **classe publiée**.

<!-- CAPTURE: Panneau Sorts / compétences, combos Type et Cible en anglais (Spell, SingleEnemy), à comparer avec Compétences où la cible est en français -->
*Capture à venir.*

## 4. Classes

Fiche de création de personnage. `CharacterGameplayService.CreateAsync` refuse une classe absente du catalogue publié (`Classe non publiee.`), sauf le repli synthétique de la classe démo quand aucun publié n’existe.

| Champ | Rôle |
| --- | --- |
| Nom | 1–120. Défaut `Nouvelle classe` |
| Notes | jusqu’à 4000 |
| PV de base / PM de base | minimum 1. Défaut 100 et 50 |
| FOR, AGI, VIT, INT, DEX, CHANCE | 1–99. Défaut 10 |
| Sort de départ | sorts **publiés**, ou **Aucun** |
| Arme par défaut / Armure par défaut | armes ou armures **publiées**, ou **Aucune** |
| État | id, révision, statut |

Pas de bouton **Importer…**. Les stats hors 1–99 affichent `Chaque statistique doit être comprise entre 1 et 99.`

La commande d’événement **Changer classe** écrit l’identifiant sur le personnage **sans** relire cette fiche : le niveau et les stats déjà en jeu ne sont pas recalculés. Voir [Commandes d’événement](EVENT_COMMANDS.md).

<!-- CAPTURE: Panneau Classes, stats FOR à CHANCE, combos Sort de départ Arme par défaut Armure par défaut avec Aucun / Aucune -->
*Capture à venir.*

## 5. Héros

Fiche d’acteur (`ActorDefinition`) : apparence et équipement de départ. Les héros **publiés** remplissent le groupe de **Système**. `Frog.Server` n’a pas de catalogue héros publié : créer un personnage en jeu passe par une **classe** publiée, pas par cette fiche.

| Champ | Rôle |
| --- | --- |
| Nom | 1–120. Défaut `Nouveau héros` |
| Description | jusqu’à 4000 |
| Classe | classes **publiées**, ou **Aucune** (vide accepté ; un guid vide est refusé) |
| Visage (chemin) | optionnel, relatif, sans `..` |
| Corps | Chevalier, Forêt, Nuit, Sable |
| Cheveux | Naturel, Brun, Blond, Noir, Roux |
| Tunique | Aucune, Ocre, Lin, Cramoisi |
| Arme de départ / Armure de départ | armes ou armures **publiées**, ou **Aucune**. Les deux ne peuvent pas être le même objet |
| PV de base / PM de base | minimum 1. Défaut 100 et 50 |
| FOR … CHANCE | 1–99. Défaut 10 |
| État | id, révision, statut |

Pas d’**Importer…**. Une apparence hors de ces listes affiche `Apparence hors des planches déjà en jeu (corps, cheveux, tunique).`

<!-- CAPTURE: Panneau Héros, combos Corps Cheveux Tunique avec les libellés français (Chevalier, Naturel, Aucune) -->
*Capture à venir.*

## 6. Compétences

Même table que **Sorts / compétences**, filtrée sur `SpellKind.Skill`. **Nouveau** pose `Nouvelle compétence` et verrouille le type sur le libellé **Compétence** (pas un combo).

| Champ | Rôle |
| --- | --- |
| Nom | 1–120 |
| Type | libellé fixe **Compétence** |
| Description | jusqu’à 4000 |
| Coût en PM | 0 ou plus (le champ s’appelle mana dans le modèle) |
| Recharge (ms) | 0 ou plus |
| Cible | **Utilisateur**, **Un ennemi**, **Un allié**, **Zone** (`TargetTypeLabels`) |
| Chemin icône, Aperçu, État | comme les sorts. **Importer…** est dans la rangée du bas |

La commande **Changer compétences** ajoute ou retire un guid dans la liste apprise du personnage (64 au plus). Une compétence **publiée** reste lançable par la barre de sorts existante même si elle n’est pas dans cette liste (`CharacterSkillSet`).

<!-- CAPTURE: Panneau Compétences, type figé sur Compétence, combo Cible en français (Un ennemi, Zone) -->
*Capture à venir.*

## 7. Système

Deux onglets. **Paramètres** est le premier onglet, **Interrupteurs / variables** le second. À l’ouverture, c’est le second qui est sélectionné (`SelectedIndex = 1`).

<!-- CAPTURE: Catégorie Système, onglets Paramètres et Interrupteurs / variables, le second actif, filtre Interrupteurs / Variables -->
*Capture à venir.*

### Interrupteurs / variables

Catalogue de noms (`SystemFlagDefinition`), distinct des interrupteurs du personnage. Le filtre de type est **Interrupteurs** / **Variables**. **Nouveau** suit le filtre : libellé `Nouvel interrupteur` ou `Nouvelle variable`, identifiant `sw_` ou `var_` plus 12 caractères hex.

| Champ | Rôle |
| --- | --- |
| Libellé | 1–120 |
| Identifiant | lettres, chiffres, `_`. 64 octets UTF-8 maximum |
| Type | libellé fixe **Interrupteur** ou **Variable** (celui du filtre) |
| Note | jusqu’à 500 caractères |
| État | id, révision, statut |

`Frog.Server` ne lit pas ce catalogue. Les commandes **Interrupteur** et **Variable** écrivent la clé tapée dans l’événement sur l’état du **personnage** (`ICharacterWorldStateRepository`). Pour que les deux se rejoignent, la clé de la commande doit être exactement cet identifiant.

### Paramètres

Une seule ligne projet (`SystemDefinition`, identifiant fixe). Défauts : unité **Or**, terme HP **HP**, terme MP **MP**.

| Champ | Rôle |
| --- | --- |
| Unité monétaire | 1–32 caractères |
| Terme HP / Terme MP | 1–32 |
| Groupe de départ | jusqu’à 4 héros **publiés** |
| Carte de départ | cartes du dépôt, ou aucune |
| Musique du titre / Musique de départ | fichier, **Volume** 0–100, **Fondu (ms)** 0–60000, **Parcourir…**, **Effacer** |
| État | révision du document |

**Enregistrer brouillon** / **Publier** de cet onglet affichent `Paramètres enregistrés rev=N` ou `Paramètres publiés rev=N`. La création de personnage, elle, prend la classe publiée et, si le monde publié est exigé, le spawn du monde (`GetSpawnConfigAsync`) — pas cette carte de départ.

## 8. Boutiques

| Champ | Rôle |
| --- | --- |
| Nom | 1–120. Défaut `Nouvelle boutique` |
| Description | jusqu’à 4000 |
| Articles | grille : **Objet publié**, **Prix**, **Stock (vide = ∞)**. **Ajouter article**, **Retirer article** |
| État | id, révision, statut |

Un objet une seule fois par boutique. Un article invalide affiche `Article de boutique invalide.` La commande **Ouvrir boutique** ne trouve qu’une boutique **publiée**. Sinon le joueur voit `Boutique indisponible.` Le modèle de fiche ne contient pas la logique d’achat.

<!-- CAPTURE: Panneau Boutiques, grille Articles avec colonnes Objet publié, Prix, Stock (vide = ∞), boutons Ajouter article et Retirer article -->
*Capture à venir.*

## 9. Ressources / spawns

Deux onglets : **Ressources**, puis **Spawns**.

### Ressources

| Champ | Rôle |
| --- | --- |
| Nom | 1–120. Défaut `Nouvelle ressource` |
| Description | jusqu’à 4000 |
| Chemin sprite | relatif, obligatoire |
| Aperçu | 128×128 |
| Réapparition (s) | 0 ou plus |
| Outil publié | objet publié, ou aucun |
| Objet produit | objet **publié**, obligatoire |
| Quantité produite | 1–999 |
| État | id, révision, statut |

Le serveur charge les définitions **publiées** (`PublishedResourceConsumer`). La fiche elle-même ne décrit pas le geste de récolte.

### Spawns

| Champ | Rôle |
| --- | --- |
| Carte | cartes du dépôt |
| Ressource publiée | ressources **publiées** |
| Tuile X / Tuile Y | 0 ou plus |
| État | id, révision, statut |

Filtres au-dessus de la liste : statut, carte, ressource. Le serveur charge les placements **publiés** (`PublishedResourceSpawnConsumer`), filtrables par carte. Pas de bouton **Importer…** sur cet onglet.

<!-- CAPTURE: Catégorie Ressources / spawns, onglet Spawns, champs Carte, Ressource publiée, Tuile X, Tuile Y -->
*Capture à venir.*

## 10. Armes et 11. Armures

Même dépôt que **Objets**, liste filtrée. **Type** et **Emplacement** sont des libellés fixes, pas des combos : **Arme** ou **Armure** (`EquipPaperdollSlot`), le slot paperdoll déjà en jeu. **Nouveau** nomme `Nouvelle arme` ou `Nouvelle armure` et force le type.

Les autres champs sont ceux des objets : nom, chemin d’icône, aperçu, pile 1–999, prix, description, état. **Importer…** est dans la rangée. Barre : `Backend armes : …` ou `Backend armures : …`.

Un objet créé ici réapparaît dans **Objets** avec le type `Weapon` ou `Armor`. Le paperdoll du client ne connaît que ces deux emplacements.

<!-- CAPTURE: Panneau Armes, Type et Emplacement tous deux sur le libellé Arme, pas un combo -->
*Capture à venir.*

## 12. Objets de carte

Props placables. Index 12, après Armures. Ce n’est pas l’inventaire. Parcours complet : [Objets de carte](MAP_OBJECTS.md).

En bref : nom, chemin logique, identifiant de pose, empreinte en cases (1–32), taille en pixels, SHA-256. Seul le **publié** entre dans la radio **Objets**, et seulement à la **fermeture** de cette fenêtre dans la coque livrée.

## 13. Tuiles composées

Tuiles déjà en **48×48**, dans l’ordre choisi, pas une feuille découpée. Parcours complet : [Tuiles composées](COMPOSED_TILESETS.md).

En bref : nom, chemin logique, liste ordonnée. Le **publié** rejoint la palette TileAsset au **prochain démarrage**, pas à la fermeture WPF de Données de jeu. Le client charge la charge signée **CTS1**. Hello reste **11**.

## Hors de cette fenêtre

Ces surfaces ont été notées dans l’inventaire NET-8 (écart 8) et ne sont pas des catégories de `GameDataForm` :

- régions et rencontres (radio **Régions**, inspecteur) ;
- drapeaux de tuile, autotile, taille et décalage de carte ;
- audio de la carte (propriétés) ;
- événements communs (**Ressources → Événements communs…**, aussi sous **Carte**).

Les commandes posées sur une page de carte sont dans [Commandes d’événement](EVENT_COMMANDS.md).

## Tu es prêt si…

- [ ] La liste de gauche a 14 lignes, **Objets** avant **Armes**, **Objets de carte** après **Armures**
- [ ] Une ligne enregistrée affiche `[Draft]` ou `[Published]`, et le filtre dit **Brouillon** / **Publié**
- [ ] PostgreSQL injoignable : **Enregistrer brouillon** et **Publier** sont inactifs
- [ ] Un monstre seulement enregistré en brouillon peut être choisi dans une zone, et le serveur ne le fait pas apparaître
- [ ] Fermer la fenêtre depuis la coque livrée met à jour la radio **Objets**, pas la palette des tuiles composées
- [ ] **Taille tuile** d’un tileset neuf n’est pas 48 ; les tuiles composées, elles, sont en **48×48**

## Et après ?

- [Objets de carte](MAP_OBJECTS.md) · [Tuiles composées](COMPOSED_TILESETS.md) · [Zones de monstres](MOB_ZONES.md)
- [Commandes d’événement](EVENT_COMMANDS.md)
- [Quickstart auteur](CREATOR_QUICKSTART.md)
- [Modèle de données](../../../DATA_MODEL.md)
