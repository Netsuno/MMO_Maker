# Phase 10 — Plan d’exécution (P10-0)

> **Bandeau 2026-09-19 :** plan d’origine P10-0, conservé. Statut actif = [`STATUS.md`](STATUS.md). P10-4 recette 2 PCs et P10-8 25×60 dédié = **Accepted / skipped by owner agreement — Netsun (2026-09-19); not re-run in this PR.** P10-9 = docs candidate ; la phrase de gate n’est **pas** écrite dans ce lot.

**Branche :** `cursor/phase10-beta-release`
**Base :** `f74b34cca09dda819fe26747d48ee16d27007dfd` (`main`, merge PR #7)
**CI `main` :** https://github.com/Netsuno/MMO_Maker/actions/runs/35386572613 SUCCESS
**Autorité :** [`MANDATE.md`](MANDATE.md) — les nombres et seuils sont des **objectifs**, pas des résultats déjà obtenus.

Ce document est un plan. **Aucune implémentation produit dans P10-0**, hors gel des règles sociales ([`SOCIAL_PROTOCOL_FREEZE.md`](SOCIAL_PROTOCOL_FREEZE.md)).

Phase 9 est **ACCEPTED** sur `main` (tip produit `cab57b9`, CI 35384819869). Les résidus non certifiés de Phase 9 (TLS, paquets client/éditeur, LOAD, restore avec sanctions, P9-S) **ne sont pas soldés** : ils deviennent des critères Phase 10, pas des excuses pour réduire le mandat.

---

## Objectif de sortie (rappel)

Bêta fermée externe, monde persistant unique : client Windows x64 autonome, éditeur Windows x64 autonome, serveur Linux x64 + PostgreSQL 16, TLS avec certificat validé, 25 joueurs simultanés authentifiés **mesurés**, restauration de données réelles, recette à deux machines, preuves et guides. La fusion, l’infra payante et la diffusion aux testeurs restent un GO Netsun.

---

## Lots

| ID | Nom | But | Responsable suggéré |
| --- | --- | --- | --- |
| **P10-0** | Audit + plan | Ce dossier, matrice, gel social, statuts historiques. **Ce lot.** | Orchestrator |
| **P10-1** | Groupes, guildes, relations | Socle social UI + serveur + PG (guildes/amis/blocage) + tests multi-clients. | social / transactions |
| **P10-2** | Échanges directs | Trade objets/or, une transaction PG, réservation/versionnement, replay, journal admin. | social / transactions |
| **P10-3** | Client et éditeur externes | Parcours joueur compréhensible ; éditeur publié crée/publie sans SQL ni JSON obligatoire ; playtest depuis les binaires livrés. | client / éditeur |
| **P10-4** | Monde démo + recette | Fixture 3 cartes / 30–60 min **mesurée** ; contenu distinct créé pendant la recette ; 12 étapes à deux joueurs. | client / éditeur (+ orchestrator recette) |
| **P10-5** | Sécurité externe | TLS client+parcours, invitations, rate-limit IP+compte, outil opérateur, pas de secret dans les artefacts, pas de superutilisateur runtime. | sécurité / distribution / ops |
| **P10-6** | Paquets, install, mise à jour | Archives autonomes (runtime inclus), manifeste SHA-256, lancement `Frog.Client.exe` / `Frog.Editor.exe` hors dépôt. | sécurité / distribution / ops |
| **P10-7** | Exploitation / backup / restore | Drain, logs, `pg_dump` **avec lignes réelles** (sanctions, guildes, amis, échanges), restore + serveur publié, crash test. | sécurité / distribution / ops |
| **P10-8** | Charge et stabilité | 25 joueurs × 60 min sur serveur distribué + PG + TLS + monde publié ; latences ; économie ; palier 50/100 exploratoire seulement. | sécurité / distribution / ops |
| **P10-9** | Validation et candidate | Suites existantes + smokes Phase 10 ×3, captures, rapports, `PHASE 10 GATE REACHED` seulement si les 8 critères de sortie tiennent. | Orchestrator + qa |

P9-S (différé en Phase 9) est **absorbé** par P10-1 et P10-2. Ne pas réutiliser les squelettes `Frog.Server/Models/Guild.cs` / `Services/GuildService.cs` (`// TODO: Implémenter`).

---

## Dépendances et ordre

```text
P10-0 (docs + gel protocole)
  │
  ├──► P10-5  modèle TLS / invitations / comptes opérateur (doc + fondations)
  │      │
  │      └──► peut avancer en parallèle de P10-1 tant que PacketDispatcher
  │           n’a qu’un rédacteur à la fois
  │
  ├──► P10-1  social (protocole v11, opcodes 80–83, schémas player.social_*)
  │      │
  │      └──► P10-2  échanges (opcodes 84–86 ; bloquage P10-1 requis ;
  │                   transactions PG calquées sur EconomyRequestId)
  │
  ├──► P10-3  client/éditeur (UI sociale + trade + TLS + settings + aide)
  │      │     dépend des contrats P10-1/P10-2/P10-5, pas forcément de
  │      │     leur UI finale pour les formulaires monde
  │      │
  │      └──► P10-4  monde démo publié par les chemins supportés
  │
  ├──► P10-6  paquets self-contained (après décisions TLS P10-5 ;
  │           binaires client/éditeur P10-3)
  │
  ├──► P10-7  restore (après migrations P10-1/P10-2 + sanctions Phase 9)
  │
  └──► P10-8  charge (après P10-6 + monde P10-4 + TLS)
                │
                └──► P10-9  gate
```

Règles d’exécution :

1. **Un seul rédacteur** sur `Frog.Server/Network/PacketDispatcher.cs` (et ses `partial`) à un instant T. Social et trade : séquentiel ou fichiers `PacketDispatcher.Social.cs` / `PacketDispatcher.Trade.cs` avec intégration unique.
2. P10-1 **avant** P10-2 pour le blocage (invitations d’échange refusées). Les formats trade sont déjà gelés : P10-2 ne « redécouvre » pas le protocole.
3. P10-5 TLS **avant** tout paquet destiné à un testeur externe. Les tests locaux peuvent rester en certificat de dev, confinés.
4. P10-6 ne marque pas le client « autonome » tant que `--self-contained false` et que le lancement hors dépôt n’est pas prouvé.
5. P10-8 ne commence pas ses mesures officielles sans OS/CPU/RAM/disque/pools **publiés d’abord**.
6. P10-9 est dernier. Aucune phrase de gate sans les 8 critères du mandat §7.
7. Préserve Phases 7–9 : transactions économie, identités de requête (`activationId`, `EconomyRequestId`), `EditorMainFormCloseCoordinator`, courses C2/C2b (`SessionTeardown`, verrou par compte).

---

## Réutilisation (ne pas réécrire)

| Composant existant | Usage Phase 10 |
| --- | --- |
| `ModerationService` + `ops.account_sanctions` | Mute s’applique aux canaux groupe/guilde ; ban coupe trade/social |
| `SessionTeardown` | Déconnexion / kick / ban / idle : annule un échange **avant** commit |
| `IOperatorDirectory` / `auth.operators` | Jamais de pouvoir opérateur via un rôle de guilde |
| `player.economy_request_ids` | Replay du commit d’échange (`operation = trade.commit`) |
| `ChatRateLimiter` | Étendre aux canaux Party/Guild ; pas un second limiteur ad hoc silencieux |
| `LoginRateLimiter` | **Remplacer** la clé `RemoteEndPoint` (IP:port) — P10-5 |
| Publication éditeur PG | Chemins Phase 6/8 ; P10-3/P10-4 les rendent utilisables depuis le paquet |
| `scripts/publish-frog.*` | Base P10-6 ; passer self-contained + preuve de lancement EXE |
| `scripts/postgres-backup.*` / restore | Base P10-7 ; campagne avec **lignes** sanctions/social/trade |
| `tools/Frog.LoadHarness` | Étendre : décodage réponses, TLS, PG, monde publié, 60 min |

---

## Preuves par lot (minimum)

Chaque lot déclare DONE seulement après intégration + tests, pas sur un compte rendu d’agent.

| Lot | Preuve minimale |
| --- | --- |
| P10-0 | Dossier + PR Draft + `git diff --check` propre |
| P10-1 | TCP multi-clients + PG persist guildes/amis/blocage + redémarrage (groupes dissous, guildes conservées) |
| P10-2 | PG runtime : double confirm, replay, concurrence objet, inventaire plein, rollback crash, ledger |
| P10-3 | Smokes Windows sur **paquets** + 1366×768 / 1920×1080 ; playtest depuis `Frog.Editor.exe` publié |
| P10-4 | Recette 12 étapes ; deux machines distinctes **ou** lacune nommée précisément |
| P10-5 | Certificats invalides refusés ; invitations consommées ; brute-force ports distincts ; scan secrets |
| P10-6 | Extraction hors dépôt ; `Frog.Client.exe` / `Frog.Editor.exe` démarrent ; manifeste SHA-256 |
| P10-7 | Restore sur base vide + serveur publié + ban/mute/guildes/échanges rejoués |
| P10-8 | Job borné ~90 min, mêmes SHA que la candidate, 25×60 min |
| P10-9 | CI vert du tip final + artefacts cohérents ; PR reste Draft jusqu’à re-revue Netsun |

---

## Hors périmètre (mandat §5 — ne pas livrer, ne pas dessiner de bouton vide)

Coffre/banque de guilde, hôtel des ventes, courrier objets, guerres/territoires, raids, instances, sharding, UDP/AOI, mobile, client macOS/Linux, UGC joueur, scripts arbitraires, import VB6, boutique argent réel, mise à jour automatique.

Les 50/100 joueurs sont **exploratoires** après un palier 25 réussi. Ne pas les annoncer comme capacité.

---

## Décisions techniques courantes (Orchestrator)

- PostgreSQL SoT ; pas de nouvelle table MariaDB.
- Identités métier = Guid stables (`player.characters.id`, `guild_id`, `trade_id`), jamais le nom affiché.
- Version protocole **11** dès le premier commit produit qui ajoute un opcode social/trade ou un canal Party/Guild.
- Certificats de développement = tests locaux uniquement.
- Un défaut de preuve → lot **incomplet**, pas « vert avec limite ».
