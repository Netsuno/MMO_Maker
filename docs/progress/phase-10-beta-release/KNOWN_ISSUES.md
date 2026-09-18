# Phase 10 — KNOWN_ISSUES (P10-0)

Inventaire **honnête** au tip `f74b34cca09dda819fe26747d48ee16d27007dfd`. Un fichier ou un bouton ≠ fonction livrée.

Gravité P10 : **P0** = perte/duplication, compromission, monde inutilisable ; **P1** = installation / accès / parcours essentiel bloqué. Aucune de ces classes n’est « fermée » tant que la candidate n’existe pas.

---

## A. Limites Phase 9 acceptées mais **non certifiées** (reprises)

Source historique : [`../phase-09-distribution-admin-hardening/KNOWN_ISSUES.md`](../phase-09-distribution-admin-hardening/KNOWN_ISSUES.md) (corps antérieur à l’acceptation, conservé). Phase 9 a été **ACCEPTED** le 2026-09-18 malgré ces résidus — ils deviennent des **critères Phase 10**, pas des dettes oubliées.

| Sujet | État réel | Lot |
| --- | --- | --- |
| **TLS** | Lot A : `SslStream` in-process (`Server:Tls:Mode=Off\|Required`, défaut Off). Bind public sans cert + Required = fail-fast. Proxy externe, mTLS, DPAPI : absents. | P10-5 A **livré** ; B–E ouverts |
| **Packaging client/éditeur** | Scripts `publish-frog.ps1/.sh` produisent des layouts **framework-dependent** (`--self-contained false`). Lancement de `Frog.Client.exe` / `Frog.Editor.exe` depuis l’arbre publié **non prouvé**. Smokes Windows = `dotnet test` from-source. Serveur Linux **prouvé** (`PackagedServerPostgreSqlProcessTests`). | P10-6 |
| **Paquet autonome sans SDK** | Le mandat bêta exige un runtime fourni ou déclaré. Aujourd’hui il faut le runtime .NET 8 sur la machine. | P10-6 |
| **LOAD** | Mesuré : 200 Hello + 100 mixed **in-memory**. PG authed concurrent = **4**. Non certifiés : idle 300 s, économie 10 mut/s, interact 5/s + rafale 20, restart-reconnect 25 &lt; 60 s, pool PG ≤ 20, palier **25×60 min**, TLS, monde publié. Pas de HTTP `/metrics`. | P10-8 |
| **Restore avec sanctions** | `PostgresBackupRestoreTests` : migrate + seed Phase 7 + compte + dump/restore + login. **Pas** de campagne dont le dump contient des lignes mute/ban. Guildes/amis/échanges n’existent pas encore. | P10-7 |
| **Rate-limit login** | IP normalisée + username (8/60s) et IP (30/60s). Plus de clé IP:port. Voir [`AUTH_RATE_LIMIT.md`](AUTH_RATE_LIMIT.md). | P10-5 B **livré** |
| **Inscriptions ouvertes** | Défaut local `Registration:Mode=Open`. Bêta : `ProvisionedOnly` (TCP refusé). InviteOnly = jalon sans jetons. | P10-5 C **livré** (jalon invites) |
| **Grant opérateur** | `tools/Frog.OpsCli operator grant|revoke` + SQL toujours possible | P10-5 C **livré** |
| **P9-S social** | Groupes/guildes/amis/blocage **P10-1 livré** (v11, 80–83). Trade P2P **absent** (P10-2). Stubs `Guild.cs` / `GuildService.cs` toujours morts. | P10-1 fait ; P10-2 |

Autres résidus documentés Phase 9 (non bloquants pour *leur* gate, toujours vrais) :

- `PRD_MMO_Maker_CSharp.md` v2.1 cité, **absent** du dépôt.
- `docs/DATA_MODEL.md` périmé (cartes/tilesets) vs `FrogDbContext` (`auth`, `content`, `ops`, `player`, `world`).
- ~100 fichiers `// TODO: Implémenter` folklore (client `Models/*`, `Services/*`, `Guild.cs`, `MaintenanceService.cs`, `OptionsForm.cs`, `UserSettings.cs`, …). Le gameplay réel passe par `MainShellForm` + `FrogGameClient`.
- CI `concurrency.cancel-in-progress: true` (annulations ≠ preuves vertes).
- Annotation Node 20 / actions v4 (dépréciation, pas un échec de test).

---

## B. Items Phase 10 **absents** (mandat)

| Item | Preuve d’absence | Lot |
| --- | --- | --- |
| Groupes | `PartyRoster` + opcodes 80–83 + canal Party | P10-1 **livré** |
| Guildes persistées | `player.guilds*` ; stubs `Guild.cs` / `GuildService.cs` toujours TODO | P10-1 **livré** (stubs non composés) |
| Amis / blocage | `player.friendships`, `player.character_blocks` | P10-1 **livré** |
| Échanges P2P | Boutique/banque Phase 7 **≠** trade joueur ; pas d’opcode 84–86 | P10-2 |
| Client TLS + validation certificat | `FrogGameClient.ConnectAsync` + `TlsClientAuthenticator` (Mode=Required) ; défaut Off | P10-5 A **livré** |
| Invitations / comptes provisionnés | `Registration:Mode=ProvisionedOnly` + OpsCli create ; **InviteOnly sans jetons** (jalon) | P10-5 C **livré** (jalon invites) |
| Outil reset mot de passe / revoke session / grant GM | `tools/Frog.OpsCli` | P10-5 C **livré** |
| Aide intégrée, rebind AZERTY/QWERTY, settings persistés | `UserSettings.cs` / `OptionsForm.cs` / `InputService.cs` = stubs ; pas de « Help » dans `MainShellForm` | P10-3 |
| Numéro de version visible + copie diagnostics | Token redacté dans le log interne ; pas de commande dédiée ni version UI | P10-3 |
| Monde démo 3 cartes 30–60 min + licences | `fixtures/` = legacy ; seeds de tests seulement | P10-4 |
| Recette 12 étapes / 2 machines | Non exécutée | P10-4 |
| Self-contained + manifeste SHA-256 d’archives | Layout scripts seulement ; `artifacts/` gitignoré | P10-6 |
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
- Playtest éditeur résout `Frog.Server.exe` via chemins `bin/Debug|Release` du **dépôt**, pas via l’arbre `publish-frog`.
- `Phase8JsonEditorPanel` n’est plus branché (éditeurs structurés Dialogue/Quête/Recette/Région/CommonEvent/Métier/Météo) — fichier mort, pas une preuve d’édition JSON obligatoire, mais le mandat interdit de **dépendre** du JSON ; rester sur les formulaires.
- Lancement depuis paquet publié : **non prouvé**.

### Packaging

- `scripts/publish-frog.sh` / `.ps1` : RID `server-linux-x64`, `server-win-x64`, `client-win-x64`, `editor-win-x64`.
- Preuve serveur : `packaged-server-smoke.sh` + test processus PG.
- Client/éditeur : layout possible depuis Linux (`EnableWindowsTargeting`), **exécution Windows non certifiée**.

### Restore / load

- Scripts `postgres-backup` / `restore` / `verify` + runbook Phase 9 : **présent**, campagne lignes métier incomplète.
- `tools/Frog.LoadHarness` : Hello/chat/move/mixed in-memory ; ne décode pas l’économie, le social, le TLS, ni 60 minutes.

### PostgreSQL rôles

- Compose `POSTGRES_USER=frog` = superuser **démo locale** seulement. Hébergé : `frog_runtime` (P10-5 D, [`POSTGRES_ROLES.md`](POSTGRES_ROLES.md)).

### Client « démo technique »

`MainShellForm` expose encore des commandes de workshop (hôte/port, « Demander map », GUID boutique en secours `Visible=false`, jeton reconnect en mémoire processus). Acceptable en interne ; **insuffisant** pour un testeur externe (P10-3).

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
