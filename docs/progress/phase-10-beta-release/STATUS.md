# Phase 10 — STATUS

**P10-5 A–E PASS sécu.** Deadlock éditeur corrigé. Harness P10-8 `campaign` + playtest zip serveur Hello. Recette 2 PCs et 25×60 min **non clos**. **Pas READY.**

| Item | Valeur |
| --- | --- |
| Branche | `cursor/phase10-beta-release` (unique ; ne pas en ouvrir une seconde) |
| Base / tip `main` | `f74b34cca09dda819fe26747d48ee16d27007dfd` (merge PR #7 Phase 9) |
| Produit Phase 9 accepté | `cab57b94c20f86af2cc61738bdf3307ed9626ef4` |
| CI `main` post-fusion | https://github.com/Netsuno/MMO_Maker/actions/runs/35386572613 **SUCCESS** |
| Mandat | [`MANDATE.md`](MANDATE.md) (texte complet, 2026-09-18) |
| Protocole runtime (cette branche) | **v11** — [`SOCIAL_PROTOCOL_FREEZE.md`](SOCIAL_PROTOCOL_FREEZE.md) opcodes 80–86 |
| P10-5 A | **PASS** TLS Windows unitaires verts — lot [`ea116afa`](https://github.com/Netsuno/MMO_Maker/commit/ea116afae9ee1a84c8d80e08ae9f6f0bf2e7af3b) ; B–E déjà sur la branche |
| P10-1 tip produit | [`dca2185`](https://github.com/Netsuno/MMO_Maker/commit/dca2185dbb80b0414af8f95696a4e0e858e6ff90) — **DONE** |
| Gate Phase 10 | **pas atteinte** — recette 2 machines physiques, P10-8 25×60 **full**, P10-9 candidate |

## Lots

| Lot | Statut |
| --- | --- |
| P10-0 Audit + plan | **FAIT** |
| P10-1 Groupes / guildes / relations | **DONE** tip `dca2185` — CI PR en cours ; pas une gate |
| P10-2 Échanges directs | **LIVRÉ (code + tests)** — opcodes 84–86, TX PG, replay, **block sur invites** |
| P10-3 Client / éditeur externes | **INCOMPLET** — P10-3a livré ; deadlock ouverture **corrigé** ; playtest zip Hello + READY headless hors dépôt (Linux) / Hello + layouts frères (Windows) ; menu Playtest WinForms / 2 PCs **non** |
| P10-4 Monde démo + recette | **INCOMPLET** — fixture 3 cartes **livrée** ; loopback 2 clients **automatisé** (`Phase10RecipeLoopbackTests`) ; étapes 2 / 5-WAN / 9-distant / 12 **exigent 2 machines** |
| P10-5 Sécurité externe (TLS, invitations, NAT) | **PASS sécu** A–E (TLS Windows unitaires verts). Palier 25×60 = P10-8 |
| P10-6 Paquets autonomes | **INCOMPLET** — layout+SHA Linux **et** `--smoke-launch` Windows **CI 35403209506 SUCCESS** ; wine Linux ≠ pass ; 2 PCs **non** |
| P10-7 Exploitation / restore | **INCOMPLET** — dump/restore **lignes** sanctions/guildes/amis/trades + serveur publié **CI** ; chiffrement/rétention/durée 30 min **non** |
| P10-8 Charge 25 joueurs | **INCOMPLET** — harness `campaign` 25×TLS + script hosted packaged+PG ([`LOAD_REPORT.md`](LOAD_REPORT.md)) ; **60 min full non** (profil `dedicated`) |
| P10-9 Validation / candidate | **INCOMPLET** — STATUS/matrice/KNOWN_ISSUES + LOAD_REPORT borné ; phrase gate **interdite** |

## File (déjà landed, ne pas rejouer)

| Lot | Tip | Statut |
| --- | --- | --- |
| P10-5 A TLS SslStream | `ea116afae9ee1a84c8d80e08ae9f6f0bf2e7af3b` | **PASS** TLS Windows unitaires verts |
| P10-2 Échanges 84–86 | `bd8462dba2c00e560ccde61ef30e411d0fd8ee94` | **livré** (block invites inclus) |
| P10-3a Settings / aide / rebind | `4ea44de675642180fc5a39989bd22b9f78eb40f6` | **livré** (fix CI `ab1bec5` ; DialogResult save hors ShowDialog) |
| P10-5 B Rate-limit | `6e73e22141447461c52878e0580dcc72b9841bb5` | **livré** |
| P10-5 C ClosedBeta + OpsCli | `726e037b4e5cd4943c1e076f732385f4d76cc76b` | **livré** |
| P10-5 D PG least-privilege | `8f06cde0cec35fefb3c27817b1e7090562633649` | **livré** |
| P10-5 E LoadHarness TLS | `b58a02a03d269ac1e89cc812638e90badb7b9634` | **livré** |

## Éditeur — deadlock ouverture (sync-over-async)

Cause : `MapEventsPostgreSqlService.LoadPlacementsForMap` (et les autres `Load*` / `Try*` sync) faisaient `_gate.ExecuteAsync(...).ConfigureAwait(false).GetAwaiter().GetResult()` depuis le thread UI WinForms (`Shown` / `OpenMap` → `RefreshMapEventMarkers`). `WaitAsync` complète souvent de façon synchrone : EF reprend alors le SynchronizationContext UI pendant que `GetResult` le bloque → chargement infini.

Correctif : `RunOffUiSyncContext` = `Task.Run(work).GetResult()` autour de tout sync-over-async du service. Tests : `Phase10EditorSyncOverAsyncTests` (scan sources) + `Phase10EditorSyncOverAsyncSmokeTests` (contexte bloquant, pas de Post).

## P10-3a — ce qui est livré

- Aide FR scrollable (`HelpForm`) : F1 + bouton Aide.
- Clavier AZERTY ZQSD+E / QWERTY WASD+E + flèches ; rebind persisté.
- `OptionsForm` : fenêtre, volume, disposition ; JSON atomique `%LocalAppData%\Frog\client-settings.json` (`FROG_CLIENT_SETTINGS_PATH` pour tests). `CommitSave` pose `DialogResult.OK` (PerformClick sans ShowDialog ne sélectionne pas le bouton).
- Badge version **10.3.0** + « Copier diagnostics » expurgé (jamais jeton / mot de passe).
- Status joueur + dual-write `[ui]` vers le log tests.
- Craft : ComboBox **noms** de recettes (`PublishedCatalogWire.recipes` additif, pas de bump de version fil) ; bouton Fabriquer ; Guid hors UI normale.
- Tests : `Phase10ClientSettingsSmokeTests`, `Phase10PublishedCatalogRecipesTests` ; smoke Phase 8 craft adapté.

## P10-5 E — ce qui est livré

- `Frog.LoadHarness` : `SslStream` via `TlsClientAuthenticator`, `Mode=Required` + `TargetHost`, validation stricte, **pas** AcceptAll.
- CA de test confinée (`--tls-ca-path`). Self-host Required exige cert+clé (pas de repli clair).
- Doc P10-8 : [`LOAD_HARNESS_TLS.md`](LOAD_HARNESS_TLS.md). Tests `Phase10LoadHarnessTlsTests`.

## P10-5 D — ce qui est livré

- Runtime hébergé = **`frog_runtime`** `NOSUPERUSER` DML sans DDL (bloquant). Scripts `frog_runtime` / `frog_migrate` / `frog_publish` + reco `frog_ops`.
- `docker-compose` reste superuser `frog` : **démo ≠ hébergé** ([`POSTGRES_ROLES.md`](POSTGRES_ROLES.md)).
- Tests : `PostgresLeastPrivilegeTests` (CREATE/DROP/CREATE ROLE refusés) ; `Phase10PostgresRolesTests`.

## P10-5 C — ce qui est livré

- `Registration:Mode=Open|InviteOnly|ProvisionedOnly`. Bêta = **ProvisionedOnly** (TCP Register refusé). Défaut local **Open**.
- **InviteOnly** : jalon explicite — pas de jetons ; TCP refusé comme ProvisionedOnly ([`CLOSED_BETA.md`](CLOSED_BETA.md)).
- `tools/Frog.OpsCli` : create, reset-password (`UpdatePasswordAsync`), session-revoke, operator grant|revoke, sanctions. Create ≠ GM.
- Tests : `Phase10ClosedBetaTests`.

## P10-5 B — ce qui est livré

- Clé `AuthRateLimitKey` : IP normalisée (strip port, unmap v4) + username. Plus de clé IP:port.
- Seuils **8/60s** IP+user, **30/60s** IP. Login, register et reconnect partagent les seaux.
- Doc NAT : [`AUTH_RATE_LIMIT.md`](AUTH_RATE_LIMIT.md). Tests `Phase10AuthRateLimitTests` (ports distincts).

## P10-5 A — ce qui est livré

- **Tip lot A :** [`ea116afa`](https://github.com/Netsuno/MMO_Maker/commit/ea116afae9ee1a84c8d80e08ae9f6f0bf2e7af3b). **PASS** TLS Windows unitaires verts. Pas READY.

- Serveur : `SslStream.AuthenticateAsServer` après accept TCP, **avant** Hello / framing (`GameServerService` + `ClientSession(Stream)`).
- Client : `SslStream.AuthenticateAsClient` après `Connect`, validation stricte (chaîne, nom/SNI, expiration, CA). **Aucun** AcceptAll.
- Flags : `Server:Tls:Mode=Off|Required` (défaut Off), PEM ou PFX + `FROG_TLS_PFX_PASSWORD`, `AllowCleartextLoopback` (loopback explicite seulement). Client : `ClientTlsOptions` / `FROG_CLIENT_TLS_MODE` + `TargetHost`.
- Fail-fast si `Mode=Required` + bind non-loopback sans certificat. Pas de repli clair silencieux.
- Tests : `Phase10TlsTests` (expiré, mauvais nom, CA inconnue, pas de fallback, fail-fast host). `Phase9SecurityGateTests` inchangé.
- Certificats de test éphémères uniquement — aucun cert prod dans Git.

## P10-8 — ce qui est PROUVÉ vs RESTANT

Voir [`LOAD_REPORT.md`](LOAD_REPORT.md). **PROUVÉ** : scénario `campaign` (25 sessions, TLS Required, RTT/TPS/CPU/RAM), script `run-p10-8-load-campaign.sh`, test CI 25×~2,5 s. **RESTANT** : 60 min wall, serveur publié + PG + monde, économie 10 mut/s, idle 300 s, reconnect 25, job CI 90 min.

## P10-3 playtest zip — ce qui est PROUVÉ vs RESTANT

Voir [`P10-3-PACKAGED-PLAYTEST.md`](P10-3-PACKAGED-PLAYTEST.md). **PROUVÉ** : zip serveur hors dépôt → process + Hello (Linux CI) ; script Windows sibling layouts + Hello. **RESTANT** : menu Playtest WinForms, Linux client/éditeur (impossible honnêtement), 2 PCs.

## P10-6 — ce qui est PROUVÉ vs RESTANT

**PROUVÉ (Linux CI, `packaged-winforms-layout-proof.sh`) :**

- Publish self-contained `client-win-x64` + `editor-win-x64`.
- Copie des zip **hors arbre git** (`/tmp/frog-p10-6-outside-*`).
- Présence `Frog.Client.exe` / `Frog.Editor.exe` + `hostfxr.dll` + `packaging-manifest.json` + docs démo.
- SHA-256 des zip = `archives/SHA256SUMS` ; SHA-256 des EXE journalisé.

**PROUVÉ (Windows CI, `packaged-winforms-smoke.ps1`) — [35403209506](https://github.com/Netsuno/MMO_Maker/actions/runs/35403209506) SUCCESS :**

- Extraction hors dépôt (`%TEMP%\frog-p10-6-outside-*`).
- `Frog.Client.exe --smoke-launch` et `Frog.Editor.exe --smoke-launch` exit 0, **PATH sans SDK**.

**RESTANT :**

- Lancement WinForms/WPF **sur Linux** : **non prouvé** (wine optionnel, jamais un pass). Phrase guide : *not proven on Linux agents*.
- SmartScreen / binaire non signé : alerte honnête dans le guide, pas de signature Authenticode.
- Installer / MAJ candidate / 2 PCs distants : P10-4 / P10-9.

`--smoke-launch` affiche le shell puis quitte (client : `Shown`→`Close` ; éditeur : skip workspace PG). Ce n’est **pas** une recette de jeu.

## P10-7 — ce qui est PROUVÉ vs RESTANT

Voir [`RESTORE_REPORT.md`](RESTORE_REPORT.md). **PROUVÉ** en CI : `pg_dump -Fc` d’une base avec sanctions + guildes + amis + trades → restore → `Frog.Server` publié login OK / ban rejeté. **RESTANT** : chiffrement dumps, rétention 7, durée 30 min, crash pendant dump.

## P10-4 — ce qui est livré

- Catalogue déterministe `Phase10DemoWorldCatalog` : 3 cartes (Village / Faubourgs / Arène), 2 régions, 3 NPC, 2 monstres, 8 objets, 1 métier, 2 recettes, 2 quêtes (Talk/Visit + Collect/Kill/Craft).
- Publication via chemins éditeur (`SaveAsync` / `Publish`) : `Phase10DemoWorldPublisher`, `tools/Frog.DemoWorld`, `scripts/publish-demo-world.sh`.
- Licences : [`demo-world/LICENSES.md`](demo-world/LICENSES.md) (tuiles procédurales, pas d’asset FRoG).
- Tests : `Phase10DemoWorldCatalogTests`, `Phase10DemoWorldPostgresTests` (base vierge + replay idempotent).
- Recette 12 étapes / 2 machines / 30–60 min : **non exécutée**. Matrice automate vs 2 PCs : [`guides/BETA_TEST_PLAN.md`](guides/BETA_TEST_PLAN.md).

## P10-2 — ce qui est livré

- Opcodes **84–86** (`TradeRequest` / `TradeResult` / `TradeSnapshot`) ; `WorldMetrics.TradeRangePixels = 96` (3 tuiles).
- `TradeService` : invite consentie 60 s, idle 120 s, max 8 piles + or / côté, révision qui invalide les confirms.
- Réservation à `SetOffer` (`TradeHoldRegistry`) vs boutique / banque / sol / équipement / craft. Replay craft `requestId` **avant** ce gate (retry après consommation).
- Commit : **une** transaction PostgreSQL (`player.trade_executions` + `economy_request_ids` opération `trade.commit`) ; verrou des deux personnages `FOR UPDATE` (ordre Guid).
- Replay `request_id` du commit sans re-transfert. Annulation / déco / ban / carte / expire avant commit = aucun effet économique.
- Crash injecté (`TestBeforeCommitAsync`) → rollback, biens inchangés, retry possible.
- UI : `TradeForm` (noms, or, piles, révision, confirms visibles) + `/trade` ; snapshot = seule source d’affichage.
- Blocage persistant : whisper + invites sociales **et d’échange** (`IsBlockedAsync` dans `TradeService.InviteAsync` ; TCP `Phase10TradeTcpTests`).
- Tests : `Phase10TradeWireTests`, `Phase10TradeLogicTests`, `Phase10TradeTcpTests`, `Phase10TradePostgresTests`, smoke `Phase10TradePanelSmokeTests`.

## P10-1 — ce qui est livré

- Groupes temporaires (invite 60 s, max 5, chef, chat Party, quit/kick/transfer/dissolve, dissolution au redémarrage processus).
- Guildes persistées PostgreSQL (`player.guilds` / `guild_members` / `guild_invites`) : création, rôles chef/officier/membre, capacité 50, MOTD, chat Guild, transfert avant départ.
- Amis + blocage persistants (`player.friendships`, `player.character_blocks`) ; le blocage coupe whisper et invites sociales.
- `PacketDispatcher.Social.cs` unique rédacteur social ; mute serveur appliqué aux canaux Party/Guild via `HandleChatSend`.
- Stubs `Guild.cs` / `GuildService.cs` **non** remplis.
- Tests : `Phase10SocialWireTests`, `Phase10SocialLogicTests`, `Phase10SocialTcpTests`, `Phase10SocialPostgresTests`.

## Interdits (toujours)

Pas de merge. Pas de distribution. Pas de Phase 11. Pas de READY bêta. PacketDispatcher social **non modifié** par P10-5 A–E (B : call sites login/register/reconnect seulement). P10-2 ajoute `PacketDispatcher.Trade.cs` sans réécrire le social.

## Verdict

**P10-5 A–E PASS sécu + P10-2 + P10-6 + P10-7 + deadlock éditeur + harness P10-8 borné (in-memory **et** hosted packaged+PG 5 s) + playtest zip Hello/READY.** La bêta n’est **pas** prête. Restent 25×60 min dédié, recette 2 PCs, playtest menu WinForms. Ne pas écrire `PHASE 10 GATE REACHED`.
