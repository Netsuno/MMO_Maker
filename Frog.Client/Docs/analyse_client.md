# Analyse Fonctionnelle : Client de Jeu (Frog.Client)

---

## 1. Affichage de la carte

### 1. Nom de la fonctionnalité
**Affichage de la carte**

### 2. Description fonctionnelle (comportement VB6)
Le client affiche la carte en utilisant les couches graphiques. Le rendu dépend de la position du joueur, avec un centrage dynamique. Les couches sont affichées dans un ordre défini (sol, masques, objets, animations).

### 3. Fichiers et modules VB6 impliqués
- `modMap.bas`
- `modDirectDraw.bas`
- `modGameLogic.bas`

### 4. Données manipulées
- `Map.Tile(x, y).Layer()`
- Position du joueur (`MyIndex`, `Player(MyIndex).X/Y`)
- Coordonnées écran

### 5. Dépendances critiques
- Rendu DirectDraw (obsolète)
- Structures partagées avec l’éditeur
- Scroll fluide sans tearing

### 6. Problèmes à surveiller (VB6 vs C#)
- DirectDraw → à remplacer par GDI+/SkiaSharp/MonoGame
- Performances : rendu de grandes cartes en 60 FPS
- DoEvents à remplacer

### 7. Plan de conversion vers C#
- `Controls/MapView.cs` pour le rendu
- `Services/MapRenderer.cs` : moteur de dessin
- `Models/Map.cs`, `Tile.cs`, `Layer.cs`
- Découplage logique/rendu

### 8. Tests à prévoir
- Rendu fluide d’une carte avec plusieurs couches
- Scroll autour du joueur
- Chargement dynamique de tuiles
- Affichage correct de la couche active

---

## 2. Entités : Joueurs, PNJ, Mobs

### 1. Nom de la fonctionnalité
**Entités visibles (joueurs, PNJ, monstres)**

### 2. Description fonctionnelle (comportement VB6)
Les entités sont affichées dynamiquement. Chaque entité a un sprite, une position, une animation, une direction. Le serveur met à jour ces données en temps réel.

### 3. Fichiers et modules VB6 impliqués
- `modHandleData.bas`
- `modGameLogic.bas`
- `modTypes.bas`

### 4. Données manipulées
- `Player()`, `Npc()`, `MapNpc()`
- Sprites
- Animation, direction, position

### 5. Dépendances critiques
- Réception réseau en temps réel
- Coordination avec le moteur de rendu
- Synchronisation entre entités

### 6. Problèmes à surveiller (VB6 vs C#)
- Animations figées ou désynchronisées
- Mauvais calage des sprites (origin point)
- Z-order incorrect (joueur caché derrière éléments)

### 7. Plan de conversion vers C#
- `Models/Entity.cs`, `Player.cs`, `Npc.cs`
- `Services/EntityRenderer.cs`
- `PacketHandlers/EntityUpdateHandler.cs`
- Gestion des animations et Z-index

### 8. Tests à prévoir
- Affichage dynamique des entités en mouvement
- Animation fluide synchronisée
- Apparition/disparition d’entités
- Ordre de superposition correct

---

## 3. Interface utilisateur (HUD)

### 1. Nom de la fonctionnalité
**HUD (barres de vie, mana, interface, fenêtres)**

### 2. Description fonctionnelle (comportement VB6)
Affichage des informations du joueur (HP, MP, EXP), du chat, des boutons d’accès à l’inventaire, fiche joueur, options…

### 3. Fichiers et modules VB6 impliqués
- `frmMirage.frm`
- `modGameLogic.bas`
- `modConstants.bas`

### 4. Données manipulées
- `Player(MyIndex).HP`, `MP`, `EXP`
- Messages de chat
- Fenêtres ouvertes

### 5. Dépendances critiques
- Rendu fluide et clair
- Réactivité UI à la souris
- Affichage multi-panneaux

### 6. Problèmes à surveiller (VB6 vs C#)
- Contrôles WinForms vs dessin DirectDraw
- Couplage logique/UI fort dans VB6
- Z-order de fenêtres à gérer manuellement

### 7. Plan de conversion vers C#
- `Forms/GameForm.cs` : fenêtre principale
- `Controls/StatusBar.cs`, `ChatPanel.cs`, `InventoryPanel.cs`
- `Services/UIService.cs` : gestion des états visibles

### 8. Tests à prévoir
- Affichage correct des barres HP/MP
- Chat fonctionnel
- Navigation dans les menus
- Superposition propre des fenêtres

---

## (la suite arrive…)
---

## 4. Réseau : Connexion et paquets

### 1. Nom de la fonctionnalité
**Connexion réseau et traitement des paquets**

### 2. Description fonctionnelle (comportement VB6)
Le client se connecte au serveur via TCP, envoie des requêtes binaires (connexion, déplacement, actions) et reçoit des réponses structurées (état du monde, messages système, entités, erreurs).

### 3. Fichiers et modules VB6 impliqués
- `modClientTCP.bas`
- `modHandleData.bas`
- `modConstants.bas`

### 4. Données manipulées
- Socket TCP
- Structures binaires des paquets
- État du jeu et données de réponse

### 5. Dépendances critiques
- Fiabilité de la connexion
- Ordre et synchronisation des paquets
- Traitement rapide pour éviter les délais

### 6. Problèmes à surveiller (VB6 vs C#)
- Stack TCP bloquante
- Manque de validation des paquets
- Structures binaires à synchroniser parfaitement

### 7. Plan de conversion vers C#
- `Services/NetworkService.cs`
- `Network/PacketReader.cs`, `PacketWriter.cs`
- `Network/Handlers/` pour chaque type de message
- Asynchrone avec `Sockets` ou `TcpClient`

### 8. Tests à prévoir
- Connexion et reconnexion
- Envoi et réception de paquets de test
- Résilience aux coupures réseau
- Compatibilité avec le serveur VB6 ou C#

---

## 5. Déplacement du joueur

### 1. Nom de la fonctionnalité
**Déplacement du joueur**

### 2. Description fonctionnelle (comportement VB6)
Le joueur contrôle son personnage via les touches directionnelles. Chaque mouvement envoie une requête au serveur qui valide et renvoie la nouvelle position. L’animation de déplacement est gérée localement.

### 3. Fichiers et modules VB6 impliqués
- `modInput.bas`
- `modGameLogic.bas`
- `modHandleData.bas`

### 4. Données manipulées
- `Player(MyIndex).X/Y`
- `Dir`, `Moving`, `OffsetX/Y`
- Position cible envoyée au serveur

### 5. Dépendances critiques
- Validation serveur
- Animation fluide locale
- Absence de désynchronisation

### 6. Problèmes à surveiller (VB6 vs C#)
- Lag entre entrée et affichage
- Décalage entre position serveur et locale
- Interpolation à implémenter

### 7. Plan de conversion vers C#
- `InputService.cs` : gestion clavier
- `MovementService.cs` : logique locale
- `Entity.cs` : mise à jour de position et offset
- `PacketSender.cs` : envoi des mouvements

### 8. Tests à prévoir
- Déplacement fluide sans latence visible
- Correction de position par le serveur
- Animation synchronisée

---

## 6. Chargement des ressources

### 1. Nom de la fonctionnalité
**Chargement des ressources graphiques**

### 2. Description fonctionnelle (comportement VB6)
Les ressources visuelles comme tilesets, sprites, ressources et objets sont chargées au démarrage depuis des fichiers `.bmp` ou `.png`. Ces images sont utilisées pour le rendu des entités et cartes.

### 3. Fichiers et modules VB6 impliqués
- `modGraphics.bas`
- `modDirectDraw.bas`

### 4. Données manipulées
- `DDS_TileSurf`, `DDS_CharSurf`, etc.
- Fichiers image locaux

### 5. Dépendances critiques
- Disponibilité des fichiers
- Format d’image attendu
- Correspondance ID ↔ Sprite

### 6. Problèmes à surveiller (VB6 vs C#)
- Erreur silencieuse si fichier manquant
- Pas de fallback ou de message utilisateur
- Surface DirectDraw obsolète

### 7. Plan de conversion vers C#
- `Assets/AssetLoader.cs` : chargement centralisé
- Utilisation de `Bitmap` en GDI+
- Gestion d'erreurs et logs

### 8. Tests à prévoir
- Chargement correct des images
- Message clair si fichier absent
- Affichage correct des sprites

---

## 7. Inventaire et objets

### 1. Nom de la fonctionnalité
**Inventaire et gestion des objets**

### 2. Description fonctionnelle (comportement VB6)
Le client affiche l’inventaire, permet l’utilisation, l’équipement ou le drop d’un objet. Le serveur gère la validation et les effets.

### 3. Fichiers et modules VB6 impliqués
- `frmMirage.frm`
- `modHandleData.bas`
- `modGameLogic.bas`

### 4. Données manipulées
- `Inventory()`
- `Item()` (depuis serveur)
- `Player(MyIndex).Equipment()`

### 5. Dépendances critiques
- Synchronisation avec le serveur
- Affichage et retour utilisateur
- Mise à jour après chaque action

### 6. Problèmes à surveiller (VB6 vs C#)
- Mises à jour visuelles asynchrones
- Pas de validation locale (tromperies possibles)
- Risques de désynchronisation

### 7. Plan de conversion vers C#
- `Models/Item.cs`, `InventorySlot.cs`
- `Controls/InventoryPanel.cs`
- `Services/InventoryService.cs`

### 8. Tests à prévoir
- Affichage des objets
- Utilisation/équipement
- Réponse du serveur
- Synchronisation à la reconnexion

---

## 8. Chat et communication

### 1. Nom de la fonctionnalité
**Système de chat**

### 2. Description fonctionnelle (comportement VB6)
Permet aux joueurs de discuter via une boîte de texte. Messages envoyés au serveur, puis redistribués aux autres clients.

### 3. Fichiers et modules VB6 impliqués
- `frmMirage.frm`
- `modHandleData.bas`

### 4. Données manipulées
- Messages texte
- File de réception et d’affichage

### 5. Dépendances critiques
- Longueur max des messages
- Encodage texte
- Propreté visuelle du rendu

### 6. Problèmes à surveiller (VB6 vs C#)
- Possibilité d’injection
- Pas de filtre ou anti-spam
- Problèmes de scroll automatique

### 7. Plan de conversion vers C#
- `Controls/ChatBox.cs`
- `Models/ChatMessage.cs`
- `Services/ChatService.cs`

### 8. Tests à prévoir
- Envoi et réception d’un message
- Scroll correct
- Résilience aux spams

---

## 9. Rendu et boucle de jeu

### 1. Nom de la fonctionnalité
**Boucle de jeu et rendu global**

### 2. Description fonctionnelle (comportement VB6)
La boucle principale met à jour le rendu, traite les entrées, les entités et rafraîchit l’interface toutes les X ms via `DoEvents`.

### 3. Fichiers et modules VB6 impliqués
- `modGameLoop.bas`
- `modGameLogic.bas`
- `modInput.bas`

### 4. Données manipulées
- Entrées clavier
- Données à afficher (map, entités, HUD)

### 5. Dépendances critiques
- Fréquence de rafraîchissement constante
- Rendu fluide même en charge
- Traitement des erreurs runtime

### 6. Problèmes à surveiller (VB6 vs C#)
- `DoEvents` → obsolète
- Problème de synchronisation logique/rendu
- Fuites mémoire possibles

### 7. Plan de conversion vers C#
- `Services/GameLoop.cs` avec `Timer` ou `Task.Run`
- `IRenderer` pour découpler la logique
- Intégration avec `Application.Idle` ou boucle dédiée

### 8. Tests à prévoir
- 60 FPS constant sur carte normale
- Réactivité aux entrées
- Absence de blocage UI

---

## 10. Connexion et authentification

### 1. Nom de la fonctionnalité
**Login et authentification**

### 2. Description fonctionnelle (comportement VB6)
Le joueur saisit son pseudo et mot de passe dans un écran d’accueil. Le client envoie les informations au serveur pour validation, puis affiche la sélection de personnage ou entre directement dans le jeu.

### 3. Fichiers et modules VB6 impliqués
- `frmMainMenu.frm`
- `modClientTCP.bas`
- `modHandleData.bas`

### 4. Données manipulées
- Nom d’utilisateur
- Mot de passe (texte brut dans VB6)
- Statut de connexion

### 5. Dépendances critiques
- Sécurité du protocole
- Affichage des erreurs
- Gestion des doublons ou joueurs déjà connectés

### 6. Problèmes à surveiller (VB6 vs C#)
- Transmission non chiffrée (à sécuriser)
- Pas de gestion de compte en base de données
- Pas de timeout ou reconnexion automatique

### 7. Plan de conversion vers C#
- `Forms/LoginForm.cs`
- `Services/AuthService.cs`
- Message d’erreur clair
- Gestion de tentative, échecs, sessions

### 8. Tests à prévoir
- Connexion valide/invalide
- Mauvais mot de passe
- Cas joueur déjà connecté

---

## 11. Équipement du joueur

### 1. Nom de la fonctionnalité
**Gestion et affichage de l’équipement**

### 2. Description fonctionnelle (comportement VB6)
Le joueur peut équiper des objets depuis son inventaire. L’équipement modifie les statistiques et l’apparence (sprite avec épée, armure...).

### 3. Fichiers et modules VB6 impliqués
- `frmMirage.frm`
- `modGameLogic.bas`
- `modHandleData.bas`

### 4. Données manipulées
- `Player(MyIndex).Equipment(Slot)`
- `Item()` (type, bonus, visuel)
- Sprites spécifiques

### 5. Dépendances critiques
- Visuel cohérent avec l’état équipé
- Données reçues du serveur
- Modification des stats en direct

### 6. Problèmes à surveiller (VB6 vs C#)
- Rafraîchissement partiel du HUD
- Pas de retour si objet non équipé
- Aucune vérification client de validité

### 7. Plan de conversion vers C#
- `Models/Equipment.cs`, `EquipmentSlot.cs`
- `Services/EquipmentService.cs`
- `Controls/EquipmentPanel.cs`

### 8. Tests à prévoir
- Équipement/déséquipement
- Modification des stats
- Apparence à l’écran

---

## 12. Dialogues et interactions PNJ

### 1. Nom de la fonctionnalité
**Système de dialogues avec les PNJ**

### 2. Description fonctionnelle (comportement VB6)
Le joueur peut interagir avec certains PNJ. Une fenêtre s’ouvre, affichant du texte, parfois avec plusieurs options (achat, quêtes, discussions).

### 3. Fichiers et modules VB6 impliqués
- `modHandleData.bas`
- `frmDialog.frm` ou similaire (si présent)
- `modNpc.bas`

### 4. Données manipulées
- Texte de dialogue
- Index PNJ
- Choix de réponse

### 5. Dépendances critiques
- Réponses du serveur selon l’option choisie
- Séquence de dialogue (étapes multiples)
- Blocage du déplacement pendant dialogue

### 6. Problèmes à surveiller (VB6 vs C#)
- Texte dur codé ou mal formaté
- Pas de contrôle sur le flux
- Bugs si interaction interrompue

### 7. Plan de conversion vers C#
- `Forms/DialogForm.cs`
- `Models/DialogLine.cs`
- `Services/DialogService.cs`

### 8. Tests à prévoir
- Déclenchement au clic sur PNJ
- Affichage du texte correct
- Navigation multi-lignes ou choix

---

## 13. Effets de combat

### 1. Nom de la fonctionnalité
**Affichage des effets de combat**

### 2. Description fonctionnelle (comportement VB6)
Le client affiche des effets visuels : animation d’attaque, dégâts affichés, projectiles, effets de zone ou de statut.

### 3. Fichiers et modules VB6 impliqués
- `modCombat.bas`
- `modGraphics.bas`
- `modHandleData.bas`

### 4. Données manipulées
- Animation en cours
- Position cible/source
- Sprites spéciaux

### 5. Dépendances critiques
- Temps de réaction rapide
- Animation fluide
- Bonne synchronisation avec l’état réel

### 6. Problèmes à surveiller (VB6 vs C#)
- Effets liés à des timers ou à `DoEvents`
- Gestion manuelle des offsets
- Aucune réutilisation de sprite (pas d’optimisation)

### 7. Plan de conversion vers C#
- `Models/CombatEffect.cs`
- `Services/EffectRenderer.cs`
- `Controls/DamagePopup.cs`

### 8. Tests à prévoir
- Lancer une attaque
- Affichage du dégât au bon endroit
- Effet visuel correct et fluide

---

## 14. Musique et sons

### 1. Nom de la fonctionnalité
**Lecture de musique et sons**

### 2. Description fonctionnelle (comportement VB6)
Le client lit une musique de fond et déclenche des sons selon les actions (attaque, clic, pickup, chat…).

### 3. Fichiers et modules VB6 impliqués
- `modSound.bas`
- `modMusic.bas`

### 4. Données manipulées
- Chemins vers les fichiers `.wav`, `.mid`, `.mp3`
- Volume, boucle, canal de lecture

### 5. Dépendances critiques
- Fichier présent localement
- Non-bloquant
- Possibilité de désactiver dans les options

### 6. Problèmes à surveiller (VB6 vs C#)
- API Windows ancienne (PlaySound, MCI)
- Manque de mixage ou de volume par type
- Aucun fallback ou gestion d’erreur

### 7. Plan de conversion vers C#
- `Services/SoundService.cs`
- Utilisation de `System.Media` ou `NAudio`
- Dossiers : `Assets/Music`, `Assets/Sounds`

### 8. Tests à prévoir
- Lecture au lancement
- Sons déclenchés aux bonnes actions
- Volume ajustable

---

## 15. Mini-carte et navigation

### 1. Nom de la fonctionnalité
**Mini-carte / Navigation**

### 2. Description fonctionnelle (comportement VB6)
Une mini-carte est affichée (en haut à droite par ex.) permettant de visualiser la position du joueur dans la carte, parfois les PNJ ou objectifs.

### 3. Fichiers et modules VB6 impliqués
- `frmMirage.frm`
- `modGameLogic.bas`
- `modMap.bas`

### 4. Données manipulées
- Position du joueur
- Limites de la map
- Zoom miniaturisé

### 5. Dépendances critiques
- Projection correcte des coordonnées
- Affichage fluide
- Taille adaptative

### 6. Problèmes à surveiller (VB6 vs C#)
- Réduction manuelle des tiles
- Rendu codé en dur
- Pas de superposition dynamique

### 7. Plan de conversion vers C#
- `Controls/MiniMap.cs`
- `Services/NavigationService.cs`
- Représentation simplifiée de `Map`

### 8. Tests à prévoir
- Suivi du joueur
- Affichage d’une map complète
- Mise à jour temps réel


---

## 16. Commandes de joueur (slash commandes)

### 1. Nom de la fonctionnalité
**Commandes texte (slash)**

### 2. Description fonctionnelle (comportement VB6)
Le joueur peut taper des commandes comme `/who`, `/warp`, `/fps`, `/help` dans le champ de chat. Ces commandes sont interprétées côté client ou envoyées au serveur.

### 3. Fichiers et modules VB6 impliqués
- `modGameLogic.bas`
- `modInput.bas`

### 4. Données manipulées
- Texte saisi
- Variables globales d'état (ex: `ShowFPS`)

### 5. Dépendances critiques
- Traitement rapide de la commande
- Interprétation correcte (paramètres, majuscules)
- Feedback utilisateur

### 6. Problèmes à surveiller (VB6 vs C#)
- Traitement manuel du parsing
- Faible retour utilisateur en cas d’erreur
- Pas de distinction commande locale/serveur

### 7. Plan de conversion vers C#
- `Services/CommandService.cs`
- Dictionnaire de commandes
- Messages système en retour

### 8. Tests à prévoir
- `/fps`, `/help`, `/warp`, etc.
- Cas invalide
- Réponses visibles dans le chat

---

## 17. Raccourcis clavier configurables

### 1. Nom de la fonctionnalité
**Raccourcis clavier**

### 2. Description fonctionnelle (comportement VB6)
Le joueur utilise des touches pour ouvrir l’inventaire, afficher le statut, parler, etc. Les raccourcis sont codés en dur dans VB6.

### 3. Fichiers et modules VB6 impliqués
- `modInput.bas`

### 4. Données manipulées
- Code touche
- Variables d’état (menu visible, panel actif)

### 5. Dépendances critiques
- Réactivité
- Conflits possibles avec la saisie texte

### 6. Problèmes à surveiller (VB6 vs C#)
- Difficile à modifier par l’utilisateur
- Aucune persistance

### 7. Plan de conversion vers C#
- `Services/InputService.cs`
- `Config/KeyBindings.cs`
- Persistance JSON ou settings

### 8. Tests à prévoir
- Appui sur les raccourcis
- Réaffectation dans config
- Pas d'interférence avec le chat

---

## 18. Horloge / Heure du jeu

### 1. Nom de la fonctionnalité
**Horloge intégrée**

### 2. Description fonctionnelle (comportement VB6)
Une heure s'affiche dans le HUD, synchronisée avec l’heure réelle ou celle du serveur.

### 3. Fichiers et modules VB6 impliqués
- `frmMirage.frm`
- `modGameLogic.bas`

### 4. Données manipulées
- `Time$`, `Now`
- Texte affiché

### 5. Dépendances critiques
- Synchronisation correcte
- Actualisation fréquente

### 6. Problèmes à surveiller (VB6 vs C#)
- Heure locale ou serveur ambiguë
- Affichage figé sans timer

### 7. Plan de conversion vers C#
- `Controls/ClockLabel.cs`
- `Services/ClockService.cs`

### 8. Tests à prévoir
- Affichage de l’heure
- Rafraîchissement régulier
- Comportement en plein écran

---

## 19. Options de configuration utilisateur

### 1. Nom de la fonctionnalité
**Options utilisateur (configuration)**

### 2. Description fonctionnelle (comportement VB6)
Le joueur peut activer ou désactiver la musique, régler le volume, changer la résolution (si supportée), etc. via un menu d’options.

### 3. Fichiers et modules VB6 impliqués
- `modConstants.bas`
- `modGameLogic.bas`
- Fichier `.ini` (probable)

### 4. Données manipulées
- Volume musique/sons
- Options booléennes

### 5. Dépendances critiques
- Lecture au démarrage
- Sauvegarde des choix

### 6. Problèmes à surveiller (VB6 vs C#)
- Lecture en dur dans `OpenFile`
- Aucune UI dédiée

### 7. Plan de conversion vers C#
- `Forms/OptionsForm.cs`
- `Config/UserSettings.cs`
- Fichier `.json` ou `.ini`

### 8. Tests à prévoir
- Changement d’une option
- Redémarrage avec persistance
- Fallback en cas de fichier manquant

---

## 20. Mode debug / FPS affiché

### 1. Nom de la fonctionnalité
**Affichage FPS (mode debug)**

### 2. Description fonctionnelle (comportement VB6)
Si activé, le client affiche le nombre de frames par seconde. Sert au debug des performances.

### 3. Fichiers et modules VB6 impliqués
- `modGameLogic.bas`

### 4. Données manipulées
- `ShowFPS As Boolean`
- Compteur d’images

### 5. Dépendances critiques
- Précision de la mesure
- Affichage lisible

### 6. Problèmes à surveiller (VB6 vs C#)
- Codé en dur
- Aucun outil de benchmark intégré

### 7. Plan de conversion vers C#
- `Services/PerformanceService.cs`
- `Controls/FpsLabel.cs`

### 8. Tests à prévoir
- Affichage actif/inactif
- Comparaison avec outils externes
- Impact minimal sur perf

