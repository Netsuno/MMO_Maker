# Phase 10 — Matrice des exigences (P10-1)

Statuts : **présent** (livré et prouvé sur `cursor/phase10-beta-release`) · **incomplet** (code ou preuve partielle) · **absent** · **hors périmètre**.

Les chemins « code attendu » pour l’absent sont des **cibles**, pas des fichiers déjà créés. Preuve = test ou artefact nommé, pas un rapport « en attente ».

Légende lots : P10-1 social · P10-2 trade · P10-3 client/éditeur · P10-4 démo/recette · P10-5 sécu · P10-6 paquets · P10-7 ops/restore · P10-8 charge · P10-9 gate.

---

## P10-0 — Audit et plan

| Exigence mandat | Code / doc attendu | Test / preuve | Statut |
| --- | --- | --- | --- |
| Dossier `docs/progress/phase-10-beta-release/` | ce dossier | présence git | **présent** (ce lot) |
| Matrice exigence → code/test/preuve | `TASK_MATRIX.md` | relecture | **présent** (ce lot) |
| Distinguer présent/incomplet/absent/hors périmètre | cette matrice + `KNOWN_ISSUES.md` | audit chemins réels | **présent** (ce lot) |
| Recenser squelettes / boutons morts | `KNOWN_ISSUES.md` §C, `BASELINE_AUDIT.md` | grep TODO / PacketId / UI | **présent** (ce lot) |
| Figer groupes/guildes/échanges **avant** code | `SOCIAL_PROTOCOL_FREEZE.md` | gel v11, opcodes, limites | **présent** (ce lot) |
| Une branche + une PR Draft | `cursor/phase10-beta-release` | PR vers `main` | **présent** (ce lot, après push) |
| Statuts historiques Phase 9 acceptée | README, `docs/STATUS.md`, bandeaux Phase 9 | relecture | **présent** (ce lot) |

---

## P10-1 — Groupes, guildes, relations

| Exigence | Code attendu | Test / preuve | Statut |
| --- | --- | --- | --- |
| Invitation groupe accept/refus/annul/expire 60 s | `SocialWire` + `PacketDispatcher.Social` + UI slash | `Phase10SocialTcpTests`, expire `Phase10SocialLogicTests` | **présent** (ce lot) |
| 5 max, un groupe, un chef | `PartyRoster` | capacité, double accept | **présent** (ce lot) |
| Liste membres, présence, chef, canal privé | `ChatChannel.Party=3`, snapshot | isolation canal TCP | **présent** (ce lot) |
| Quitter / expulser / transfert / dissolve+confirm | actions Party 5–8 | `Phase10SocialLogicTests` | **présent** (ce lot) |
| Reconnexion retrouve le groupe (processus vivant) | registre session + select | reconnect TCP | **présent** (ce lot) |
| Restart serveur dissout les groupes + message | mémoire processus ; snapshot vide + event disband | restart TCP in-memory + PG | **présent** (ce lot) |
| Pas de partage auto butin/XP | doc + absence de code | revue | **hors périmètre** (non annoncé) |
| Guilde : create, nom unique normalisé, invite… | tables `player.guilds*` EF | PG persist + restart | **présent** (ce lot) |
| Une guilde / perso, cap 50 configurable | `Social:GuildMaxMembers` | deux guildes, cap store | **présent** (ce lot) |
| Rôles chef/officier/membre, matrice serveur | `GuildRole` + store | permissions périmées | **présent** (ce lot) |
| MOTD + canal guilde | `ChatChannel.Guild=4` | isolation + mute | **présent** (ce lot) |
| Chef transfère avant départ ; jamais 0/2 chefs | transaction / verrou store | transfert simultané | **présent** (ce lot) |
| Rôle guilde ≠ opérateur | pas d’écriture `auth.operators` | test négatif TCP | **présent** (ce lot) |
| Amis consentis, présence, persist | `player.friendships` | restart PG | **présent** (ce lot) |
| Blocage : whisper + invites sociales/trade | `player.character_blocks` | négatif whisper/invite | **présent** (ce lot ; trade P10-2) |
| Anti-spam invites | compteurs + cooldown gel | flood store / rate `SocialService` | **présent** (ce lot) |
| Mute serveur sur nouveaux canaux | étendre `HandleChatSend` | mute Party TCP | **présent** (ce lot) |
| Squelettes `Guild.cs` / `GuildService.cs` | laisser morts (ADR-0003) | `Phase10SocialWireTests` | **présent** (stubs non livrés) |

---

## P10-2 — Échanges

| Exigence | Code attendu | Test / preuve | Statut |
| --- | --- | --- | --- |
| Invite consentie, distance 3 tuiles, vivants, même carte | `TradeWire` + `WorldMetrics.TradeRangePixels=96` | TCP + PG | **absent** |
| Offre / révision / confirm invalidé | `TradeSnapshot.revision` | changement d’offre | **absent** |
| Vérifs serveur au commit | propriété, qty, or, cap inventaire | cas négatifs | **absent** |
| Pas de double dépense vs shop/banque/sol/équip/craft | réservation | concurrence | **absent** |
| Une transaction PG + ledger | `player.trade_executions` + `EconomyRequestId` | crash injecté, rollback | **absent** |
| Replay `request_id` après commit | même table économie | reconnect replay | **absent** |
| Annulation/déco/ban/carte/expire sans effet | `SessionTeardown` | C2-like | **absent** |
| Après commit : biens transférés même si ACK perdu | serveur commit-first | drop réponse | **absent** |
| UI confirmation visible | panneau client | smoke Windows | **absent** |
| Journal admin sans secret | table + ops query | scan logs | **absent** |
| Boutique NPC / banque | `ShopBuy*` `Bank*` Phase 7 | suites existantes | **présent** (≠ P2P) |

---

## P10-3 — Client et éditeur externes

| Exigence | Code attendu / actuel | Test / preuve | Statut |
| --- | --- | --- | --- |
| Parcours login → perso → monde | `MainShellForm` phases Login/Character/Playing | gameplay smoke ×3 | **présent** (from-source) |
| Adresse serveur réglable | `_txtHost` / `_numPort` (défaut 127.0.0.1:6000) | UI | **incomplet** (pas préconfig bêta, pas TLS) |
| Messages indispo / version / auth / sanction / cert | Hello version OK ; cert **absent** ; sanction login Phase 9 | | **incomplet** |
| HUD vie, inventaire, banque, chat, quêtes, craft | panneaux Phase 7–8 | smokes + SHA Phase 8 | **présent** (interne) |
| Pas GUID/JSON/dump comme UI normale | GUID boutique `Visible=false` encore là ; log technique en bas | | **incomplet** |
| Interaction NPC/objets/joueurs | `InteractRequest`, mêlée, chat | | **présent** |
| Mort / respawn / reco / perte réseau | `DeathNotify`, `RespawnRequest`, jeton mémoire | | **incomplet** (jeton non OS-protégé ; UI gel ? non certifié externe) |
| Aide intégrée | — | | **absent** |
| AZERTY/QWERTY ou rebind persisté | `UserSettings.cs` / `InputService.cs` stubs ; flèches/WASD implicites | | **absent** |
| Settings persistés (fenêtre, volume, touches) | `OptionsForm.cs` stub | | **absent** |
| 1366×768 et 1920×1080, DPI 100/150, accents | smokes actuels non dimensionnés ainsi | | **absent** |
| Version visible + copie diagnostics expurgés | redact token dans log ; pas de bouton | | **absent** |
| Éditeur : ouvrir/créer monde depuis paquet | playtest résout `bin/Debug\|Release` du repo | | **incomplet** |
| Import graphismes chemins transportables | tilesets PG + fichiers ; risque chemins dev | | **incomplet** |
| Créer carte, collisions, warps, NPC, objets, dialogue, quête, recette, événement | formulaires Phase 4–8 | editor smoke **87×3** | **présent** (from-source) |
| Save / close / reopen / publish | workspace PG + close coordinator | smokes close | **présent** |
| Erreurs publish liées au contenu | messages workspace | | **incomplet** (pas recette paquet) |
| Playtest depuis binaires **livrés** | `EditorFrogServerLauncher` chemins SDK | | **absent** (preuve paquet) |
| Modification publiée visible joueur | live refresh Phase 8 existe en interne | | **incomplet** |
| Pas d’édition SQL obligatoire pour le contenu | vrai pour cartes/catalogues ; grant GM = SQL | | **incomplet** (ops) |

---

## P10-4 — Monde démo et recette

| Exigence | Code / fixture | Test / preuve | Statut |
| --- | --- | --- | --- |
| 3 cartes, 2 régions, 3 NPC, 2 monstres, 8 objets, 1 métier, 2 recettes, 2 quêtes 5 types | monde démo versionné | recette humaine 30–60 min **mesurée** | **absent** |
| Licences / crédits assets | dossier licences | revue FRoG | **absent** |
| Réinstall base vierge | publication + restore | | **absent** |
| Contenu distinct créé pendant recette | éditeur publié | étape 11 | **absent** |
| 12 étapes, 2 joueurs, 2 machines, paquets | `BETA_TEST_PLAN` (à écrire P10-9) | pas une boucle locale seule | **absent** |
| Seeds `Phase7PostgresContentSeed` / smokes | tests seulement | CI | **présent** (≠ monde démo) |

---

## P10-5 — Sécurité externe

| Exigence | Code actuel / attendu | Test / preuve | Statut |
| --- | --- | --- | --- |
| Client TLS + validation cert/chaîne/nom | `SslStream.AuthenticateAsClient` + `TlsCertificateValidator` (pas AcceptAll) | `Phase10TlsTests` expiré / nom / CA inconnue | **lot A livré** |
| Pas de callback tout-accepter, pas de repli clair | Mode Off\|Required ; pas de fallback silencieux | `Phase10TlsTests` + scan sources | **lot A livré** |
| Terminaison TLS externe OK si client parle TLS | in-process SslStream seulement | | **absent** (lots B–E / proxy hors lot A) |
| Certs dev confinés | tests éphémères temp ; aucun `.pfx`/`.pem` prod dans Git | `Phase10TlsTests.CommittedAppsettings_DefaultTlsModeIsOff_NoProductionCerts` | **lot A livré** |
| Pas de secret Git / paquets / captures / logs | placeholders `NOT_A_PRODUCTION_SECRET` ; Local gitignoré | scan P10-9 | **incomplet** (jeton client en RAM) |
| Jetons mémorisés protégés OS | `_storedAuthToken` champ UI | | **absent** |
| Client jamais PG direct | `Frog.Client` ↛ persistence | architecture | **présent** |
| Pas de fallback mémoire si PG down (profil hébergé) | `allowInMemoryFallback=false` défaut ; factory refuse sans PG | `Phase9SecurityGateTests` | **présent** |
| Port PG non exposé aux joueurs | compose expose 5432 en **dev** | doc prod | **incomplet** |
| Pas de superutilisateur runtime | `frog_runtime` NOSUPERUSER DML-only ; compose `frog` = démo ≠ hébergé | `PostgresLeastPrivilegeTests` CREATE/DROP/CREATE ROLE ; [POSTGRES_ROLES.md](POSTGRES_ROLES.md) | **lot D livré** |
| Rate-limit IP normalisée + compte | `AuthRateLimitKey` + `AuthRateLimiter` 8/60s IP+user, 30/60s IP ; login+register+reconnect | `Phase10AuthRateLimitTests` ports distincts ; [AUTH_RATE_LIMIT.md](AUTH_RATE_LIMIT.md) | **lot B livré** |
| Inscriptions invitation / provisionnées | `Registration:Mode` ; bêta `ProvisionedOnly` ; TCP Register refusé | `Phase10ClosedBetaTests` ; [CLOSED_BETA.md](CLOSED_BETA.md) | **lot C livré** (InviteOnly = jalon, pas de jetons) |
| Invitation ≠ rôle GM | create OpsCli n’écrit pas `auth.operators` | `Phase10ClosedBetaTests` + `Phase9SecurityGateTests` | **lot C livré** |
| Outil opérateur comptes / reset / revoke / GM / sanctions | `tools/Frog.OpsCli` + `UpdatePasswordAsync` | `Phase10ClosedBetaTests` | **lot C livré** |
| Autorisation nouvelles ops + limites tailles | C2/C2b + rate chat/move | suites Phase 9 | **incomplet** (social/trade pas là) |
| Suites C2/C2b conservées | `Phase9SessionRaceTests`, teardown | CI 454 | **présent** |

---

## P10-6 — Paquets

| Exigence | Actuel | Preuve | Statut |
| --- | --- | --- | --- |
| Client Win x64 autonome | layout `client-win-x64`, **framework-dependent** | lancement EXE hors dépôt | **incomplet** |
| Éditeur Win x64 autonome | idem `editor-win-x64` | idem | **incomplet** |
| Serveur Linux x64 + profil | `server-linux-x64` + smoke processus | `PackagedServerPostgreSqlProcessTests` | **présent** (runtime .NET 8 **hôte** encore requis) |
| Monde démo + ressources dans le paquet | — | | **absent** |
| Manifeste commit / protocole / SHA-256 archives | `packaging-manifest.json` par layout (SHA git, pas archives) | | **incomplet** |
| Serveur Windows | layout produit, lancement **non** revendiqué | | **incomplet** / secondaire |
| Testeur ne compile pas, n’installe pas PG | faux aujourd’hui (SDK/runtime + from-source smokes) | | **absent** |
| Guide install / version / update / uninstall | `PACKAGING_GUIDE.md` Phase 9 ops | | **incomplet** |
| Alerte binaire non signé honnête | non documentée pour testeurs | | **absent** |
| MAJ candidate → candidate + rollback | — | | **absent** |

---

## P10-7 — Exploitation / restore

| Exigence | Actuel | Preuve | Statut |
| --- | --- | --- | --- |
| Start/stop propres | Generic Host ; pas de drain | | **incomplet** |
| Maintenance / stop new conns | `MaintenanceService.cs` TODO | | **absent** |
| Logs rotation / rétention / santé | console + `ops_metrics` fichier optionnel | | **incomplet** |
| Backup schémas+ressources, chiffrement, 7 versions, hors exec dir | scripts `pg_dump -Fc` + runbook ; pas de rétention auto ni chiffrement | `PostgresBackupRestoreTests` | **incomplet** |
| Restore lignes : comptes, persos, inventaires, or, banque, quêtes, métiers, monde, ops, mute/ban, **guildes, amis, échanges** | seed Phase 7 + compte ; pas sanctions peuplées ; social absent | | **incomplet** |
| Serveur publié sur base restaurée | host de test after restore + login | | **incomplet** (login seulement) |
| Replay échange validé après restore | — | | **absent** |
| Crash pendant mutations | — | | **absent** |
| Volume/durée restore ≤ 30 min démo | non mesuré | | **absent** |

---

## P10-8 — Charge

| Essai mandat | Actuel Phase 9 | Statut |
| --- | --- | --- |
| 25 joueurs × 60 min, actions réelles, TLS+PG+monde | non exécuté (mixed 25 in-memory **14 s**) | **absent** |
| Latence p95 ≤ 250 ms / p99 ≤ 1 s | non mesuré bout-en-bout | **absent** |
| Économie ≥ 10 mut/s × 5 min @ 25 | non certifié | **absent** |
| Interact 5/s × 60 s + rafale 20 | non certifié | **absent** |
| Idle 300 s | config 300 s existe ; **non mesuré** | **incomplet** |
| Reconnect 25 &lt; 60 s après restart | non mesuré | **absent** |
| Connexions PG budget 20 | pool non dumpé ; 4 authed PG | **incomplet** |
| CPU &lt; 80 %, mémoire bornée | ~31 % process à 100 mixed in-memory (autre scénario) | **incomplet** |
| 50/100 exploratoire | 100 mixed in-memory mesuré — **pas** une certif hébergée | **hors périmètre** tant que 25×60 échoue ; ne pas vendre |
| Générateur décode réponses / états | harness compte Hello/select/chat/move | **incomplet** |
| Job CI ~90 min dédié | absent | **absent** |

---

## P10-9 — Validation / docs de sortie

| Livrable mandat | Statut |
| --- | --- |
| E2E_MATRIX / TEST_RESULTS Phase 10 | **absent** |
| LOAD_REPORT / RESTORE_REPORT Phase 10 | **absent** (Phase 9 LOAD_REPORT = historique in-memory) |
| RELEASE_MANIFEST / RELEASE_NOTES | **absent** |
| PLAYER_QUICKSTART / CREATOR_QUICKSTART | **absent** |
| OPERATIONS / BACKUP_RESTORE Phase 10 | **incomplet** (runbooks Phase 9 à étendre) |
| BETA_TEST_PLAN / BUG_REPORT_TEMPLATE | **absent** |
| PHASE_REPORT / REVIEW_REQUEST Phase 10 | **absent** (STATUS dit pas READY) |
| Capture SHA-256 Phase 8 non affaibli | **présent** (ne pas toucher) |
| `git diff --check` | à tenir à chaque push |
| Phrase `PHASE 10 GATE REACHED` | **interdit** tant que §7 du mandat est faux |

---

## Hors périmètre (ne pas implémenter)

Coffre guilde, HdV, mail objets, guerres, raids, instances, sharding, UDP/AOI, mobile, client macOS/Linux, UGC joueur, scripts arbitraires, import VB6/`.fcc`, boutique réelle, updater auto, MariaDB nouveau, parité FRoG.

---

## Synthèse audit (une ligne)

| Domaine | Statut |
| --- | --- |
| Social (groupes/guildes/amis/blocage) | **P10-1 livré** (stubs `Guild.cs` non composés) |
| Trade P2P | **absent** |
| TLS | **lot A livré** (SslStream in-process ; proxy/LoadHarness TLS absents) |
| PG runtime least-privilege | **lot D livré** (`frog_runtime` DML-only ; compose démo ≠ hébergé) |
| Éditeur publish | **incomplet** (from-source oui ; paquet / playtest livré non) |
| Paquets | **incomplet** (serveur Linux oui ; client/éditeur autonomes non) |
| Restore | **incomplet** (schéma + login ; sanctions/social/trade non) |
| Load 25×60 | **absent** (mesures courtes in-memory seulement) |
