# MMO_Maker — Mandat Phase 10 pour Grok Bot

> Copie de traçabilité (P10-0, 2026-09-18). Texte officiel de Marc. Toute réduction de périmètre ou de preuve est hors autorité de l’agent. Fichier source de travail : ce chemin dans `docs/progress/phase-10-beta-release/`.

## GO Phase 10 — Bêta jouable, distribuable et exploitable

Je donne mon GO pour réaliser la Phase 10 de `Netsuno/MMO_Maker` conformément à ce mandat.

**Objectif : à la fin de cette phase, je dois pouvoir distribuer une bêta à des testeurs externes et exploiter leur monde sans devoir lancer une autre phase de développement indispensable.** Livrer seulement des fonctionnalités sociales ou des tests verts ne suffit pas. Il faut une version candidate concrète, installable, jouable, protégée, récupérable et accompagnée de ses preuves.

Cette autorisation couvre l'implémentation, les tests sur des environnements jetables, les commits, les pushs sur la branche Phase 10 et une seule PR Draft. La fusion, l'ouverture d'un serveur public, une dépense d'infrastructure et la diffusion aux testeurs restent soumises à mon GO explicite. Prépare les éléments nécessaires jusqu'à ce que cette décision soit la dernière étape.

Travaille et communique en français. Continue sans me demander confirmation entre les tâches déjà définies. Une décision technique courante t'appartient ; une réduction du périmètre ou du niveau de preuve ne t'appartient pas.

## 1. Point de départ vérifié

Au moment de la préparation de ce mandat :

- PR #7 : fusionnée dans `main`.
- Commit de fusion Phase 9 : `f74b34cca09dda819fe26747d48ee16d27007dfd`.
- Dernier commit produit accepté : `cab57b94c20f86af2cc61738bdf3307ed9626ef4`.
- CI du produit accepté : https://github.com/Netsuno/MMO_Maker/actions/runs/35384819869 — SUCCESS.
- CI après fusion : https://github.com/Netsuno/MMO_Maker/actions/runs/35386572613 — encore en cours lors de la consultation initiale ; récupérer son état réel au démarrage.
- Référence des tests acceptés : 454 unitaires, 181 PostgreSQL, éditeur 87 × 3, gameplay 6 × 3, Phase 8 24 × 3, manifeste de 12 captures avec vérification SHA-256 exacte.

La feuille de route du README consulté s'arrête à la Phase 9 et contient des statuts historiques périmés. **Ce mandat définit le nouveau périmètre Phase 10.** Les nombres et seuils ci-dessous sont des objectifs à atteindre, pas des résultats déjà obtenus.

Commence par récupérer `main`, lire les instructions applicables du dépôt, confirmer le commit de fusion et consulter le CI après fusion. Vérifie également l'existence éventuelle d'un mandat Phase 10 plus récent avant de créer quoi que ce soit.

Crée ou réutilise une seule branche `cursor/phase10-beta-release` depuis la base validée et une seule PR vers `main`. Ne réutilise pas la PR #7 fusionnée. Aucun force-push, rebase ou réécriture d'historique. Si la base a un échec CI réel, diagnostique-le avant d'empiler des changements ; si elle est simplement en cours, avance sur l'audit et la préparation sans attente infinie.

Mets à jour les statuts historiques actifs pour refléter l'acceptation et la fusion de Phase 9. Conserve les anciens rapports comme historique clairement daté.

## 2. Définition précise de la bêta

La cible est une **bêta fermée externe**, pour un monde persistant unique :

| Dimension | Exigence de sortie |
| --- | --- |
| Joueurs | Au moins 25 joueurs simultanés authentifiés, réellement actifs, sur le serveur PostgreSQL testé |
| Client joueur | Windows 11 x64, paquet autonome utilisable sans SDK, Visual Studio, dépôt Git ou accès PostgreSQL |
| Éditeur | Windows 11 x64, paquet autonome pour les auteurs de confiance ; création et publication utilisables sans modifier du JSON à la main |
| Serveur | Linux x64 avec PostgreSQL 16 ; distribution Linux et versions réellement testées documentées |
| Réseau | Deux clients sur des machines distinctes peuvent rejoindre un même serveur par son adresse réseau et jouer ensemble |
| Transport | Connexion chiffrée et certificat serveur validé sur le parcours externe livré |
| Monde de démonstration | Contenu original ou redistribuable, cohérent, offrant un parcours guidé d'environ 30 à 60 minutes à mesurer pendant la recette |
| Exploitation | Installation, démarrage, arrêt, sauvegarde, restauration et mise à jour documentés et exécutés |
| Qualité | Aucun défaut connu bloquant un parcours obligatoire, aucune perte ou duplication de données, aucune élévation de privilèges connue |
| Livraison | Version candidate, paquets, sommes SHA-256, licences, notes de version, guides et rapports de validation disponibles |

Les 50 et 100 joueurs sont des objectifs exploratoires. Ne revendique pas cette capacité sans mesure. Les 25 joueurs constituent le minimum de cette bêta ; ne réduis pas silencieusement ce seuil si les essais échouent.

Deux profils doivent être utilisables : le **joueur testeur**, qui reçoit seulement le client, et l'**auteur de confiance**, qui reçoit l'éditeur et une procédure d'accès protégée au monde. Un joueur ordinaire ne reçoit jamais un secret de base de données.

### Ce que signifie « bêta de tout »

La bêta doit couvrir **toute la plateforme annoncée** : client joueur, serveur, PostgreSQL, connexion réseau, comptes, personnages, cartes, événements, combats, objets, quêtes, métiers, craft, boutique, banque, groupes, guildes, amis, blocage, échanges, modération, éditeur, publication, sauvegarde, restauration, mise à jour et exploitation. Chaque système inclus dans cette liste doit être réellement utilisable dans le parcours prévu et posséder une preuve correspondante.

Tu ne dois toutefois pas fabriquer à ma place tout le contenu final de mon jeu. **Je créerai moi-même les cartes finales, l'ambiance, les dialogues définitifs, les quêtes finales, les monstres, les objets, les recettes et le reste du contenu créatif.** La Phase 10 doit donc livrer l'éditeur et les outils nécessaires pour que je puisse le faire sans modifier directement la base de données, le JSON ou le code.

Pour valider la plateforme, tu dois fournir un **monde de démonstration temporaire** avec un contenu minimal et redistribuable. Ce monde sert à prouver les fonctions et à exécuter les recettes ; il ne constitue pas le contenu final que je distribuerai aux joueurs. Le mandat doit distinguer clairement :

- les fonctions et outils que Grok doit implémenter ;
- le monde de démonstration créé uniquement pour la validation ;
- le contenu final que je créerai ensuite avec l'éditeur ;
- les limites qui resteraient réellement à traiter avant d'inviter des testeurs.

Une bêta n'est pas prête si les systèmes fonctionnent seulement avec le monde de démonstration mais que l'éditeur ne permet pas de créer, publier, modifier et restaurer mon propre contenu.

## 3. Organisation et règles de travail

L'Orchestrator reste responsable de l'ensemble, des intégrations, des commits, du push, de la PR et du bilan utilisateur.

Si ton environnement permet les sous-agents, utilise au maximum trois agents d'exécution simultanés avec des tâches délimitées : social et transactions ; client et éditeur ; sécurité, distribution et exploitation. Un même fichier partagé, notamment `PacketDispatcher`, ne doit pas avoir deux rédacteurs simultanés. Sinon, exécute ces lots séquentiellement sans prétendre avoir délégué.

Chaque tâche possède un responsable, ses dépendances, ses fichiers, ses tests et une preuve de résultat. Un agent ne déclare pas DONE sur la seule base de son compte rendu : l'intégration et la vérification doivent être terminées. Aucun agent ne crée de branche distante ou de PR supplémentaire et aucun ne fusionne de son initiative.

Privilégie des commits cohérents par lot. Après un problème, relève le test, l'assertion et la cause avant de pousser une correction. Préserve les acquis des Phases 7–9, particulièrement les transactions, les identités de requête, les fermetures de l'éditeur et les courses C2/C2b.

Les fichiers VB6 servent uniquement d'inspiration historique. PostgreSQL reste la source de vérité. Pas de nouvelle dépendance MariaDB, d'import `.fcc`, de parité VB6, d'exécution arbitraire de scripts auteur ou de réécriture globale de l'éditeur.

## 4. Lots à livrer

### P10-0 — Audit bêta et plan d'exécution

Crée `docs/progress/phase-10-beta-release/` et une matrice reliant chaque exigence à son code, à son test et à sa preuve. Distingue présent, incomplet, absent et hors périmètre.

Recense les fonctions visibles qui sont encore des squelettes ou des démonstrations. Un fichier existant ou un bouton dessiné n'est pas une fonctionnalité livrée. Toute fonction obligatoire doit fonctionner dans le parcours produit.

Fige avant développement les règles de groupes, guildes, échanges et invitations, les formats réseau concernés et les limites de capacité. Réutilise les composants opérationnels existants. Toute évolution incompatible du protocole doit avoir une version explicite et un refus compréhensible des anciens clients.

### P10-1 — Groupes, guildes et relations sociales

Livre un socle social utilisable depuis l'interface client, autorisé par le serveur et testé avec plusieurs clients.

**Groupes :**

- Invitation, acceptation, refus, annulation et expiration ; invitation valable 60 secondes par défaut.
- Cinq personnages maximum par groupe, un seul groupe par personnage, un seul chef.
- Liste des membres, état en ligne/hors ligne, identification du chef et canal de discussion privé au groupe.
- Quitter, expulser avec permission, transférer le rôle de chef et dissoudre avec confirmation.
- Une reconnexion retrouve l'appartenance tant que le groupe existe. Les groupes sont temporaires pour cette bêta : un redémarrage serveur les dissout proprement et cette règle est expliquée.
- Les règles existantes de récompenses et de progression individuelles restent explicites. Aucun partage automatique de butin ou d'XP n'est annoncé sans implémentation et tests correspondants.

**Guildes :**

- Création, nom unique normalisé, invitation, acceptation, refus, départ, exclusion, transfert de direction et dissolution.
- Une guilde par personnage ; capacité de 50 membres pour cette bêta, configurable côté serveur.
- Rôles chef, officier et membre, avec une matrice de permissions vérifiée côté serveur.
- Liste des membres, état de présence, message de guilde et canal privé à la guilde.
- Appartenance, rôles et message persistés dans PostgreSQL et conservés après reconnexion et redémarrage.
- Le chef doit transférer la direction avant de quitter une guilde non vide. Le dernier membre peut dissoudre avec confirmation. Aucun état sans chef ou avec deux chefs.
- Un rôle de guilde n'accorde jamais de pouvoir d'opérateur serveur.

**Amis et blocage :**

- Demande d'amitié consentie, acceptation/refus, suppression et présence en ligne ; relation persistante entre personnages.
- Blocage persistant empêchant les messages privés et invitations sociales ou d'échange de l'expéditeur bloqué.
- Aucun spam illimité d'invitations. Limites, expiration, refus et erreurs visibles.

Les canaux groupe/guilde n'envoient les messages qu'aux membres autorisés. Le mute serveur s'applique aussi aux nouveaux canaux. Les demandes falsifiées, permissions périmées et invitations rejouées sont rejetées.

Tests minimum : invitations concurrentes, arrivée à capacité maximale, double acceptation, transfert simultané du chef, membre expulsé pendant une action, permissions invalides, deux guildes distinctes, reconnexion et persistance après redémarrage. Les identités métier sont des identifiants stables, pas les noms affichés.

### P10-2 — Échanges directs entre joueurs

Livre l'échange d'objets et d'or entre deux personnages connectés, vivants et sur la même carte, à une distance maximale explicite, trois tuiles par défaut.

- Invitation consentie, refus, expiration, offre de chaque participant, modification des quantités, confirmation et annulation.
- Les deux joueurs voient exactement la même révision d'offre. Toute modification invalide les confirmations précédentes.
- Le serveur vérifie participants, distance, propriété, quantités, solde, capacité des inventaires et disponibilité réelle des objets au moment de la validation finale.
- Un objet offert ne peut pas être transféré deux fois par une course avec vente, banque, dépôt au sol, équipement, consommation ou craft. Utilise une stratégie cohérente de réservation ou de validation/versionnement au commit.
- L'or, les objets, les deux inventaires et le journal d'exécution sont modifiés dans une seule transaction PostgreSQL.
- Identité stable de l'échange et des requêtes ; un replay après commit renvoie le résultat déjà validé sans refaire le transfert.
- Avant commit, annulation, déconnexion, ban, changement de carte ou expiration rendent l'échange sans effet économique. Après commit, les biens appartiennent à leurs nouveaux propriétaires même si la réponse réseau est perdue.
- Un crash ne laisse ni objet perdu, ni récompense dupliquée, ni réservation définitivement bloquée.
- Confirmation visible avec noms, icônes, quantités et or ; aucune confirmation aveugle sur une ancienne offre.
- Historique administratif des échanges validés, avec participants, identifiant, contenu et date ; aucun secret dans les journaux.

Tests PostgreSQL par le runtime et le protocole : double confirmation, replay après reconnexion, concurrence sur le même objet, inventaire plein, fonds insuffisants, changement d'offre après confirmation, ban/déconnexion, panne injectée après une mutation intermédiaire, rollback complet et nouvelle tentative. Après chaque cas, assert les sommes d'or et quantités chez les deux joueurs et dans le ledger.

### P10-3 — Client et éditeur utilisables par une personne extérieure

**Client joueur :**

- Parcours clair : lancement → connexion/inscription autorisée → choix/création de personnage → entrée dans le monde.
- Adresse serveur correctement préconfigurée ou réglable ; messages explicites pour serveur indisponible, version incompatible, mauvais identifiants, sanction et certificat invalide.
- Vie, ressources, nom, interactions, inventaire, équipement, banque, chat, quêtes, craft et social compréhensibles. Les GUID, JSON, dumps réseau et diagnostics techniques ne constituent pas l'interface normale.
- Sélectionner et interagir avec NPC, objets et autres joueurs sans commande technique cachée.
- Mort, réapparition, reconnexion et perte réseau ont un comportement visible et cohérent. Aucune fenêtre gelée ni double session.
- Commandes expliquées dans une aide intégrée. Déplacement utilisable sur claviers AZERTY et QWERTY ; touches configurables ou dispositions sélectionnables et conservées.
- Paramètres conservés entre lancements : affichage fenêtré/plein écran, taille de fenêtre, commandes et volume si le produit diffuse de l'audio. Aucun réglage factice.
- Interface testée à 1366 × 768 et 1920 × 1080, aux échelles Windows 100 % et 150 %, avec textes longs et noms accentués. Pas de bouton essentiel inaccessible.
- Numéro de version visible, état de connexion et commande de copie des diagnostics expurgés pour signaler un problème.

**Éditeur auteur :**

- Depuis le paquet publié, ouvrir/créer un monde de test et configurer l'accès protégé à PostgreSQL suivant le guide.
- Importer les ressources graphiques nécessaires avec chemins transportables ; aucune référence à un dossier personnel du développeur.
- Créer/modifier une carte, collisions et warps, placer NPC et monstres, configurer objets/boutique, dialogue, quête, recette et événement typé.
- Enregistrer, fermer, rouvrir, publier et retrouver les valeurs éditées. Tester aussi duplication, suppression, contenu invalide et navigation avec modifications non enregistrées.
- Erreurs de publication associées au contenu fautif, références manquantes identifiées, sauvegarde brouillon possible quand elle est prévue.
- Fermer pendant initialisation, sauvegarde et publication sans perte silencieuse, blocage ni destruction de ressources encore utilisées.
- Lancer un playtest depuis l'éditeur publié avec les binaires livrés ; l'arrêter proprement.
- Montrer qu'une modification publiée est chargée par le serveur selon le mécanisme supporté, sans prétendre à du rafraîchissement à chaud si un redémarrage a été nécessaire.

Les fonctions obligatoires ne peuvent pas dépendre d'une édition SQL manuelle. Le provisionnement initial d'un opérateur et l'administration de l'infrastructure restent des opérations réservées à l'exploitant.

### P10-4 — Monde de démonstration et recette complète

Livre un petit monde de démonstration temporaire, créé et publié par les chemins supportés puis réinstallable dans une base vierge. Il sert de fixture de validation et de recette ; il ne prétend pas être mon monde final. Vérifie aussi, avec un contenu distinct créé pendant la recette, que l'auteur peut réellement créer et publier ses propres cartes et données depuis l'éditeur.

Contenu minimal :

- Trois cartes reliées : accueil/village, zone extérieure et zone de combat finale.
- Deux régions avec environnement visuellement distinct et transition réelle pendant le déplacement.
- Trois NPC utiles : accueil/quêtes, marchand et métier/craft ; au moins deux types de monstres.
- Une classe jouable, des équipements et consommables nommés, au moins huit objets au total, une profession et deux recettes utilisables.
- Deux quêtes au minimum, couvrant ensemble Talk, Kill, Collect, Visit et Craft par leurs vrais chemins de gameplay.
- Au moins un événement réutilisable, une condition, un dialogue à choix et une récompense accordée une seule fois.
- Graphismes cohérents et redistribuables ; un dossier de licences/crédits accompagne les ressources. Pas d'asset FRoG repris sans droits établis.

La durée de 30 à 60 minutes est une cible de recette humaine, pas une durée à inventer dans un rapport. Le monde doit montrer une progression et une conclusion compréhensibles. Le rapport doit séparer les défauts du produit des éléments qui relèvent simplement de la création de mon contenu final.

Recette obligatoire avec deux joueurs et les paquets publiés :

1. Installer le serveur et le monde dans un environnement vierge, puis lancer deux clients sur des machines distinctes.
2. Inscrire les comptes selon le mode bêta, créer les personnages et voir l'autre joueur.
3. Utiliser chat global/carte/privé, amis, groupe et guilde ; vérifier l'isolation des canaux.
4. Traverser des warps, constater collisions et changement d'environnement.
5. Parler à un NPC, accepter une quête, combattre, prendre du butin et consommer/équiper un objet.
6. Mourir et réapparaître suivant les règles affichées, sans perte ou duplication anormale.
7. Acheter/vendre, déposer/retirer objets et or à la banque, apprendre un métier et fabriquer.
8. Exécuter les cinq types d'objectif avec assertions des compteurs, rendre les quêtes et vérifier l'unicité des récompenses.
9. Échanger objets et or entre joueurs ; contrôler les deux inventaires.
10. Déconnecter/reconnecter, puis redémarrer le serveur et retrouver les données persistantes.
11. Modifier un élément du monde depuis l'éditeur publié, publier et constater le résultat côté joueur.
12. Appliquer mute/kick/ban, y compris face à une connexion simultanée, puis vérifier leurs effets et la persistance du ban.

Les comptes, objets et personnages de la recette sont préparés avant le scénario par un processus reproductible. Aucun correctif SQL ou appel interne ne remplace une étape annoncée comme jouée par le client.

Une boucle locale automatisée ne prouve pas à elle seule la recette sur machines distinctes. Fournis cette preuve sur un environnement de test autorisé ; si l'accès manque, prépare tout le reste et nomme précisément la vérification encore manquante.

### P10-5 — Sécurité requise pour les testeurs externes

Le TCP en clair accepté comme limite de Phase 9 ne convient pas au parcours externe de cette bêta.

- Le client livré utilise TLS et vérifie le certificat, la chaîne de confiance et le nom du serveur. Aucun callback qui accepte tous les certificats, aucun basculement silencieux en clair.
- Une terminaison TLS externe est acceptable si le client parle réellement TLS, si l'amont en clair reste local/protégé et si les paquets distribués utilisent ce parcours. Un proxy seul devant un client resté en TCP clair ne suffit pas.
- Les certificats de développement sont confinés aux tests locaux. Certificat expiré, nom incorrect et autorité inconnue sont refusés par le client bêta.
- Aucun mot de passe, jeton, chaîne de connexion ou secret dans Git, les paquets, les captures et les journaux remis aux testeurs. Les jetons mémorisés sont protégés par les mécanismes de l'OS ; pas de mot de passe enregistré en clair.
- Le client n'accède jamais directement à PostgreSQL. Le serveur refuse de retomber en mémoire dans le profil hébergé si PostgreSQL échoue.
- L'accès à PostgreSQL et à l'éditeur est privé/réservé aux auteurs ; le profil bêta n'expose pas le port de la base à tous les joueurs.
- Le profil hébergé n'utilise pas un superutilisateur PostgreSQL. Documenter les comptes et privilèges nécessaires à l'exécution, aux migrations et à la publication ; vérifier les droits réels avec le profil livré.
- Corriger la limite d'authentification fondée uniquement sur IP:port : changer de port source ne doit pas réinitialiser la protection. Combiner une IP normalisée et un contrôle par compte, avec fenêtres bornées et comportement documenté pour plusieurs joueurs derrière un NAT.
- Mode bêta fermée : inscriptions contrôlées par invitations ou comptes provisionnés via un outil opérateur documenté. Les invitations sont bornées/à usage contrôlé et aucune ne confère le rôle GM. Pas d'ouverture accidentelle des inscriptions.
- Outil/procédure opérateur pour créer un compte, réinitialiser son mot de passe sans le connaître, révoquer ses sessions, attribuer/retirer le rôle GM et gérer sanctions. Pas de promotion automatique du premier inscrit.
- Autorisation serveur des nouvelles opérations, limites de tailles/fréquences et rejet des paquets malformés. Les suites C2/C2b restent obligatoires.

Tests : accès non autorisé, usurpation d'identifiant, permissions de guilde périmées, invité déjà consommé, jeton révoqué, ban concurrent, brute force avec ports source différents, certificats invalides et absence de secrets dans les artefacts.

### P10-6 — Paquets, installation et mise à jour

Livre au minimum :

- client Windows x64 autonome ;
- éditeur Windows x64 autonome ;
- serveur Linux x64 avec profil de configuration documenté ;
- monde de démonstration et ressources ;
- manifeste de version avec commit source, version protocole, versions des composants et SHA-256 de chaque archive.

Le serveur Windows peut rester secondaire ; s'il est proposé dans la livraison, son lancement doit être testé également. Ne le marque pas pris en charge sur la seule présence d'un dossier publié.

Le test Windows doit extraire les archives dans un dossier extérieur au dépôt et lancer les véritables `Frog.Client.exe` et `Frog.Editor.exe`. Vérifie l'absence de dépendance au SDK, à un chemin du développeur, à des variables de CI ou à un runtime non fourni/non déclaré. Un `dotnet test` qui instancie un formulaire depuis les sorties de compilation ne remplace pas cette preuve.

Le paquet contient les DLL, ressources et runtimes nécessaires, les exemples de configuration sans secret et les licences. Le testeur ordinaire ne compile rien et n'installe pas PostgreSQL. Une archive portable est suffisante ; aucun installateur ou programme de mise à jour automatique n'est requis pour cette bêta.

Le guide couvre téléchargement/extraction, lancement, adresse serveur, connexion, version incompatible, mise à jour, conservation des préférences et désinstallation. Une alerte Windows liée à un binaire non signé doit être déclarée honnêtement ; ne demande pas de désactiver globalement les protections du système.

Teste une mise à jour d'une version candidate vers une autre avec conservation des données et paramètres. Prépare un retour à la version précédente. Pour une migration de données incompatible, le retour utilise une sauvegarde cohérente ; ne prétends pas qu'un simple remplacement d'exécutable suffit.

### P10-7 — Exploitation, sauvegarde et restauration complètes

Les lacunes de restauration Phase 9 doivent être fermées pour les données de la bêta.

- Démarrage et arrêt propres, mode maintenance ou arrêt des nouvelles connexions pendant la préparation d'un arrêt, délai de vidage borné et comportement des actions en cours documenté.
- Logs exploitables avec rotation/rétention, état de santé et compteurs permettant de distinguer serveur arrêté, PostgreSQL inaccessible, saturation et problème client. Un diagnostic doit inclure version et identifiant de corrélation sans secret.
- Sauvegarde des schémas et des ressources nécessaires au monde, avec chiffrement/accès restreint, rétention et copie hors du répertoire d'exécution.
- Profil proposé : sauvegarde quotidienne, sept versions conservées, sauvegarde supplémentaire avant migration. Documenter la perte potentielle depuis la dernière sauvegarde ; ne pas promettre une absence de perte en cas de destruction totale de la base.
- Exécuter `pg_dump` puis restauration sur une autre base vide avec de vraies lignes : comptes, personnages, inventaires, or, banque, quêtes, métiers, monde publié, opérateurs, mute/ban, guildes, amis et échanges validés.
- Démarrer le serveur publié sur cette base restaurée. Vérifier par les parcours publics les connexions, le refus du compte banni, le mute, les appartenances, les soldes et les récompenses déjà accordées.
- Rejouer un identifiant d'échange déjà validé après restauration : aucun nouveau transfert. Un échange non validé avant l'arrêt ne doit pas bloquer les objets après reprise.
- Faire également un arrêt brutal sur une base de test pendant des mutations, puis redémarrer : PostgreSQL conserve les effets committés et annule les autres.
- Mesurer volume sauvegardé et durée de restauration. Cible de récupération du monde de démonstration : au plus 30 minutes avec la procédure fournie sur l'environnement documenté.

La présence des tables ou un test de migration vide ne prouve pas la restauration des données. Ne réutilise pas des données personnelles réelles pour ces essais.

### P10-8 — Charge réelle et stabilité

Ferme les scénarios laissés non mesurés en Phase 9 pour la capacité de bêta choisie. Utilise le serveur distribué, PostgreSQL, le monde publié et le transport chiffré livré.

Fixe et publie avant les mesures : OS, CPU, RAM, disque, localisation du client de charge, latence, configuration, taille des pools et version exacte. Référence de départ : 4 vCPU, 8 Gio RAM et SSD ; tout environnement différent est décrit. Distingue les ressources consommées par le générateur de charge de celles du serveur et de PostgreSQL.

| Essai | Preuve attendue |
| --- | --- |
| 25 joueurs actifs pendant 60 minutes | Connexion, déplacement, combat, dialogue, chat, quêtes, craft, banque et échanges réellement exercés ; aucune erreur inattendue, perte ou duplication |
| Latence sur réseau de test | Actions avec réponse mesurées de bout en bout : p95 ≤ 250 ms et p99 ≤ 1 s, hors ouverture de session et transferts initiaux ; distributions séparées pour les actions lourdes |
| Économie | Au moins 10 mutations/s au total pendant 5 minutes à 25 joueurs ; soldes/quantités vérifiés après charge et replay |
| Interactions | 5 requêtes/s sur un client pendant 60 s, puis rafale de 20 ; exécution correcte ou refus explicite selon limite publiée, sans duplication |
| Inactivité | Session réglée à 300 s sans heartbeat ni action ; expiration constatée dans le délai de nettoyage documenté, sans toucher les autres joueurs actifs |
| Reconnexion | Après redémarrage, les 25 joueurs peuvent revenir en moins de 60 s ; données et appartenance persistante correctes |
| Connexions PostgreSQL | Compter réellement les connexions sur la durée ; budget initial de 20 pour le profil runtime, tous pools cumulés ; mesurer aussi saturation et temps d'attente |
| Ressources | CPU moyen inférieur à 80 % sur le palier stable ; mémoire, sockets et connexions reviennent à un niveau stable après cycles connexion/déconnexion ; aucune croissance sans borne |
| 50/100 joueurs | Essai exploratoire si les 25 passent ; rapport exact des limites rencontrées et aucune annonce commerciale au-delà de la capacité prouvée |

Une révision du budget matériel ou du pool doit être motivée et enregistrée avant une nouvelle campagne, jamais utilisée pour faire passer rétroactivement un résultat échoué. Ne diminue pas les 25 joueurs ou la durée exigée sans mon accord.

Le générateur doit décoder les réponses et contrôler les états finaux. Compter les paquets envoyés, les sockets ouverts ou les seuls Hello ne suffit pas. Rapporte les refus attendus des limites séparément des erreurs et délais inattendus.

Le long essai de 60 minutes utilise un job dédié borné, par exemple 90 minutes maximum avec préparation et artefacts, lié au même commit que la candidate. Il peut être déclenché explicitement dans la CI existante. Les tests rapides ne sont pas remplacés par ce job.

### P10-9 — Validation, documentation et candidate de sortie

Conserve tous les acquis des Phases 7–9. Les nouveaux tests complètent les suites existantes, y compris PostgreSQL réel et UI Windows. Les seuls tests en mémoire ne prouvent pas la persistance.

Avant la re-revue :

- Build Release complet, aucun avertissement du compilateur des projets.
- Tous les tests unitaires et PostgreSQL obligatoires réussis, sans skip de la Phase 10 ni retrait d'un test nécessaire.
- Suites existantes éditeur, gameplay et Phase 8 × 3 ; smokes Phase 10 et des paquets publiés × 3 sur Windows, avec délais bornés.
- Tests de concurrence, erreurs intermédiaires, rollback, replay, reconnexion et redémarrage pour social et échanges.
- Tests négatifs de sécurité et vérification des artefacts sans secrets.
- Captures prises sur les vrais écrans et manifeste vérifié. Une capture modifiée par un changement UI voulu doit être revue et régénérée avec justification ; ne pas affaiblir le contrôle SHA-256 existant.
- Charge, restauration, mise à jour et recette à deux machines exécutées et rapportées.
- Scans des TODO/NotImplementedException/placeholders accessibles depuis les nouveaux parcours. Les anciens squelettes hors parcours restent inventoriés, sans prétendre que tout le dépôt en est exempt.
- `git diff --check`, arbre propre, aucune modification du produit non committée.
- CI vert sur le commit final exact, plus preuves des essais longs et des paquets correspondant à cette même version.

Documente au minimum, dans le dossier Phase 10 ou les emplacements appropriés :

| Livrable | Contenu obligatoire |
| --- | --- |
| PHASE_PLAN / TASK_MATRIX | Lots, responsabilités, dépendances, statuts et liens exigence → preuve |
| BETA_SCOPE / KNOWN_ISSUES | Fonctions garanties, limites explicites, capacité prouvée, problèmes restants et gravité |
| E2E_MATRIX / TEST_RESULTS | Noms des tests, résultats exacts, environnement, commit et artefacts |
| LOAD_REPORT / RESTORE_REPORT | Mesures brutes/résumées, configurations, données contrôlées, résultats de reprise |
| RELEASE_MANIFEST / RELEASE_NOTES | Version, commits, protocole, archives, SHA-256, licences, changements et incompatibilités |
| PLAYER_QUICKSTART | Installer, se connecter, commandes, jouer et transmettre un problème |
| CREATOR_QUICKSTART | Configurer l'éditeur, créer, tester et publier un contenu |
| OPERATIONS / BACKUP_RESTORE | Installer, gérer les accès, observer, sauvegarder, restaurer, mettre à jour et revenir en arrière |
| BETA_TEST_PLAN / BUG_REPORT_TEMPLATE | Scénarios testeurs et rapport avec version, étapes, attendu/observé et logs expurgés |
| PHASE_REPORT / REVIEW_REQUEST | Conclusion honnête, exigences satisfaites et décisions restantes |

Actualise README, STATUS, BACKLOG, DATA_MODEL et la documentation du protocole selon les changements réellement livrés. N'utilise pas un document « en attente » comme preuve de réussite.

## 5. Périmètre différé explicite

Pour maintenir une bêta finissable, cette phase ne promet pas : coffre/banque de guilde partagé, hôtel des ventes, courrier avec objets, guerres/territoires de guildes, raids, instances, sharding, UDP/AOI, application mobile, client macOS/Linux, création de contenu par n'importe quel joueur, scripts arbitraires, import VB6, boutique en argent réel ou mise à jour automatique.

Les groupes, guildes de base, amis/blocage et échanges directs décrits plus haut sont obligatoires. Les fonctions différées ne doivent pas apparaître comme des boutons utilisables mais vides. Un coffre de guilde pourra faire l'objet d'une phase ultérieure avec ses propres transactions et permissions.

Les capacités de bêta obligatoires ne peuvent pas être déplacées vers une phase suivante pour obtenir une gate verte. Si une contrainte rend un critère impossible, indique ce qui fonctionne, ce qui échoue, les preuves et la décision précise nécessaire ; ne déclare pas la bêta prête.

## 6. CI, preuves et prévention des boucles

Chaque commande de build/test a un délai maximal adapté. Sur dépassement, capturer les diagnostics et traiter la cause. Les sorties et dumps utiles sont conservés même en cas d'échec.

Après un push, récupérer un état frais du run associé au SHA exact. Un job obligatoire échoué rend la validation échouée même si les autres tournent encore. Ne multiplie pas les runs identiques sans correction ou cause transitoire identifiée.

Pendant un run, avance sur les travaux indépendants. Lorsqu'il ne reste que le résultat externe, utilise un mécanisme de suivi sur événement s'il existe ; sinon termine le tour avec le SHA, le lien et le statut observé. À chaque reprise, récupérer l'état actuel, jamais réutiliser un ancien « in_progress ». Ne me demande pas de surveiller GitHub à ta place et ne reste pas dans une attente infinie.

Les documents committés peuvent référencer le commit produit stable et ses essais. Le corps de la PR conserve le SHA final exact et le CI après le dernier commit. Aucun nouveau commit uniquement pour écrire son propre SHA ou l'URL de son propre CI.

Si un nouvel essai révèle un défaut, corrige et relance les vérifications affectées, puis la validation finale requise. Une limite documentée ne ferme pas un critère obligatoire non exécuté.

## 7. Décision de sortie

La candidate est prête pour re-revue seulement lorsque :

1. Un testeur externe peut installer et jouer avec le client livré, et un auteur peut modifier/publier avec l'éditeur livré.
2. Les parcours obligatoires fonctionnent avec PostgreSQL, sur les paquets, et sur deux machines distinctes pour la recette réseau.
3. Les groupes, guildes, relations sociales et échanges décrits ici sont implémentés, persistés selon leurs règles et testés.
4. La connexion externe est chiffrée, les droits sont vérifiés côté serveur et les paquets ne contiennent aucun secret.
5. Les 25 joueurs et les essais de stabilité passent sur l'environnement annoncé.
6. La restauration de données réelles de test, le redémarrage et la mise à jour ont réussi.
7. Aucun P0/P1 connu n'affecte un parcours obligatoire : P0 = perte/duplication de données, compromission ou monde inutilisable ; P1 = installation, accès, parcours essentiel ou action annoncée bloquée/crashante. Les défauts mineurs restants ont un contournement et sont publiés.
8. Les artefacts et toutes les preuves portent des identités cohérentes, le CI du tip final est vert et la PR est prête à être évaluée.

Fournis alors le SHA produit, le SHA final, les CI, les comptes de tests, les archives/empreintes, les résultats charge/restauration et la liste exacte des limites. Termine par :

`PHASE 10 GATE REACHED — WAITING FOR RE-REVIEW`

Garde la PR Draft jusqu'à la re-revue. Ne fusionne pas et ne diffuse pas la bêta de ta propre initiative. Après acceptation et mon GO de fusion, vérifier le CI et les artefacts du commit de `main` résultant. Après mon GO de diffusion, la candidate peut être publiée aux testeurs. **Il ne doit rester aucune autre phase technique indispensable entre cette acceptation et la sortie bêta.**

## Références du mandat

- Dépôt : https://github.com/Netsuno/MMO_Maker
- Fusion Phase 9, PR #7 : https://github.com/Netsuno/MMO_Maker/pull/7
- Commit de fusion : https://github.com/Netsuno/MMO_Maker/commit/f74b34cca09dda819fe26747d48ee16d27007dfd
- CI produit Phase 9 accepté : https://github.com/Netsuno/MMO_Maker/actions/runs/35384819869
- CI après fusion à reconsulter : https://github.com/Netsuno/MMO_Maker/actions/runs/35386572613
- README, périmètre historique : https://github.com/Netsuno/MMO_Maker/blob/f74b34cca09dda819fe26747d48ee16d27007dfd/README.md
- Limites Phase 9 : https://github.com/Netsuno/MMO_Maker/blob/f74b34cca09dda819fe26747d48ee16d27007dfd/docs/progress/phase-09-distribution-admin-hardening/KNOWN_ISSUES.md

Les statuts historiques contenus dans le dépôt au commit de fusion ne remplacent pas la décision d'acceptation de Phase 9 ni les critères nouveaux de ce mandat.
