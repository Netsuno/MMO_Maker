# Phase 10 — Audit de base (P10-0)

Date : 2026-09-18. Dépôt : https://github.com/Netsuno/MMO_Maker  
Tip audité : `f74b34cca09dda819fe26747d48ee16d27007dfd`  
Méthode : lecture du code et des docs **présents**, grep opcodes/TLS/TODO, CI GitHub. Pas d’invention de fichiers.

---

## Identité du tip

| Item | Valeur |
| --- | --- |
| `main` | `f74b34c` merge PR #7 |
| Produit Phase 9 | `cab57b9` `fix(phase9): serialize ban with final login/reconnect validation` |
| CI merge | [35386572613](https://github.com/Netsuno/MMO_Maker/actions/runs/35386572613) SUCCESS |
| CI produit | [35384819869](https://github.com/Netsuno/MMO_Maker/actions/runs/35384819869) SUCCESS |
| Branche Phase 10 avant ce lot | **n’existait pas** |
| Dossier Phase 10 avant ce lot | **n’existait pas** |
| Mandat plus récent | **aucun** (issues vides ; PR ouvertes = 0) |
| Protocole | v10 |
| PacketId utile max | 79 (`ModerateResult`) |
| ChatChannel | 0/1/2 |
| Persistence | PostgreSQL EF, dernière migration `20260918001424_OpsAccountSanctions` |

Comptes de tests **revendiqués à l’acceptation** (mandat + CI produit) : Frog.Tests **454**, PG **181**, éditeur **87×3**, gameplay **6×3**, Phase 8 **24×3**, 12 captures SHA-256 exactes.

---

## Carte « fichier vs fonction »

| Chemin | Nature | Produit ? |
| --- | --- | --- |
| `Frog.Client/MainShellForm.cs` + `Network/FrogGameClient.cs` | Client réel | oui (interne) |
| `Frog.Client/Models/*`, `Services/*`, `Controls/ChatBox.cs`, `Config/UserSettings.cs`, `Forms/OptionsForm.cs` | `// TODO: Implémenter` | **non** |
| `Frog.Server/Network/PacketDispatcher.cs` | Serveur réel | oui |
| `Frog.Server/Models/Guild.cs`, `Services/GuildService.cs`, `MaintenanceService.cs`, `AdminCommandService` folklore | stubs | **non** |
| `Frog.Editor/Forms/MainForm.cs` + Phase 6/8 panels | Éditeur réel | oui (from-source) |
| `Frog.Editor/Forms/ItemEditorForm.cs` etc. (racine Forms TODO) | stubs morts (vrais éditeurs = `GameData/*`) | **non** |
| `Frog.Editor/Forms/Phase8/Phase8JsonEditorPanel.cs` | non branché | **non** |
| `scripts/publish-frog.*` | layouts | serveur Linux **oui** ; EXE Win **non prouvé** |
| `tools/Frog.LoadHarness` | charge courte in-memory | mesure ≠ certif 25×60 |
| `tests/.../PostgresBackupRestoreTests.cs` | restore schéma + login | **pas** sanctions/social |
| `docker-compose.yml` | PG 16 local, user `frog` superuser d’instance | dev seulement |

---

## Réseau et sécurité (constat)

1. `ServerSocket` : `new TcpListener(bindAddress, port)`.
2. Client : `await tcp.ConnectAsync(host, port)` puis `GetStream()` — pas TLS.
3. Bind public : `Server:AllowNonLoopbackBind=true` documenté comme **clair**.
4. Login limiter : `TryAllow(clientSession.RemoteEndPoint)` — IP:port.
5. Register : ouvert, message « Compte cree. »
6. Opérateur : `auth.operators` ; slash `/mute|/kick|/ban` → opcode 78.
7. Jeton reconnect : string en mémoire formulaire, redacté à l’affichage log, **pas** DPAPI/Keychain.
8. `PostgreSql:allowInMemoryFallback` défaut false ; composition hébergée refuse l’absence de PG.

---

## Social / trade (constat)

- `grep` Guild/Party/Trade/Friend dans `Frog.Client/**/*.cs` : **aucun** hit métier.
- `PacketId` : pas d’opcode ≥ 80 métier.
- Chat whisper isolé (test `ChatWhisper_DoesNotLeakToThirdParty`) — base à étendre, pas un blocage persistant.
- Économie P2P : inexistante. Shop/Bank = NPC / coffre perso.

---

## Éditeur / contenu (constat)

Présent : cartes `.fmap` + PG, tilesets, NPC, items, sorts, classes, shops, ressources, dialogues, quêtes, recettes, régions, common events, professions, météo, événements carte, playtest processus, close coordinator.

Manquant pour la bêta externe : lancement depuis `artifacts/publish/editor-win-x64`, monde démo redistribuable 3 cartes, licences assets, preuve « auteur crée un contenu distinct et le joueur le voit » sur paquets.

---

## Décisions de plan qui découlent de l’audit

1. Ne pas « implémenter Guild.cs ».
2. Passer le protocole en v11 (parseur chat fermé sur 0–2).
3. Self-contained est un **changement** P10-6, pas un flag déjà là.
4. P10-8 ne peut pas recycler le LOAD_REPORT Phase 9 comme preuve 25×60.
5. P10-7 doit **peupler** mute/ban/guildes/échanges avant de dump.
6. PacketDispatcher : un rédacteur ; extraire Social/Trade en `partial`.
