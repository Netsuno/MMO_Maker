# Analyse Fonctionnelle : Serveur de Jeu (Frog.Server)

---

## 1. Traitement réseau (connexion client, paquets)

### 1. Nom de la fonctionnalité
**Traitement réseau : connexions et paquets**

### 2. Description fonctionnelle (comportement VB6)
Le serveur écoute les connexions TCP des clients. Une fois connecté, chaque client est identifié par un index et un socket. Les messages reçus (sérialisés en binaire) sont analysés et dispatchés à des fonctions spécifiques.

Le serveur envoie également des paquets pour notifier les changements (état des entités, messages, erreurs, maps...).

### 3. Fichiers et modules VB6 impliqués
- `modServerLoop.bas`  
- `modServerTCP.bas`  
- `modHandleData.bas`  
- `modConstants.bas`

### 4. Données manipulées
- Sockets TCP (`Socket(Index)`), Buffer
- `IncomingData()`, `Buffer()`  
- Types de paquets définis dans une énumération
- Tables `Player()`, `Map()` mises à jour via les paquets

### 5. Dépendances critiques
- Stabilité de la connexion (écouteurs, threads, temps de réponse)
- Ordre des paquets, synchronisation
- Traitement sécurisé pour éviter injections ou flood

### 6. Problèmes à surveiller (VB6 vs C#)
- Sockets asynchrones nécessaires pour éviter les blocages
- Code VB6 en boucle bloquante + `DoEvents`
- Protocole binaire artisanal → à documenter clairement

### 7. Plan de conversion vers C#
- `Network/ServerSocket.cs` : écoute et acceptation
- `Network/ClientSession.cs` : un objet par client connecté
- `Network/PacketDispatcher.cs` : traitement par opcode
- `Models/PacketType.cs` : enum centralisée
- Utilisation de `TcpListener`, `NetworkStream`, `Memory<byte>`…

### 8. Tests à prévoir
- Connexion de plusieurs clients
- Envoi/réception de paquets corrects
- Déconnexion propre
- Résistance à l’injection ou flood de données

---

## 2. Authentification et gestion des sessions

### 1. Nom de la fonctionnalité
**Connexion, création de compte et sessions**

### 2. Description fonctionnelle (comportement VB6)
Le client envoie un identifiant et mot de passe. Le serveur vérifie via ses fichiers binaires (`accounts.dat` ?), accepte ou refuse la connexion, et charge les personnages associés. Une fois un personnage sélectionné, une session de jeu est créée.

### 3. Fichiers et modules VB6 impliqués
- `modHandleData.bas`  
- `modAccount.bas`  
- `modDatabase.bas`

### 4. Données manipulées
- Liste des comptes
- Tableaux `Player(Index)`, `Account(Index)`
- Statut de session, adresse IP, map

### 5. Dépendances critiques
- Chargement/sauvegarde des comptes
- Correspondance login ↔ personnage
- Protection contre doublons (même login multiple fois)

### 6. Problèmes à surveiller (VB6 vs C#)
- Pas de base SQL → fichiers binaires
- Mot de passe probablement en texte clair
- Aucune expiration/session timeout

### 7. Plan de conversion vers C#
- `Services/AuthService.cs` : vérifie l’identifiant
- `Database/AccountRepository.cs` : accès aux comptes
- `Models/Account.cs`, `Session.cs`
- Mots de passe hashés avec `BCrypt` ou `PBKDF2`

### 8. Tests à prévoir
- Tentative de connexion correcte et incorrecte
- Double connexion même identifiant
- Expiration automatique de session
- Connexion concurrente multi-joueur

---

## 3. Synchronisation et gestion de carte

### 1. Nom de la fonctionnalité
**Chargement et gestion des cartes**

### 2. Description fonctionnelle (comportement VB6)
Chaque carte est chargée depuis un fichier `.map`. Le serveur garde une copie de toutes les maps en mémoire, gère les entrées/sorties des joueurs, les warps, et transmet la carte aux clients qui se connectent.

### 3. Fichiers et modules VB6 impliqués
- `modMap.bas`
- `modHandleData.bas`
- `modDatabase.bas`

### 4. Données manipulées
- Fichiers `.map`
- Table `Map()` (positions, tiles, PNJ, objets…)
- Positions joueurs sur chaque carte

### 5. Dépendances critiques
- Chargement binaire correct
- Synchronisation avec le client
- Mise à jour en temps réel (entités, changements…)

### 6. Problèmes à surveiller (VB6 vs C#)
- Taille fixe des structures binaires
- Mauvaise isolation entre maps
- Fuite mémoire si reload de carte mal géré

### 7. Plan de conversion vers C#
- `Models/Map.cs`, `Tile.cs`, `WarpAttribute.cs`
- `Services/MapService.cs` : chargement, get/set, validation
- `IO/MapSerializer.cs` : lecture binaire `.map`
- Partage via `SendMapData()` sur demande client

### 8. Tests à prévoir
- Chargement carte valide / invalide
- Envoi au client dès l’entrée sur la map
- Consistance entre carte serveur / client
- Warps, téléportations


---

## 4. Gestion des entités (joueurs, PNJ, monstres)

### 1. Nom de la fonctionnalité
**Gestion des entités dynamiques**

### 2. Description fonctionnelle (comportement VB6)
Le serveur gère la position, la direction, l'état et l’animation des entités (joueurs, PNJ, monstres). Il synchronise ces informations avec les clients via des paquets réseau. Les PNJ peuvent avoir des comportements définis : déplacement, attaque, dialogues...

### 3. Fichiers et modules VB6 impliqués
- `modTypes.bas`
- `modHandleData.bas`
- `modNpc.bas`
- `modServerLoop.bas`

### 4. Données manipulées
- `Player()`, `MapNpc()`, `Npc()`
- Coordonnées, direction, sprite
- IA rudimentaire (déplacement aléatoire ou fixe)

### 5. Dépendances critiques
- Synchronisation avec le client
- Actualisation périodique (boucle de jeu)
- Gestion de la mémoire et du nombre d'entités

### 6. Problèmes à surveiller (VB6 vs C#)
- Structures statiques → à convertir en objets
- Boucle de mise à jour manuelle
- Peu ou pas d'encapsulation

### 7. Plan de conversion vers C#
- `Models/Entity.cs`, `Player.cs`, `Npc.cs`
- `Services/EntityManager.cs`
- Mise à jour via `GameLoopService`
- Synchronisation par `PacketSender.cs`

### 8. Tests à prévoir
- Ajout / suppression d'entités
- Mise à jour des positions
- Envoi aux clients
- Apparition/disparition dynamiques

---

## 5. Système de combat

### 1. Nom de la fonctionnalité
**Combat : attaque, dégâts, mort**

### 2. Description fonctionnelle (comportement VB6)
Le serveur traite les attaques des joueurs, calcule les dégâts en fonction des statistiques, met à jour les points de vie, et notifie la mort ou la victoire. Il gère également l’aggro et les retours d’expérience.

### 3. Fichiers et modules VB6 impliqués
- `modCombat.bas`
- `modHandleData.bas`
- `modTypes.bas`

### 4. Données manipulées
- Statistiques : `Strength`, `Defence`, `HP`, `MP`, etc.
- `Player()`, `Npc()`, `MapNpc()`
- Résultat des attaques (touché, raté, critique)

### 5. Dépendances critiques
- Cohérence des calculs entre client et serveur
- Réactivité pour feedback en combat
- Anti-triche sur les dégâts

### 6. Problèmes à surveiller (VB6 vs C#)
- Logique répartie sur plusieurs modules
- Risque de désynchronisation client ↔ serveur
- Aucun système de log de combat

### 7. Plan de conversion vers C#
- `Services/CombatService.cs`
- `Models/Stats.cs`, `AttackResult.cs`
- `PacketSender.SendDamageResult()`

### 8. Tests à prévoir
- Déclenchement d’une attaque
- Calcul de dégâts correct
- Notification aux clients
- Mort et respawn

---

## 6. Commandes administratives

### 1. Nom de la fonctionnalité
**Commandes d’administration (admin)**

### 2. Description fonctionnelle (comportement VB6)
Le serveur autorise certains comptes à exécuter des commandes spéciales : téléportation, attribution d’objets, messages globaux, kick, ban…

### 3. Fichiers et modules VB6 impliqués
- `modHandleData.bas`
- `modServerTCP.bas`
- `modAdmin.bas`

### 4. Données manipulées
- Liste des comptes admins
- Commandes texte (slash)
- État des joueurs connectés

### 5. Dépendances critiques
- Vérification des droits
- Feedback aux utilisateurs
- Commandes loggées ou non

### 6. Problèmes à surveiller (VB6 vs C#)
- Pas de journalisation
- Risque d’erreur silencieuse
- Commandes peu sécurisées

### 7. Plan de conversion vers C#
- `Services/AdminCommandService.cs`
- `Models/AdminCommand.cs`
- Log via `ILogger<>`

### 8. Tests à prévoir
- Utilisation valide/invalide d’une commande
- Portée des effets
- Journalisation
- Feedback clair

---

## 7. Sauvegarde des données de jeu

### 1. Nom de la fonctionnalité
**Persistance : sauvegarde des comptes, maps, objets**

### 2. Description fonctionnelle (comportement VB6)
Le serveur sauvegarde périodiquement l’état du jeu : cartes modifiées, états des joueurs, objets, ressources… Il utilise des fichiers binaires (e.g. `.dat`, `.map`, `.itm`).

### 3. Fichiers et modules VB6 impliqués
- `modDatabase.bas`
- `modFileIO.bas`
- `modTypes.bas`

### 4. Données manipulées
- État mémoire des structures (map, item, joueur…)
- Fichiers binaires sur disque

### 5. Dépendances critiques
- Fichiers bien formatés
- Sauvegarde complète, pas partielle
- Risques de corruption si crash

### 6. Problèmes à surveiller (VB6 vs C#)
- Utilisation de `Put`/`Get` peu portable
- Aucune transaction ou rollback
- Données entassées sans index

### 7. Plan de conversion vers C#
- `IO/BinaryRepository<T>` générique
- `Services/SaveService.cs`, `AutoSaveService.cs`
- Format structuré avec validation
- Sauvegarde sur événement ou intervalle

### 8. Tests à prévoir
- Sauvegarde manuelle / automatique
- Récupération après redémarrage
- Données modifiées persistantes
- Fichiers valides et lisibles



---

## 8. Gestion des objets (items et inventaire)

### 1. Nom de la fonctionnalité
**Inventaire des joueurs et objets**

### 2. Description fonctionnelle (comportement VB6)
Le serveur gère les objets que possèdent les joueurs (consommables, armes, ressources), leur acquisition, suppression, et leurs effets. Il contrôle également les échanges entre joueurs (si activé), et les drops sur la carte.

### 3. Fichiers et modules VB6 impliqués
- `modItem.bas`
- `modTypes.bas`
- `modHandleData.bas`

### 4. Données manipulées
- Tableaux `Item()`, `Inventory()`
- Objets au sol, objets équipés

### 5. Dépendances critiques
- Objets synchronisés avec le client
- Attribution cohérente (pas de duplication)
- Utilisation → effet correct (ex: potion)

### 6. Problèmes à surveiller (VB6 vs C#)
- Peu ou pas de validation côté serveur
- Aucun système de logs des objets
- Structures peu typées (indices bruts)

### 7. Plan de conversion vers C#
- `Models/Item.cs`, `InventorySlot.cs`, `WorldItem.cs`
- `Services/InventoryService.cs`, `ItemService.cs`
- Sérialisation via `ItemSerializer.cs`

### 8. Tests à prévoir
- Ajout d’un objet à l’inventaire
- Utilisation/équipement d’un objet
- Drop au sol et récupération
- Synchronisation avec le client

---

## 9. Système de quêtes et dialogues PNJ

### 1. Nom de la fonctionnalité
**Quêtes et interactions avec les PNJ**

### 2. Description fonctionnelle (comportement VB6)
Le serveur gère les dialogues envoyés aux clients lorsqu’un joueur interagit avec un PNJ. Dans certains cas, cela déclenche une quête, un échange, une téléportation ou une commande serveur.

### 3. Fichiers et modules VB6 impliqués
- `modNpc.bas`
- `modDialog.bas` (si présent)
- `modHandleData.bas`

### 4. Données manipulées
- Texte de dialogue
- Arbre de décision (ou séquences simples)
- État de la quête pour chaque joueur

### 5. Dépendances critiques
- Affichage du bon texte au bon moment
- Déclencheurs d’événements (give item, warp…)
- Suivi des étapes de quête

### 6. Problèmes à surveiller (VB6 vs C#)
- Aucun modèle de quête dédié
- Texte codé en dur
- Aucun état de progression

### 7. Plan de conversion vers C#
- `Models/Quest.cs`, `QuestStep.cs`, `NpcDialog.cs`
- `Services/QuestService.cs`, `DialogService.cs`
- Persistance dans base ou fichiers `.quest`

### 8. Tests à prévoir
- Déclenchement d’un dialogue
- Avancement d’une quête
- Récompense et fin de quête
- Sauvegarde du progrès

---

## 10. Systèmes additionnels (météo, guildes, commerce)

### 1. Nom de la fonctionnalité
**Systèmes optionnels additionnels**

### 2. Description fonctionnelle (comportement VB6)
Selon la version, des fonctionnalités annexes sont implémentées :
- Météo : changement de climat global ou par map
- Guildes : gestion de groupes de joueurs, noms, membres
- Commerce : boutiques PNJ, troc, vente entre joueurs

### 3. Fichiers et modules VB6 impliqués
- `modGuild.bas` (si présent)
- `modShop.bas` ou `modTrade.bas`
- `modWeather.bas`

### 4. Données manipulées
- États météorologiques
- Membres de guilde, permissions
- Inventaire des PNJ marchands

### 5. Dépendances critiques
- Synchronisation avec le client
- Données persistantes et modifiables
- Sécurité des échanges (ex: anti-duplication)

### 6. Problèmes à surveiller (VB6 vs C#)
- Fonctionnalités incomplètes ou non isolées
- Interface manuelle via commandes
- Sauvegarde partielle ou manuelle

### 7. Plan de conversion vers C#
- `Models/WeatherState.cs`, `Guild.cs`, `Shop.cs`
- `Services/WeatherService.cs`, `GuildService.cs`, `ShopService.cs`
- Persistance dédiée par système

### 8. Tests à prévoir
- Changement météo global et local
- Création/suppression de guilde
- Vente/achat avec un PNJ



---

## 11. Boucle principale du serveur (GameLoop)

### 1. Nom de la fonctionnalité
**Boucle principale du serveur**

### 2. Description fonctionnelle (comportement VB6)
Le serveur exécute une boucle de traitement permanente : mise à jour des entités, IA, timers, sauvegardes automatiques, envoi de paquets aux clients.

### 3. Fichiers et modules VB6 impliqués
- `modServerLoop.bas`
- `modGeneral.bas`

### 4. Données manipulées
- Tick global
- États des entités (positions, stats, état)
- Buffers à envoyer aux clients

### 5. Dépendances critiques
- Fréquence d’itération stable
- Ne pas bloquer le thread principal
- Contrôle précis du temps (deltaTime)

### 6. Problèmes à surveiller (VB6 vs C#)
- `DoEvents` obsolète
- Framerate non régulé
- Aucune séparation logique/rendu

### 7. Plan de conversion vers C#
- `Services/GameLoopService.cs`
- Utilisation de `System.Threading.Timer` ou `Task.Run + Delay`
- Injection de services métier (IA, sauvegarde…)

### 8. Tests à prévoir
- Boucle active constante
- Réactivité des mises à jour
- Temps CPU stable

---

## 12. IA de PNJ et monstres

### 1. Nom de la fonctionnalité
**Comportement des PNJ et monstres**

### 2. Description fonctionnelle (comportement VB6)
Certains PNJ ou ennemis se déplacent aléatoirement, poursuivent un joueur ou déclenchent une action. Cela est recalculé périodiquement par le serveur.

### 3. Fichiers et modules VB6 impliqués
- `modNpc.bas`
- `modTypes.bas`

### 4. Données manipulées
- `MapNpc()`, `Npc()`
- Direction, état de déplacement
- Timer d’action

### 5. Dépendances critiques
- Boucle de mise à jour
- Temps d’attente, distance d’agression
- Collision avec les tuiles et joueurs

### 6. Problèmes à surveiller (VB6 vs C#)
- IA très simple (pas de pathfinding)
- Risques de surcharge CPU sur cartes denses

### 7. Plan de conversion vers C#
- `Services/NpcAiService.cs`
- Mise à jour via `GameLoopService`
- Comportements extensibles par type

### 8. Tests à prévoir
- Déplacement autonome
- Poursuite du joueur
- Arrêt au bon moment

---

## 13. Événements planifiés (respawn, annonces…)

### 1. Nom de la fonctionnalité
**Gestion d’événements serveur (timers, respawn)**

### 2. Description fonctionnelle (comportement VB6)
Le serveur peut effectuer des actions programmées : respawn d’entités, annonces à intervalles réguliers, nettoyage de zones, sauvegarde auto.

### 3. Fichiers et modules VB6 impliqués
- `modGeneral.bas`
- `modServerLoop.bas`

### 4. Données manipulées
- Horloge serveur
- Liste d’actions planifiées

### 5. Dépendances critiques
- Temps réel
- Conflits entre événements simultanés

### 6. Problèmes à surveiller (VB6 vs C#)
- Timers artisanaux (calcul via `Now`)
- Actions non annulables

### 7. Plan de conversion vers C#
- `Services/SchedulerService.cs`
- `Models/ScheduledEvent.cs`
- Gestion via `Timer` ou `Task.Delay`

### 8. Tests à prévoir
- Exécution planifiée
- Conflit d’horaire
- Respawn visible côté client

---

## 14. Configuration serveur

### 1. Nom de la fonctionnalité
**Chargement de la configuration serveur**

### 2. Description fonctionnelle (comportement VB6)
Au lancement, le serveur lit un fichier texte (INI ou CFG) contenant les réglages réseau, chemins, port d’écoute, nombre max de joueurs…

### 3. Fichiers et modules VB6 impliqués
- `modConstants.bas`
- `modGeneral.bas`

### 4. Données manipulées
- Port, max joueurs
- Chemins vers assets
- Modes debug ou silencieux

### 5. Dépendances critiques
- Valeurs valides au démarrage
- Persistance entre redémarrages

### 6. Problèmes à surveiller (VB6 vs C#)
- Lecture manuelle via `Line Input`
- Pas de validation
- Chemins codés en dur

### 7. Plan de conversion vers C#
- `Config/ServerConfig.cs`
- Chargement depuis `.json` ou `.yaml` avec `System.Text.Json`
- Validation automatique

### 8. Tests à prévoir
- Chargement correct
- Valeurs par défaut
- Modification sans recompiler

---

## 15. Sécurité et anti-triche

### 1. Nom de la fonctionnalité
**Vérification et validation des actions**

### 2. Description fonctionnelle (comportement VB6)
Le serveur valide chaque action reçue : déplacements, attaques, interactions. Il bloque ou ignore les requêtes incorrectes (téléportation non autorisée, vitesse excessive…).

### 3. Fichiers et modules VB6 impliqués
- `modHandleData.bas`
- `modServerTCP.bas`

### 4. Données manipulées
- Paquets entrants
- Position réelle vs demandée
- Cooldowns et vitesses

### 5. Dépendances critiques
- Contrôle strict pour éviter abus
- Réponse rapide en cas de fraude

### 6. Problèmes à surveiller (VB6 vs C#)
- Peu de logs d’infractions
- Aucune alerte
- Faible modularité du système

### 7. Plan de conversion vers C#
- `Services/SecurityService.cs`
- Règles regroupées et testables
- Journaux via `ILogger<>`

### 8. Tests à prévoir
- Détection de téléportation
- Blocage d’attaque trop rapide
- Journalisation d’anomalies



---

## 16. Journaux et audit du serveur

### 1. Nom de la fonctionnalité
**Système de logs et audit**

### 2. Description fonctionnelle (comportement VB6)
Certaines actions du serveur peuvent être écrites dans des fichiers textes pour audit : erreurs, connexions, commandes admin, actions critiques (téléportation, suppression...).

### 3. Fichiers et modules VB6 impliqués
- `modGeneral.bas`
- `modServerLoop.bas` (éventuellement)

### 4. Données manipulées
- Logs texte (`.txt`, `.log`)
- Horodatage, type d’événement

### 5. Dépendances critiques
- Lisibilité
- Non-bloquant (pas d’erreur si fichier non accessible)

### 6. Problèmes à surveiller (VB6 vs C#)
- Écriture manuelle dans des fichiers
- Pas de rotation automatique

### 7. Plan de conversion vers C#
- `Services/LogService.cs`
- Utilisation de `ILogger<>` avec NLog ou Serilog
- Logs séparés par type : erreurs, gameplay, sécurité…

### 8. Tests à prévoir
- Journalisation automatique des erreurs
- Écriture dans un fichier
- Lecture rétroactive des actions critiques

---

## 17. Reconnexion d’un joueur

### 1. Nom de la fonctionnalité
**Reconnexion rapide et récupération d’état**

### 2. Description fonctionnelle (comportement VB6)
Si un joueur est déconnecté involontairement (perte réseau), il peut se reconnecter rapidement sans perte de progression. Le serveur doit libérer proprement l’ancien slot.

### 3. Fichiers et modules VB6 impliqués
- `modHandleData.bas`
- `modServerTCP.bas`

### 4. Données manipulées
- Statut des connexions
- Sauvegarde temporaire d’état

### 5. Dépendances critiques
- Nettoyage mémoire du joueur déconnecté
- Résistance au flood de reconnexions

### 6. Problèmes à surveiller (VB6 vs C#)
- Pas de distinction entre déconnexion volontaire/involontaire
- Aucune file d’attente ou file de reconnexion

### 7. Plan de conversion vers C#
- `Services/ConnectionManager.cs`
- Timeout sur session
- Réattribution propre si même identifiant

### 8. Tests à prévoir
- Déconnexion réseau simulée
- Reconnexion en moins de 30s
- Position et inventaire conservés

---

## 18. Communication inter-serveur

### 1. Nom de la fonctionnalité
**Interopérabilité entre serveurs (cluster futur)**

### 2. Description fonctionnelle (comportement VB6)
Non présent nativement dans VB6, mais utile à prévoir. Permet le transfert de joueurs ou de données entre plusieurs instances de serveurs.

### 3. Fichiers et modules VB6 impliqués
- Aucun

### 4. Données manipulées
- Position, inventaire, session
- Messages entre processus

### 5. Dépendances critiques
- Protocole clair entre serveurs
- Synchronisation des identifiants

### 6. Problèmes à surveiller (VB6 vs C#)
- Inexistant, tout est monolithique

### 7. Plan de conversion vers C#
- `Services/InterServerMessenger.cs`
- Protocole TCP/UDP ou Redis pub/sub
- À prévoir mais non prioritaire

### 8. Tests à prévoir
- Simulation de transfert inter-map inter-instance
- File d’attente ou buffer

---

## 19. Redémarrage et maintenance

### 1. Nom de la fonctionnalité
**Support de maintenance / arrêt planifié**

### 2. Description fonctionnelle (comportement VB6)
Fonction absente ou manuelle dans VB6. Permet d’envoyer des messages d’arrêt ou de maintenance, sauvegarder proprement puis couper le serveur.

### 3. Fichiers et modules VB6 impliqués
- `modGeneral.bas`
- `modServerLoop.bas`

### 4. Données manipulées
- Message système
- Statut du serveur

### 5. Dépendances critiques
- Sauvegarde propre
- Notification aux clients

### 6. Problèmes à surveiller (VB6 vs C#)
- Pas de fermeture propre
- Aucune UI ou commande spécifique

### 7. Plan de conversion vers C#
- `Services/MaintenanceService.cs`
- Envoi de message global
- Timer de shutdown

### 8. Tests à prévoir
- Message visible sur client
- Fermeture après minuteur
- Données sauvegardées

---

## 20. Rôles et permissions

### 1. Nom de la fonctionnalité
**Système de permissions par rôle**

### 2. Description fonctionnelle (comportement VB6)
Le serveur attribue certains droits selon le type de compte : joueur normal, modérateur, administrateur. Les commandes et actions sensibles sont restreintes.

### 3. Fichiers et modules VB6 impliqués
- `modHandleData.bas`
- `modAccount.bas`

### 4. Données manipulées
- Rôle de l’utilisateur
- Liste des autorisations

### 5. Dépendances critiques
- Validation cohérente
- Séparation claire des niveaux

### 6. Problèmes à surveiller (VB6 vs C#)
- Tout est basé sur “IsAdmin” booléen
- Pas de gestion de rôles multiple

### 7. Plan de conversion vers C#
- `Models/Role.cs`, `Permission.cs`
- `Services/PermissionService.cs`
- Enum `[Player, Mod, Admin]` ou dynamique

### 8. Tests à prévoir
- Accès bloqué selon rôle
- Commandes restreintes
- Affectation en base ou fichier config

