# Phase 10 — KNOWN_ISSUES (P10-0)

Inventaire **honnête** au tip `f74b34cca09dda819fe26747d48ee16d27007dfd`. Un fichier ou un bouton ≠ fonction livrée.

Gravité P10 : **P0** = perte/duplication, compromission, monde inutilisable ; **P1** = installation / accès / parcours essentiel bloqué. Aucune de ces classes n’est « fermée » tant que la candidate n’existe pas.

---

## A. Limites Phase 9 acceptées mais **non certifiées** (reprises)

Source historique : [`../phase-09-distribution-admin-hardening/KNOWN_ISSUES.md`](../phase-09-distribution-admin-hardening/KNOWN_ISSUES.md) (corps antérieur à l’acceptation, conservé). Phase 9 a été **ACCEPTED** le 2026-09-18 malgré ces résidus — ils deviennent des **critères Phase 10**, pas des dettes oubliées.

| Sujet | État réel | Lot |
| --- | --- | --- |
| **TLS** | Lots A+E : `SslStream` in-process + LoadHarness `Mode=Required` (pas AcceptAll). Proxy externe, mTLS, DPAPI : absents. | P10-5 A+E **livrés** |
| **Packaging client/éditeur** | Scripts `publish-frog` self-contained + SHA-256. Layout EXE hors dépôt : `packaged-winforms-layout-proof.sh` (Linux). Lancement process : `packaged-winforms-smoke.ps1` (Windows CI `--smoke-launch`). **not proven on Linux agents.** Smokes Phase 8 = from-source, distincts. Serveur Linux **prouvé**. | P10-6 |
| **Paquet autonome sans SDK** | Runtime bundlé. Smoke Windows PATH sans `dotnet.exe`. Jeu réel 2 PCs **non**. | P10-6 |
| **LOAD** | Harness TLS Required livré (P10-5 E). Mesuré : 200 Hello + 100 mixed **in-memory**. PG authed concurrent = **4**. Non certifiés : idle 300 s, économie 10 mut/s, interact 5/s + rafale 20, restart-reconnect 25 &lt; 60 s, pool PG ≤ 20, palier **25×60 min**, monde publié. | P10-8 |
| **Restore avec sanctions** | `Phase10BackupRestoreRowsTests` : dump avec mute/ban + guildes + amis + trades ; serveur publié refuse le banni. Chiffrement/rétention 7 **non**. | P10-7 |
| **Rate-limit login** | IP normalisée + username (8/60s) et IP (30/60s). Plus de clé IP:port. Voir [`AUTH_RATE_LIMIT.md`](AUTH_RATE_LIMIT.md). | P10-5 B **livré** |
| **Inscriptions ouvertes** | Défaut local `Registration:Mode=Open`. Bêta : `ProvisionedOnly` (TCP refusé). InviteOnly = jalon sans jetons. | P10-5 C **livré** (jalon invites) |
| **Grant opérateur** | `tools/Frog.OpsCli operator grant|revoke` + SQL toujours possible | P10-5 C **livré** |
| **P9-S social** | Groupes/guildes/amis/blocage **P10-1 DONE** tip `dca2185` (v11, 80–83 ; CI PR en cours). Trade P2P **P10-2 livré** (84–86). Stubs `Guild.cs` / `GuildService.cs` toujours morts. | P10-1 + P10-2 |

Autres résidus documentés Phase 9 (non bloquants pour *leur* gate, toujours vrais) :

- `PRD_MMO_Maker_CSharp.md` v2.1 cité, **absent** du dépôt.
- `docs/DATA_MODEL.md` périmé (cartes/tilesets) vs `FrogDbContext` (`auth`, `content`, `ops`, `player`, `world`).
- ~100 fichiers `// TODO: Implémenter` folklore (client `Models/*`, `Services/AuthService.cs` / `ChatService.cs`, `Guild.cs`, `MaintenanceService.cs`, …). P10-3a a remplacé `OptionsForm` / `UserSettings` / `InputService` / `SoundService`. Le gameplay réel passe par `MainShellForm` + `FrogGameClient`.
- CI `concurrency.cancel-in-progress: true` (annulations ≠ preuves vertes).
- Annotation Node 20 / actions v4 (dépréciation, pas un échec de test).

---

## B. Items Phase 10 **absents** (mandat)

| Item | Preuve d’absence | Lot |
| --- | --- | --- |
| Groupes | `PartyRoster` + opcodes 80–83 + canal Party | P10-1 **livré** |
| Guildes persistées | `player.guilds*` ; stubs `Guild.cs` / `GuildService.cs` toujours TODO | P10-1 **livré** (stubs non composés) |
| Amis / blocage | `player.friendships`, `player.character_blocks` | P10-1 **livré** |
| Échanges P2P | `TradeWire` 84–86 + `player.trade_executions` + holds | P10-2 **livré** |
| Client TLS + validation certificat | `FrogGameClient.ConnectAsync` + `TlsClientAuthenticator` (Mode=Required) ; défaut Off | P10-5 A **livré** |
| Invitations / comptes provisionnés | `Registration:Mode=ProvisionedOnly` + OpsCli create ; **InviteOnly sans jetons** (jalon) | P10-5 C **livré** (jalon invites) |
| Outil reset mot de passe / revoke session / grant GM | `tools/Frog.OpsCli` | P10-5 C **livré** |
| Aide intégrée, rebind AZERTY/QWERTY, settings persistés | `HelpForm` + `OptionsForm` + `ClientSettingsStore` (`%LocalAppData%\Frog\client-settings.json`) | P10-3a **livré** |
| Numéro de version visible + copie diagnostics | Badge `v10.3.0` + « Copier diagnostics » expurgé | P10-3a **livré** |
| Monde démo 3 cartes 30–60 min + licences | Catalogue + publisher PG + `LICENSES.md` | P10-4 **fixture livrée** ; durée humaine **non mesurée** |
| Recette 12 étapes / 2 machines | Matrice automate vs 2 PCs dans `BETA_TEST_PLAN` ; campagne **non exécutée** | P10-4 |
| Self-contained + manifeste SHA-256 d’archives | `publish-frog` + `SHA256SUMS` + layout-proof Linux | P10-6 **layout CI** ; lancement EXE = job Windows |
| Restore lignes sociales/trade/sanctions + serveur publié | `Phase10BackupRestoreRowsTests` | P10-7 **CI** ; chiffrement dumps **non** |
| Mode maintenance / drain connexions | `MaintenanceService.cs` stub | P10-7 |
| Rotation/rétention des logs | Console uniquement (`appsettings.json`) | P10-7 |
| Job CI 60 min charge | Absent de `.github/workflows/ci.yml` | P10-8 |
| Guides PLAYER/CREATOR/OPERATIONS Phase 10 | Dossier créé en P10-0 ; guides de sortie **absents** | P10-9 |

---

## C. Squelettes / démos / UI non branchée (inventaire)

### Social / trade

- `Frog.Server/Models/Guild.cs`, `Frog.Server/Services/GuildService.cs` — stubs. **Ne pas les « remplir »** : nouvelles entités PG + services composés.
- Client : slash `/party` `/guild` `/friend` `/block` + canaux Party/Guild dans le combo chat. Pas de panneau Ami/Groupe/Guilde dédié (P10-3). Pas d’échange.

### TLS / réseau

- `Frog.Server/Network/ServerSocket.cs` : `TcpListener` (accept TCP). TLS = `SslStream` post-accept dans `GameServerService` (`TlsServerTransport`).
- `Frog.Client/Network/FrogGameClient.cs` : `Stream` + `TlsClientAuthenticator` si `ClientTlsOptions.Mode=Required`.
- Stubs morts : `NetworkService.cs`, `PacketReader.cs`, `PacketWriter.cs` (TODO).

### Éditeur / publish

- Publication PostgreSQL **réelle** (cartes, catalogues Phase 6, contenu Phase 8). Menu « Publier vers MariaDB… (héritage) » encore visible.
- Playtest éditeur : même dossier que l’éditeur, layouts frères `../client-win-x64` / `../server-win-x64`, puis `bin/Debug|Release` du dépôt.
- Lancement depuis zip hors dépôt : layout **Linux CI** ; process `--smoke-launch` **Windows CI**. Wine Linux ≠ pass.
- `Phase8JsonEditorPanel` n’est plus branché (éditeurs structurés Dialogue/Quête/Recette/Région/CommonEvent/Métier/Météo) — fichier mort, pas une preuve d’édition JSON obligatoire, mais le mandat interdit de **dépendre** du JSON ; rester sur les formulaires.

### Packaging

- `scripts/publish-frog.sh` / `.ps1` : RID self-contained, archives zip + SHA-256.
- Preuve serveur : `packaged-server-smoke.sh` + test processus PG.
- Client/éditeur : layout + SHA-256 hors dépôt **Linux CI** ; process `--smoke-launch` **Windows CI**. Wine ≠ pass.

### Restore / load

- Scripts `postgres-backup` / `restore` / `verify` + `Phase10BackupRestoreRowsTests` (lignes sanctions/social/trade + serveur publié). Chiffrement/rétention **non**.
- `tools/Frog.LoadHarness` : TLS Required + CA confinée (P10-5 E, [`LOAD_HARNESS_TLS.md`](LOAD_HARNESS_TLS.md)). Ne décode pas encore l’économie / le social ; palier 25×60 **non** exécuté.

### PostgreSQL rôles

- Compose `POSTGRES_USER=frog` = superuser **démo locale** seulement. Hébergé : `frog_runtime` (P10-5 D, [`POSTGRES_ROLES.md`](POSTGRES_ROLES.md)).

### Client « démo technique »

`MainShellForm` expose encore des commandes de workshop (hôte/port, « Demander map », GUID boutique en secours `Visible=false`, jeton reconnect en mémoire processus). Craft : ComboBox de noms (Guid caché). Aide / options / version / diagnostics expurgés : **P10-3a livré**. Reste insuffisant pour un testeur externe sur l’éditeur publié et les résolutions 1366×768 (reste P10-3).

---

## D. Acquis à ne pas casser

- Transactions économie Phase 7 + identités de requête Phase 8 (`activationId`, quest/craft `requestId`).
- C2/C2b : `SessionTeardown` idempotent ; ban vs login/reconnect sous verrou compte (`cab57b9`).
- Dispose éditeur / `EditorMainFormCloseCoordinator`.
- Suites : Frog.Tests **454** / PG **181** / éditeur **87×3** / gameplay **6×3** / Phase 8 **24×3** + manifeste 12 SHA-256 (CI produit 35384819869 et post-merge 35386572613).
- PostgreSQL SoT ; MariaDB héritage gelé.

---

## E. Ce que P10-0 ne clôt pas

Aucun P0/P1 de parcours bêta n’est fermé : il n’y a pas encore de parcours bêta. Ne pas déclarer READY. Ne pas fusionner. Ne pas diffuser.
