# Phase 10 — Matrice des exigences

Statuts : **présent** (livré et prouvé sur `cursor/phase10-beta-release`) · **incomplet** (code ou preuve partielle) · **absent** · **hors périmètre** · **accepted by owner** (preuve physique acceptée par Marc Giroux le 2026-09-19, **non rejouée** dans cette PR).

Les chemins « code attendu » pour l’absent sont des **cibles**, pas des fichiers déjà créés. Preuve automatisée = test ou artefact nommé + SHA/CI. Preuve propriétaire = acceptation datée, **sans** chiffres inventés.

Légende lots : P10-1 social · P10-2 trade · P10-3 client/éditeur · P10-4 démo/recette · P10-5 sécu · P10-6 paquets · P10-7 ops/restore · P10-8 charge · P10-9 docs candidate.

**Pins CI connus (avant ce commit docs) :** tip `a489379` → [35450339601](https://github.com/Netsuno/MMO_Maker/actions/runs/35450339601) SUCCESS ; produit `814b8ba` → [35449733364](https://github.com/Netsuno/MMO_Maker/actions/runs/35449733364) SUCCESS ; EXE smoke [35403209506](https://github.com/Netsuno/MMO_Maker/actions/runs/35403209506) SUCCESS. Le CI du tip P10-9 n’est **pas** inventé ici (mandat §6).

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
| Invite consentie, distance 3 tuiles, vivants, même carte | `TradeWire` + `WorldMetrics.TradeRangePixels=96` | TCP + PG | **présent** (ce lot) |
| Offre / révision / confirm invalidé | `TradeSnapshot.revision` | changement d’offre | **présent** (ce lot) |
| Vérifs serveur au commit | propriété, qty, or, cap inventaire | cas négatifs | **présent** (ce lot) |
| Pas de double dépense vs shop/banque/sol/équip/craft | `TradeHoldRegistry` à `SetOffer` ; craft replay `requestId` avant le gate holds | concurrence TCP sell réservé + `Phase10TradeLogicTests` craft replay | **présent** (ce lot) |
| Une transaction PG + ledger | `player.trade_executions` + `EconomyRequestId` `trade.commit` | crash injecté, rollback | **présent** (ce lot) |
| Replay `request_id` après commit | même table économie | reconnect / même `request_id` | **présent** (ce lot) |
| Annulation/déco/ban/carte/expire sans effet | `SessionTeardown` + `ITradePresenceSink` | TCP disconnect | **présent** (ce lot) |
| Après commit : biens transférés même si ACK perdu | serveur commit-first | replay commit | **présent** (ce lot) |
| UI confirmation visible | `TradeForm` | `Phase10TradePanelSmokeTests` | **présent** (ce lot) |
| Journal admin sans secret | `trade_executions.contents_json` | scan tests (pas `password`) | **présent** (ce lot) |
| Boutique NPC / banque | `ShopBuy*` `Bank*` Phase 7 | suites existantes | **présent** (≠ P2P) |

---

## P10-3 — Client et éditeur externes

| Exigence | Code attendu / actuel | Test / preuve | Statut |
| --- | --- | --- | --- |
| Parcours login → perso → monde | `MainShellForm` phases Login/Character/Playing | gameplay smoke ×3 | **présent** (from-source) |
| Adresse serveur réglable | `_txtHost` / `_numPort` (défaut 127.0.0.1:6000) | UI | **incomplet** (pas préconfig bêta, pas TLS) |
| Messages indispo / version / auth / sanction / cert | Hello version OK ; cert TLS A ; status joueur FR + `[ui]` | `Phase10ClientSettingsSmokeTests` | **incomplet** (pas tous les libellés certifiés en recette externe) |
| HUD vie, inventaire, banque, chat, quêtes, craft | panneaux Phase 7–8 | smokes + SHA Phase 8 | **présent** (interne) |
| Pas GUID/JSON/dump comme UI normale | GUID boutique `Visible=false` ; craft ComboBox **noms** (Guid caché) ; log technique en bas + diagnostics expurgés | `Phase10ClientSettingsSmokeTests` + Phase8 craft | **incomplet** (log technique encore visible) |
| Interaction NPC/objets/joueurs | `InteractRequest`, mêlée, chat | | **présent** |
| Mort / respawn / reco / perte réseau | `DeathNotify`, `RespawnRequest`, jeton mémoire | | **incomplet** (jeton non OS-protégé ; UI gel ? non certifié externe) |
| Aide intégrée | `HelpForm` FR scrollable, F1 + bouton Aide | `Phase10ClientSettingsSmokeTests` | **présent** (P10-3a) |
| AZERTY/QWERTY ou rebind persisté | `InputService` ZQSD / WASD + flèches ; rebind JSON | `Phase10ClientSettingsSmokeTests` | **présent** (P10-3a) |
| Settings persistés (fenêtre, volume, touches) | `ClientSettingsStore` `%LocalAppData%\Frog\client-settings.json` atomique ; `OptionsForm` | `Phase10ClientSettingsSmokeTests` | **présent** (P10-3a) |
| 1366×768 et 1920×1080, DPI 100/150, accents | smokes actuels non dimensionnés ainsi | | **absent** |
| Version visible + copie diagnostics expurgés | badge `v10.3.0` + « Copier diagnostics » (jamais jeton/mdp) | `Phase10ClientSettingsSmokeTests` | **présent** (P10-3a) |
| Éditeur : ouvrir/créer monde depuis paquet | playtest : même dossier + `../client-win-x64` / `../server-win-x64` ; `LoadPlacementsForMap` hors SyncContext UI | `Phase10PackagedLauncherResolveTests` + `Phase10EditorSyncOverAsyncTests` | **incomplet** (deadlock ouverture **corrigé** ; chemins livrés ; recette humaine paquet non) |
| Import graphismes chemins transportables | tilesets PG + fichiers ; risque chemins dev | | **incomplet** |
| Créer carte, collisions, warps, NPC, objets, dialogue, quête, recette, événement | formulaires Phase 4–8 | editor smoke **87×3** | **présent** (from-source) |
| Save / close / reopen / publish | workspace PG + close coordinator | smokes close | **présent** |
| Erreurs publish liées au contenu | messages workspace | | **incomplet** (pas recette paquet) |
| Playtest depuis binaires **livrés** | `packaged-playtest-e2e.sh` / `.ps1` + `Phase10PackagedPlaytestFromZipTests` | Hello TCP zip hors dépôt ; READY headless + serveur publié (Linux) — CI [35449733364](https://github.com/Netsuno/MMO_Maker/actions/runs/35449733364) / [35450339601](https://github.com/Netsuno/MMO_Maker/actions/runs/35450339601) ; WinForms Linux **not proven** | **incomplet / hors gate** (Hello/READY **oui** ; menu Playtest GUI Linux **non** ; recette 2 PCs = P10-4 acceptée propriétaire) |
| Modification publiée visible joueur | live refresh Phase 8 existe en interne | | **incomplet** |
| Pas d’édition SQL obligatoire pour le contenu | vrai pour cartes/catalogues ; grant GM = SQL | | **incomplet** (ops) |

---

## P10-4 — Monde démo et recette

| Exigence | Code / fixture | Test / preuve | Statut |
| --- | --- | --- | --- |
| 3 cartes, 2 régions, 3 NPC, 2 monstres, 8 objets, 1 métier, 2 recettes, 2 quêtes 5 types | `Phase10DemoWorldCatalog` + publisher PG | `Phase10DemoWorldCatalogTests`, `Phase10DemoWorldPostgresTests` | **présent** (fixture) |
| Licences / crédits assets | `demo-world/LICENSES.md` | tuiles procédurales, pas FRoG | **présent** |
| Réinstall base vierge | `Migrate` + `Frog.DemoWorld publish` | PG isolated empty | **présent** |
| Contenu distinct créé pendant recette | éditeur publié | étape 11 recette 2 PCs | **présent (accepted by owner Marc Giroux on 2026-09-19)** — non rejoué dans cette PR |
| 12 étapes, 2 joueurs, 2 machines, paquets | `BETA_TEST_PLAN` + `Phase10RecipeLoopbackTests` + `run-p10-4-recipe-loopback.sh` | **Automatisé :** loopback 3–8/10–11 CI. **Physique :** 2 / 5-WAN / 9-distant / 12 **DONE / accepted by owner Marc Giroux on 2026-09-19** (pas rejoué ici) | **présent** (loopback CI + acceptation propriétaire) |
| Seeds `Phase7PostgresContentSeed` / smokes | tests seulement | CI | **présent** (≠ monde démo) |

---

## P10-5 — Sécurité externe

| Exigence | Code actuel / attendu | Test / preuve | Statut |
| --- | --- | --- | --- |
| Client TLS + validation cert/chaîne/nom | `SslStream.AuthenticateAsClient` + `TlsCertificateValidator` (pas AcceptAll) | `Phase10TlsTests` expiré / nom / CA inconnue | **lot A PASS** TLS Windows unitaires verts |
| Pas de callback tout-accepter, pas de repli clair | Mode Off\|Required ; pas de fallback silencieux | `Phase10TlsTests` + scan sources | **lot A livré** |
| Terminaison TLS externe OK si client parle TLS | in-process SslStream ; harness P10-8 parle TLS Required | `Phase10LoadHarnessTlsTests` ; proxy externe **absent** | **lot E livré** (proxy hors périmètre) |
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
| Autorisation nouvelles ops + limites tailles | C2/C2b + rate chat/move + `CrossInviteCounters` social/trade | suites Phase 9 + P10-1/P10-2 + restore P10-7 | **incomplet** (taille payloads campagne humaine non) |
| Suites C2/C2b conservées | `Phase9SessionRaceTests`, teardown | CI 454 | **présent** |

---

## P10-6 — Paquets

| Exigence | Actuel | Preuve | Statut |
| --- | --- | --- | --- |
| Client Win x64 autonome | layout `client-win-x64` **self-contained** | Linux : EXE+SHA hors dépôt (`packaged-winforms-layout-proof.sh`) ; lancement **not proven on Linux agents** ; Windows CI `--smoke-launch` [35403209506](https://github.com/Netsuno/MMO_Maker/actions/runs/35403209506) SUCCESS | **incomplet / hors gate Linux GUI** (layout+lancement CI **oui** ; jeu 2 PCs = P10-4 **accepted by owner**) |
| Éditeur Win x64 autonome | idem `editor-win-x64` | idem | **incomplet / hors gate Linux GUI** |
| Serveur Linux x64 + profil | `server-linux-x64` self-contained + `libhostfxr.so` | `PackagedServerPostgreSqlProcessTests` + layout smoke | **présent** |
| Monde démo + ressources dans le paquet | `DEMO_WORLD.md` + `demo-world/LICENSES.md` copiés | | **présent** (fixture docs ; pas un zip de cartes binaires) |
| Manifeste commit / protocole / SHA-256 archives | `packaging-manifest.json` + `archives/SHA256SUMS` | `Phase10PackagingTests` | **présent** (archives locales gitignorées) |
| Serveur Windows | layout produit, lancement **non** revendiqué | | **incomplet** / secondaire |
| Testeur ne compile pas, n’installe pas PG | self-contained + smoke PATH sans SDK | job Windows `--smoke-launch` | **présent** (smoke CI) ; jeu 2 PCs **accepted by owner** (P10-4) |
| Guide install / version / update / uninstall | `PACKAGING_GUIDE.md` + scripts P10-6 | | **incomplet** (pas d’installer / MAJ) |
| Alerte binaire non signé honnête | `PACKAGING_GUIDE.md` SmartScreen | | **présent** (pas de signature) |
| MAJ candidate → candidate + rollback | — | | **absent** |

---

## P10-7 — Exploitation / restore

| Exigence | Actuel | Preuve | Statut |
| --- | --- | --- | --- |
| Start/stop propres | Generic Host ; pas de drain | | **incomplet** |
| Maintenance / stop new conns | `MaintenanceService.cs` TODO | | **absent** |
| Logs rotation / rétention / santé | console + `ops_metrics` fichier optionnel | | **incomplet** |
| Backup schémas+ressources, chiffrement, 7 versions, hors exec dir | scripts `pg_dump -Fc` + runbook ; pas de rétention auto ni chiffrement | `PostgresBackupRestoreTests` | **incomplet** |
| Restore lignes : comptes, persos, inventaires, or, banque, quêtes, métiers, monde, ops, mute/ban, **guildes, amis, échanges** | seed Phase 7 + lignes P10-7 (guild/friends/block/trade/mute/ban) | `Phase10BackupRestoreRowsTests` + [RESTORE_REPORT.md](RESTORE_REPORT.md) | **présent** (démo CI ; pas un dump prod) |
| Serveur publié sur base restaurée | `Frog.Server` publié + login OK / ban rejeté | `Phase10BackupRestoreRowsTests` | **présent** (CI) |
| Replay échange validé après restore | `TryReplayAsync` `trade.commit` sur la base restaurée | `Phase10BackupRestoreRowsTests` | **présent** (CI) |
| Crash pendant mutations | — | | **absent** |
| Volume/durée restore ≤ 30 min démo | non mesuré | | **absent** |

---

## P10-8 — Charge

Deux classes de preuve : **automatisée (CI / harness court)** vs **physique acceptée propriétaire**. Ne pas inventer de latence / TPS / CPU pour le run 60 min. Aucun job CI 3600 s.

| Essai mandat | Preuve | Statut |
| --- | --- | --- |
| 25 joueurs × 60 min, actions réelles, TLS+PG+monde | **Automatisé :** hosted packaged+PG hold **5 000 ms** (`mandateDurationMet=false`) CI [35449733364](https://github.com/Netsuno/MMO_Maker/actions/runs/35449733364) ; in-memory **45 s**. **Physique :** machine dédiée **DONE / accepted by owner Marc Giroux on 2026-09-19** — non rejouée ici, **aucun** chiffre inventé | **présent (accepted by owner Marc Giroux on 2026-09-19)** |
| Latence p95 ≤ 250 ms / p99 ≤ 1 s | RTT heartbeat/move/interact **loopback** campaign courte seulement ([LOAD_REPORT.md](LOAD_REPORT.md)) | **incomplet** (indicatif 5 s / 45 s ; **pas** de p95/p99 dédié inventé) |
| Économie ≥ 10 mut/s × 5 min @ 25 | non mesuré en CI | **absent** (non bloqueur gate après update 2026-09-19) |
| Interact 5/s × 60 s + rafale 20 | interact cadencé campaign courte, pas le palier isolé | **absent** (non bloqueur gate) |
| Idle 300 s | config 300 s existe ; **non mesuré** en CI | **incomplet** (non bloqueur gate) |
| Reconnect 25 &lt; 60 s après restart | non mesuré en CI | **absent** (non bloqueur gate) |
| Connexions PG budget 20 | pool non dumpé en CI | **incomplet** (non bloqueur gate) |
| CPU &lt; 80 %, mémoire bornée | échantillons process **campagne courte seulement** — pas de CPU dédié inventé | **incomplet** (indicatif 5 s / 45 s) |
| 50/100 exploratoire | 100 mixed in-memory historique — **pas** une certif hébergée | **hors périmètre** |
| Générateur décode réponses / états | campaign décode Hello/auth/heartbeat/move/interact/melee/chat + TLS | `Phase10LoadHarnessTlsTests` ; [LOAD_HARNESS_TLS.md](LOAD_HARNESS_TLS.md) | **présent** (harness) |
| Job CI ~90 min dédié | hold 5 s hosted CI (`--profile ci`) ; 60 min **pas** branché | **absent** (volontaire ; acceptation propriétaire ≠ job CI) |

---

## P10-9 — Validation / docs candidate

| Livrable mandat | Statut |
| --- | --- |
| E2E_MATRIX / TEST_RESULTS Phase 10 | **incomplet** — preuves éclatées : playtest zip [P10-3-PACKAGED-PLAYTEST.md](P10-3-PACKAGED-PLAYTEST.md), recette [guides/BETA_TEST_PLAN.md](guides/BETA_TEST_PLAN.md), charge [LOAD_REPORT.md](LOAD_REPORT.md). Pas de matrice E2E unique inventée. |
| LOAD_REPORT / RESTORE_REPORT Phase 10 | **présent** — RESTORE_REPORT P10-7 CI ; LOAD_REPORT = harness court **plus** acceptation propriétaire 25×60 (sans métriques inventées) |
| RELEASE_MANIFEST / checklist candidate | **présent** — [RELEASE_MANIFEST.md](RELEASE_MANIFEST.md) + [CANDIDATE_CHECKLIST.md](CANDIDATE_CHECKLIST.md) ; **pas** un claim de sortie |
| PLAYER_QUICKSTART / CREATOR_QUICKSTART | **présent** (guides P10-9) |
| OPERATIONS / BACKUP_RESTORE Phase 10 | **présent** (guide + runbooks Phase 9 + RESTORE_REPORT P10-7) |
| BETA_TEST_PLAN / BUG_REPORT_TEMPLATE | **présent** — loopback CI + **accepted by owner Marc Giroux on 2026-09-19** pour 2 PCs |
| PHASE_REPORT / REVIEW_REQUEST Phase 10 | **absent** volontairement — docs candidate seulement ; phrase gate **interdite** dans ce lot |
| Capture SHA-256 Phase 8 non affaibli | **présent** (ne pas toucher) |
| `git diff --check` | à tenir à chaque push |
| Phrase de gate | **absent / interdit** dans ce lot — Orchestrator après CI du tip docs exact |

---

## Hors périmètre (ne pas implémenter)

Coffre guilde, HdV, mail objets, guerres, raids, instances, sharding, UDP/AOI, mobile, client macOS/Linux, UGC joueur, scripts arbitraires, import VB6/`.fcc`, boutique réelle, updater auto, MariaDB nouveau, parité FRoG.

---

## Synthèse audit (une ligne)

| Domaine | Statut |
| --- | --- |
| Social (groupes/guildes/amis/blocage) | **P10-1 DONE `dca2185`** (stubs `Guild.cs` non composés) |
| Trade P2P | **P10-2 livré** (84–86, TX PG, replay, holds, block invites) |
| TLS | **lots A+E PASS** (SslStream in-process + LoadHarness Required ; proxy externe absent) |
| PG runtime least-privilege | **lot D livré** (`frog_runtime` DML-only ; compose démo ≠ hébergé) |
| Éditeur publish | **présent** chemins + fixture ; recette 2 PCs **accepted by owner Marc Giroux on 2026-09-19** |
| Paquets | **incomplet / hors gate Linux GUI** (layout Linux + `--smoke-launch` Windows **CI 35403209506**) |
| Restore | **présent CI** (sanctions/social/trade + serveur publié) ; chiffrement/rétention **non** (non bloqueur gate) |
| Load 25×60 | **présent (accepted by owner Marc Giroux on 2026-09-19)** ; harness CI = 5 s / 45 s seulement |
