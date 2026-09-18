# Phase 10 — Périmètre de la bêta

Source : mandat [`MANDATE.md`](MANDATE.md) §2 et §5. Ce fichier distingue **ce que Grok implémente**, **le monde de démonstration temporaire**, **le contenu final de Marc**, et **ce qui est différé**.

La Phase 10 n’est **pas** prête (P10-0). Rien ci-dessous n’est revendiqué comme livré.

---

## 1. Ce que la bêta doit garantir (plateforme)

À la sortie, un testeur externe et un auteur de confiance doivent pouvoir, **sans SDK, sans Visual Studio, sans dépôt Git, sans accès PostgreSQL joueur** :

| Surface | Garantie |
| --- | --- |
| Client Windows 11 x64 | Paquet autonome ; lancement → inscription autorisée → perso → monde |
| Éditeur Windows 11 x64 | Paquet autonome ; créer / modifier / publier cartes et données **sans JSON/SQL manuels** |
| Serveur Linux x64 | PostgreSQL 16 ; profil documenté ; versions réellement testées |
| Réseau | Deux clients sur machines distinctes rejoignent le même serveur par son adresse |
| Transport | TLS + certificat serveur **validé** (pas de callback « tout accepter », pas de repli silencieux en clair) |
| Comptes | Inscriptions **fermées** (invitation ou provisionnement opérateur) ; joueur ≠ secret DB |
| Gameplay déjà Phase 7–8 | Cartes, warps, collisions, combat, objets, quêtes, métiers, craft, boutique, banque, chat Global/Map/Whisper, mute/kick/ban |
| Social (nouveau) | Groupes, guildes, amis, blocage — règles gelées dans [`SOCIAL_PROTOCOL_FREEZE.md`](SOCIAL_PROTOCOL_FREEZE.md) |
| Échanges (nouveau) | Trade objets + or, transaction unique, replay, journal admin |
| Exploitation | Installer, démarrer, arrêter, sauvegarder, restaurer, mettre à jour — exécutés, pas seulement écrits |
| Charge | **25** joueurs simultanés authentifiés actifs pendant 60 minutes, mesurés sur l’environnement annoncé |
| Qualité | Aucun P0/P1 connu sur un parcours obligatoire |

Deux profils :

- **Joueur testeur** : reçoit seulement le client.
- **Auteur de confiance** : reçoit l’éditeur + procédure d’accès **protégée** au monde. Jamais le secret DB aux joueurs.

---

## 2. Ce que Grok doit implémenter (fonctions et outils)

Tout le socle listé au mandat §2 « bêta de tout », **y compris** les outils pour que Marc crée ensuite son contenu :

- Serveur autoritaire, protocole v11, TLS, invitations, opérateur (créer compte, reset mot de passe **sans le connaître**, révoquer sessions, grant/revoke GM, sanctions).
- Client : HUD compréhensible, aide, touches AZERTY/QWERTY ou rebind persisté, settings persistés, diagnostics expurgés, version visible.
- Éditeur : cartes, collisions, warps, NPC/monstres, objets/boutique, dialogue, quête, recette, événement typé ; enregistrer / publier / playtest depuis **les binaires livrés** ; fermeture non coopérative déjà Phase 9 à préserver.
- Scripts d’exploitation, paquets, manifeste SHA-256, guides joueur / auteur / ops.
- Monde de **démonstration temporaire** (section 3) pour prouver les fonctions.
- Preuves : tests PG, smokes Windows ×3, recette 12 étapes, charge, restore, scan secrets.

Grok **ne** crée **pas** le jeu final de Marc (cartes définitives, ambiance, dialogues, quêtes, monstres, objets, recettes « de sortie »).

---

## 3. Monde de démonstration temporaire (fixture de recette)

Rôle : prouver la plateforme et exécuter les recettes. **Ce n’est pas** le contenu que Marc distribuera.

Contenu minimal (P10-4, encore **absent**) :

- Trois cartes reliées : accueil/village, extérieur, combat final.
- Deux régions visuellement distinctes + transition réelle.
- Trois NPC utiles (accueil/quêtes, marchand, métier/craft) ; ≥ 2 types de monstres.
- Une classe jouable ; ≥ 8 objets nommés ; une profession ; deux recettes.
- Deux quêtes couvrant Talk, Kill, Collect, Visit, Craft par les vrais chemins.
- Un événement réutilisable, une condition, un dialogue à choix, une récompense **une seule fois**.
- Graphismes cohérents **redistribuables** + dossier licences/crédits. Pas d’asset FRoG sans droits.
- Durée 30–60 minutes = **cible de recette humaine**, pas un chiffre inventé dans un rapport.
- Réinstallable dans une base vierge via les chemins supportés (éditeur / publication / restore).

Pendant la recette : un **contenu distinct** créé par l’auteur avec l’éditeur publié doit aussi se publier et se voir côté joueur. Si seuls les seeds de tests (`Phase7PostgresContentSeed`, smokes in-memory) marchent, la bêta **n’est pas** prête.

État actuel : **absent**. Les seeds d’intégration et `Docs/premier-monde.md` ne constituent pas ce monde.

---

## 4. Contenu final Marc (hors implémentation Grok, dans le périmètre outils)

Marc créera lui-même, **avec l’éditeur livré** :

- cartes finales, ambiance, dialogues définitifs ;
- quêtes, monstres, objets, recettes, événements de son monde.

La Phase 10 échoue si ces outils ne permettent pas de créer, publier, modifier et restaurer **son** contenu (indépendamment du monde démo).

Le provisionnement initial d’un opérateur et l’administration d’infra restent des opérations **exploitant**.

---

## 5. Différé explicite (mandat §5)

Ne pas implémenter, ne pas afficher comme bouton utilisable vide :

| Différé | Note |
| --- | --- |
| Coffre / banque de guilde partagé | Phase ultérieure (transactions + permissions propres) |
| Hôtel des ventes | — |
| Courrier avec objets | — |
| Guerres / territoires de guildes | — |
| Raids, instances, sharding | — |
| UDP / AOI | Mesure de charge sur le TCP actuel |
| Application mobile | — |
| Client macOS / Linux | Client + éditeur = Windows 11 x64 seulement |
| Création de contenu par n’importe quel joueur | Auteur de confiance seulement |
| Scripts arbitraires (Lua/C#/…) | Interpréteur de commandes typées déjà Phase 8 |
| Import VB6 / `.fcc` | ADR-0003 |
| Boutique argent réel | — |
| Mise à jour automatique / installateur | Archive portable + guide suffisent |
| Serveur Windows | Secondaire ; s’il est dans la livraison, son lancement doit être testé, pas « présent donc supporté » |

Les **25 joueurs**, le TLS, le social de base, les échanges, la restauration réelle et les paquets **ne peuvent pas** être déplacés en Phase 11 pour obtenir une gate verte.

---

## 6. Acquis Phase 7–9 à préserver (déjà dans le produit, à ne pas casser)

Présents et acceptés, avec les limites listées dans [`KNOWN_ISSUES.md`](KNOWN_ISSUES.md) :

- Gameplay essentiel (inventaire, équipement, sol, boutique, banque, mêlée, mort/respawn).
- Quêtes, dialogues, craft, métiers, événements carte, régions (Phase 8).
- Idempotence `InteractRequest.activationId`, `EconomyRequestId`.
- Modération mute/kick/ban persistée (`ops.account_sanctions`).
- `SessionTeardown` partagé + courses C2/C2b (ban vs login/reconnect).
- Publication éditeur PostgreSQL (cartes + catalogues) ; fermeture non coopérative.
- Backup/restore **de schéma** + login Phase 7 sur base restaurée.
- Packaging **serveur Linux** prouvé ; layouts client/éditeur **produits** mais lancement non prouvé.

Un fichier existant ≠ fonctionnalité bêta. Voir [`TASK_MATRIX.md`](TASK_MATRIX.md).
