# Phase 10 — Périmètre de la bêta

Source : mandat [`MANDATE.md`](MANDATE.md) §2 et §5 + update propriétaire **2026-09-19 ~11:21 ET** (Marc Giroux).

Ce fichier distingue **ce que la plateforme garantit**, **le monde de démonstration temporaire**, **le contenu final de Marc**, et **ce qui est différé**.

Paquet **docs candidate** (P10-9). Pas un claim marketing de sortie. Pas de phrase de gate ici.

---

## 0. Preuves 2026-09-19 — ne pas mélanger

| Garantie mandat | Automatisé (dépôt / CI) | Physique |
| --- | --- | --- |
| 25 joueurs × 60 min | Harness hosted packaged+PG **~5 s** + in-memory **~45 s** ([`LOAD_REPORT.md`](LOAD_REPORT.md)). Aucun job CI 60 min. Aucune latence/TPS/CPU inventée pour le dédié. | **DONE / accepted by owner Marc Giroux on 2026-09-19** — non rejouée dans cette PR |
| Recette 2 machines (WAN / éditeur distant / stabilité) | Fixture 3 cartes + loopback `Phase10RecipeLoopbackTests` | **DONE / accepted by owner Marc Giroux on 2026-09-19** — non rejouée dans cette PR |

Client / éditeur = **Windows 11 x64**. Playtest GUI WinForms *not proven on Linux agents* = **hors gate**.

---

## 1. Ce que la bêta doit garantir (plateforme)

À la sortie, un testeur externe et un auteur de confiance doivent pouvoir, **sans SDK, sans Visual Studio, sans dépôt Git, sans accès PostgreSQL joueur** :

| Surface | Garantie | État docs 2026-09-19 |
| --- | --- | --- |
| Client Windows 11 x64 | Paquet autonome ; lancement → inscription autorisée → perso → monde | Layout + `--smoke-launch` Windows CI ; recette humaine 2 PCs **accepted by owner** |
| Éditeur Windows 11 x64 | Paquet autonome ; créer / modifier / publier **sans JSON/SQL manuels** | Chemins paquet + fixture ; éditeur distant **accepted by owner** |
| Serveur Linux x64 | PostgreSQL 16 ; profil documenté | Processus publié CI + hosted load courte |
| Réseau | Deux clients sur machines distinctes rejoignent le même serveur | Loopback CI **plus** recette 2 PCs **accepted by owner** |
| Transport | TLS + certificat serveur **validé** (pas AcceptAll, pas de repli clair) | P10-5 A–E PASS (unitaires + harness Required) |
| Comptes | Inscriptions **fermées** (`ProvisionedOnly`) ; joueur ≠ secret DB | P10-5 C livré |
| Gameplay Phase 7–8 | Cartes, warps, combat, objets, quêtes, métiers, craft, boutique, banque, chat, sanctions | Suites existantes + fixture démo |
| Social | Groupes, guildes, amis, blocage — [`SOCIAL_PROTOCOL_FREEZE.md`](SOCIAL_PROTOCOL_FREEZE.md) | P10-1 DONE `dca2185` |
| Échanges | Trade objets + or, TX unique, replay, journal admin | P10-2 livré |
| Exploitation | Démarrer, arrêter, sauvegarder, restaurer — exécutés | Restore lignes **CI** ; chiffrement/rétention **non** (hors bloqueurs update) |
| Charge | **25** joueurs × 60 min sur l’environnement annoncé | Harness court CI **plus** **accepted by owner Marc Giroux on 2026-09-19** |
| Qualité | Aucun P0/P1 connu sur un parcours obligatoire | Voir [`KNOWN_ISSUES.md`](KNOWN_ISSUES.md) — les deux items physiques ne sont plus des bloqueurs |

Deux profils :

- **Joueur testeur** : reçoit seulement le client.
- **Auteur de confiance** : reçoit l’éditeur + procédure d’accès **protégée** au monde. Jamais le secret DB aux joueurs.

---

## 2. Ce que Grok implémente (fonctions et outils)

Socle mandat §2, **y compris** les outils pour que Marc crée ensuite son contenu :

- Serveur autoritaire, protocole v11, TLS, invitations, OpsCli (créer compte, reset mot de passe, révoquer sessions, grant/revoke GM, sanctions).
- Client : HUD, aide, AZERTY/QWERTY ou rebind persisté, settings, diagnostics expurgés, version `v10.3.0`.
- Éditeur : cartes, collisions, warps, NPC/monstres, objets, dialogue, quête, recette, événement typé ; save / publish ; deadlock ouverture corrigé.
- Scripts d’exploitation, paquets, SHA-256 d’archives, guides.
- Monde de **démonstration temporaire** (section 3).
- Preuves : tests PG, smokes Windows, loopback recette, charge courte, restore CI.

Grok **ne** crée **pas** le jeu final de Marc.

---

## 3. Monde de démonstration temporaire (fixture de recette)

Rôle : prouver la plateforme. **Ce n’est pas** le contenu que Marc distribuera.

**Présent (code + tests) :** [`DEMO_WORLD.md`](DEMO_WORLD.md) — `Phase10DemoWorldCatalog` :

- Trois cartes reliées : Village d'accueil / Faubourgs / Arène.
- Deux régions + transition réelle.
- Trois NPC (Guide, Marchand, Artisan) ; 2 monstres.
- Une classe jouable ; 8 objets ; une profession ; deux recettes.
- Deux quêtes (Talk/Visit + Collect/Kill/Craft).
- Événement réutilisable, condition, dialogue à choix, récompense unique.
- Licences : [`demo-world/LICENSES.md`](demo-world/LICENSES.md) (tuiles procédurales, pas FRoG).
- Réinstallable base vierge : `Migrate` + `Frog.DemoWorld publish`.

Durée 30–60 min humaine + contenu distinct créé pendant recette + 12 étapes / 2 machines : **DONE / accepted by owner Marc Giroux on 2026-09-19** (physique, non rejouée ici). Loopback 1 hôte : **automatisé** (`Phase10RecipeLoopbackTests`).

Les seeds `Phase7PostgresContentSeed` / smokes in-memory **≠** ce monde.

---

## 4. Contenu final Marc (hors implémentation Grok)

Marc créera lui-même, **avec l’éditeur livré** : cartes finales, ambiance, dialogues, quêtes, monstres, objets, recettes, événements.

La Phase 10 échoue si ces outils ne permettent pas de créer, publier, modifier et restaurer **son** contenu.

---

## 5. Différé explicite (mandat §5)

Ne pas implémenter, ne pas afficher comme bouton utilisable vide :

| Différé | Note |
| --- | --- |
| Coffre / banque de guilde partagé | Phase ultérieure |
| Hôtel des ventes | — |
| Courrier avec objets | — |
| Guerres / territoires de guildes | — |
| Raids, instances, sharding | — |
| UDP / AOI | Mesure de charge sur le TCP actuel |
| Application mobile | — |
| Client macOS / Linux | Client + éditeur = Windows 11 x64 seulement |
| Création de contenu par n’importe quel joueur | Auteur de confiance seulement |
| Scripts arbitraires (Lua/C#/…) | Interpréteur de commandes typées Phase 8 |
| Import VB6 / `.fcc` | ADR-0003 |
| Boutique argent réel | — |
| Mise à jour automatique / installateur | Archive portable + guide |
| Serveur Windows | Secondaire ; lancement non revendiqué comme supporté |

Les **25 joueurs**, le TLS, le social, les échanges, la restauration et les paquets **ne sont pas** déplacés en Phase 11.

---

## 6. Acquis Phase 7–9 à préserver

Présents et acceptés, limites dans [`KNOWN_ISSUES.md`](KNOWN_ISSUES.md) :

- Gameplay essentiel ; quêtes / craft / événements (Phase 8).
- Idempotence `activationId` / `EconomyRequestId`.
- Modération persistée ; `SessionTeardown` + courses C2/C2b.
- Publication éditeur PostgreSQL ; fermeture non coopérative.
- Backup/restore schéma Phase 9 + lignes P10-7 CI.
- Packaging serveur Linux prouvé ; client/éditeur self-contained + smoke Windows.

Un fichier existant ≠ fonctionnalité bêta. Voir [`TASK_MATRIX.md`](TASK_MATRIX.md).
