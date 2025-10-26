# Analyse Fonctionnelle : Module Partagé `Frog.Core`

---

## 1. Types de données partagés

### 1. Nom de la fonctionnalité
**Structures partagées entre client, serveur et éditeur**

### 2. Description fonctionnelle (VB6)
Tous les modules (éditeur, client, serveur) utilisent les mêmes types définis dans `modTypes.bas`, ce qui garantit une interopérabilité parfaite au prix d’un couplage fort. Cela inclut : `Map`, `Tile`, `Npc`, `Player`, `Item`, `Account`, etc.

### 3. Fichiers et modules VB6 impliqués
- `modTypes.bas`
- `modEnumerations.bas`
- `modConstants.bas`

### 4. Données manipulées
- Types `Type ... End Type` (UDT)
- Constantes `Const`, `Enum`

### 5. Dépendances critiques
- Synchronisation stricte des structures entre tous les modules
- Changements dangereux si non propagés
- Dépend du format binaire fixe pour lecture/sauvegarde

### 6. Problèmes à surveiller (VB6 vs C#)
- Aucun mécanisme de versionnage
- Structures figées, peu flexibles
- Champ à champ sans encapsulation

### 7. Plan de conversion vers C#
- Créer un projet `Frog.Core` référencé par tous
- `Models/` : `Map.cs`, `Tile.cs`, `Player.cs`, `Npc.cs`, etc.
- Ajouter `DataContract` ou attributs de sérialisation
- Optionnel : centraliser les tailles de struct pour compatibilité binaire

### 8. Tests à prévoir
- Référence croisée entre projets
- Comparaison des objets en mémoire (test de compatibilité)
- Lecture/sauvegarde cohérente

---

## 2. Énumérations globales

### 1. Nom de la fonctionnalité
**Énumérations partagées**

### 2. Description fonctionnelle (VB6)
Toutes les constantes de type enum (ex: `TileType`, `ItemType`, `StatEnum`, `DirEnum`, `LayerEnum`, `AccessRightEnum`) sont centralisées dans un module.

### 3. Fichiers et modules VB6 impliqués
- `modEnumerations.bas`
- `modTypes.bas`

### 4. Données manipulées
- `Enum` VB6, parfois avec des valeurs explicites

### 5. Dépendances critiques
- L’ordre des valeurs est utilisé dans les fichiers binaires
- Doit être strictement identique entre tous les exécutables

### 6. Problèmes à surveiller (VB6 vs C#)
- Enum non typés (implicites `Integer`)
- Aucune documentation attachée

### 7. Plan de conversion vers C#
- `Enums/TileType.cs`, `ItemType.cs`, `Direction.cs`, etc.
- Utilisation d’enums typés `: byte` ou `: int` selon besoin binaire
- Ajouter des descriptions via `[Description]` si utile à l’UI

### 8. Tests à prévoir
- Enum identiques dans client, serveur, éditeur
- Valeurs explicites respectées
- Sérialisation/désérialisation correcte

---

## 3. Constantes partagées

### 1. Nom de la fonctionnalité
**Constantes globales utilisées dans toutes les couches**

### 2. Description fonctionnelle (VB6)
Toutes les constantes utilisées dans les 3 projets sont centralisées (ex: `MAX_PLAYERS`, `MAX_MAPS`, `MAX_ITEMS`, codes de touches, identifiants spéciaux…)

### 3. Fichiers et modules VB6 impliqués
- `modConstants.bas`

### 4. Données manipulées
- Constantes `Public Const`

### 5. Dépendances critiques
- Certaines constantes contrôlent la taille des buffers réseau
- Impactent les limites dans les fichiers binaires

### 6. Problèmes à surveiller (VB6 vs C#)
- Risques d’incohérences si dupliquées
- Aucun regroupement logique

### 7. Plan de conversion vers C#
- `Constants/GameLimits.cs`, `Constants/Keys.cs`, `Constants/PacketIds.cs`
- Regrouper les constantes par domaine
- Prévoir éventuelle migration vers config dynamique

### 8. Tests à prévoir
- Compilation croisée sans conflit
- Contrôle de limites (map, items, NPC…)

---

## 4. Compatibilité binaire et sérialisation

### 1. Nom de la fonctionnalité
**Interopérabilité binaire entre projets**

### 2. Description fonctionnelle (VB6)
Tous les projets utilisent les mêmes structures pour écrire/relire des fichiers binaires (map, item, npc, account, etc.), ce qui repose sur un agencement strict de la mémoire.

### 3. Fichiers et modules VB6 impliqués
- `modFileIO.bas`
- `modTypes.bas`

### 4. Données manipulées
- Offsets mémoire implicites
- Longueurs fixes

### 5. Dépendances critiques
- Nécessité absolue de conserver l’ordre des champs
- Conversion difficile si on change un seul champ

### 6. Problèmes à surveiller (VB6 vs C#)
- Aucune abstraction
- Pas de version de fichier

### 7. Plan de conversion vers C#
- Ajouter attributs `[StructLayout(LayoutKind.Sequential)]` si nécessaire
- Créer une `MapSerializer`, `ItemSerializer`, etc.
- Ajouter numéro de version de fichier

### 8. Tests à prévoir
- Lecture des fichiers VB6 en C#
- Comparaison binaire
- Sauvegarde/export C# compatible avec client existant


---

## 5. Validation de données

### 1. Nom de la fonctionnalité
**Règles de validation des modèles**

### 2. Description fonctionnelle (VB6)
Peu présente. Certaines validations sont implicites dans la logique métier (ex: une arme doit avoir une statistique d'attaque, un objet de soin doit avoir une valeur de soin…).

### 3. Fichiers et modules VB6 impliqués
- `modGeneral.bas`
- `modItemEditor.bas`

### 4. Données manipulées
- Champs dans `Item()`, `Npc()`, `Map.Tile()`, etc.

### 5. Dépendances critiques
- Qualité des données
- Impact sur le gameplay

### 6. Problèmes à surveiller (VB6 vs C#)
- Aucune validation automatique
- Bugs silencieux fréquents (ex: valeur par défaut invalide)

### 7. Plan de conversion vers C#
- Ajouter méthodes de validation dans chaque modèle
- `IValidatable`, `Validate()` sur `Item`, `Npc`, `Map`, etc.
- Validation dans les éditeurs et avant sauvegarde

### 8. Tests à prévoir
- Modèle incomplet → rejeté
- Propriétés incohérentes détectées

---

## 6. Interfaces fonctionnelles partagées

### 1. Nom de la fonctionnalité
**Interfaces C# pour factorisation**

### 2. Description fonctionnelle (VB6)
Aucune. Tous les types sont des UDT plats. Aucune interface ou héritage.

### 3. Fichiers et modules VB6 impliqués
- `modTypes.bas`

### 4. Données manipulées
- `Map`, `Player`, `Npc`, `Item`, etc.

### 5. Dépendances critiques
- Difficulté à factoriser la logique commune

### 6. Problèmes à surveiller (VB6 vs C#)
- Copie de code (ex: position x/y présente partout)
- Aucune abstraction de comportement

### 7. Plan de conversion vers C#
- Créer `IPositioned`, `IEntity`, `IHasStats`, etc.
- Utiliser ces interfaces dans les services, rendu, logique

### 8. Tests à prévoir
- Implémentation correcte dans les modèles
- Résolution polymorphique fonctionnelle

---

## 7. Structures auxiliaires (géométrie, couleur…)

### 1. Nom de la fonctionnalité
**Types utilitaires de base**

### 2. Description fonctionnelle (VB6)
VB6 ne dispose pas de `Point`, `Rectangle`, `Color` standards. Tout est fait avec des entiers et tableaux.

### 3. Fichiers et modules VB6 impliqués
- `modMap.bas`, `modGraphics.bas`

### 4. Données manipulées
- Coordonnées, tailles, couleurs

### 5. Dépendances critiques
- Manipulations fréquentes en rendu et collisions

### 6. Problèmes à surveiller (VB6 vs C#)
- Code illisible avec `(x, y)` bruts
- Aucune réutilisation

### 7. Plan de conversion vers C#
- Créer `Point.cs`, `Rectangle.cs`, `Size.cs`, `ColorRgba.cs`
- Utilisation dans les modèles et rendu

### 8. Tests à prévoir
- Calculs de rectangle d’intersection
- Couleurs cohérentes en affichage

---

## 8. Types de paquets réseau

### 1. Nom de la fonctionnalité
**Identifiants et structures des paquets**

### 2. Description fonctionnelle (VB6)
Les paquets sont identifiés par des entiers et dispatchés selon des `Select Case` dans `modHandleData.bas`.

### 3. Fichiers et modules VB6 impliqués
- `modHandleData.bas`
- `modConstants.bas`

### 4. Données manipulées
- Identifiants de paquet (byte)
- Données sérialisées

### 5. Dépendances critiques
- Synchronisation client ↔ serveur
- Compatibilité stricte

### 6. Problèmes à surveiller (VB6 vs C#)
- Tout est en dur
- Aucun regroupement ou documentation

### 7. Plan de conversion vers C#
- `Enums/PacketId.cs`
- `Packets/` : classes représentant chaque paquet
- Documentation des structures échangées

### 8. Tests à prévoir
- Correspondance client/serveur
- Traitement correct par dispatcher

---

## 9. Utilitaires partagés

### 1. Nom de la fonctionnalité
**Fonctions d’aide communes**

### 2. Description fonctionnelle (VB6)
Dispersées dans des modules divers : conversion de chaînes, hash, mathématiques, vérification de bornes…

### 3. Fichiers et modules VB6 impliqués
- `modGeneral.bas`
- `modUtility.bas`

### 4. Données manipulées
- Chaînes, nombres, buffers, tableaux

### 5. Dépendances critiques
- Réutilisation dans tout le projet

### 6. Problèmes à surveiller (VB6 vs C#)
- Code dupliqué
- Faible lisibilité

### 7. Plan de conversion vers C#
- `Utils/ByteHelper.cs`, `StringHelper.cs`, `HashHelper.cs`
- Méthodes statiques ou extensions

### 8. Tests à prévoir
- Hash correct
- Manipulation de tableau
- Conversion chaînes ↔ bytes

---

## 10. Extensions recommandées (modularité C#)

### 1. Nom de la fonctionnalité
**Améliorations orientées architecture**

### 2. Description fonctionnelle (futur C#)
Ajouts proposés pour structurer proprement `Frog.Core` comme une base saine et évolutive.

### 3. Éléments à intégrer
- Interfaces `IPositioned`, `IEntity`, `IHasStats`
- Structs `Point`, `Size`, `Color`, `Rectangle`
- `PacketId.cs` + classes de paquet (`MapUpdatePacket`, etc.)
- `Utils/` avec helpers typés
- Attributs `[StructLayout(LayoutKind.Sequential)]`
- Ajout de validation dans les modèles (méthodes `Validate()`)

### 4. Objectif
- Mieux structurer la logique commune
- Faciliter le testing unitaire
- Minimiser duplication & erreurs interprojets

