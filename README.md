# 🐸 FRoG Creator (Modern C# Edition.)

Projet de modernisation complète du **FRoG Creator OSE v0.6.3** (VB6) vers **C# / .NET 8**, en conservant la logique d’origine tout en modernisant l’architecture, les outils et la base de données.

**Dépôt GitHub :** [https://github.com/Netsuno/MMO_Maker](https://github.com/Netsuno/MMO_Maker)

---

## 🎯 Objectifs

- Migrer le moteur **VB6** (Client, Serveur, Éditeur) vers une base **C# .NET 8 (WinForms)**.
- Unifier la logique commune dans un projet central `Frog.Core`.
- Moderniser la communication réseau (voir **Décisions réseau** ci‑dessous).
- Sauvegarder les données dans une **base PostgreSQL** (comptes + état monde joueur).
- Rendre l’éditeur compatible avec les formats d’origine tout en préparant l’extension du moteur.

---

## 🧭 Décisions produit (référence)

| Sujet | Choix |
|--------|--------|
| **Chat** | Trois canaux : **global**, **par carte (map)**, **chuchotement (whisper)**. |
| **Persistance joueur** | Sauvegarde **périodique** (intervalle configurable, défaut 45 s) + sauvegarde à la **déconnexion** / **expiration session** / **logout** — limiter la charge serveur (pas de save à chaque paquet). |
| **Cartes / warps** | **Phase 1 : une seule carte monde** pour tous les joueurs (`MapService.DefaultWorldMapId`). **Phase 2 :** instances / multi‑maps. |
| **Transport (MMO)** | **TCP** pour tout le contrôle fiable (auth, chat, map, état synchrone actuel). **UDP** (snapshots position / combat) prévu en phase ultérieure pour réduire la latence — sans casser le flux TCP existant. |
| **Version protocole** | Pas de numéro de version dans les paquets **pendant le développement actif** ; **à ajouter** avant compatibilité client multiple. |
| **Combat** | Visée **Zelda / Graal Online** (action, timing). Portée mêlée en **pixels** (centre tuile → centre tuile, `WorldMetrics.MeleeRangePixels`). **Plus tard :** magie, niveaux, stats, éléments. |

---

## 🧱 Structure du projet

| Projet | Description |
|--------|--------------|
| **Frog.Core** | Modèles partagés, enums, interfaces, sérialiseurs binaires (maps, items, NPCs…), IDs de paquets, `ChatChannel`, **`WorldMetrics`** (tuile ↔ pixels). |
| **Frog.Client** | Client WinForms : TCP, login, map (rendu tuile simplifié), flèches, chat, heartbeat, mêlée ; doc `Frog.Client/Docs/protocol_login_map.md`. |
| **Frog.Editor** | Éditeur de cartes et de ressources, inspiré du FRoG Creator original. |
| **Frog.Server** | Serveur TCP : sessions, **carte `.fmap` optionnelle** (`Maps:worldMapPath`), map monde unique, mouvements, **mêlée pixel**, chat, persistance PostgreSQL optionnelle, sauvegarde périodique joueur. |
| **Frog.Tests** | Tests unitaires (sérialisation, protocole, persistance mémoire, mouvements…). |

---

## 🔧 Technologies principales

- **.NET 8.0 / C# 12**
- **WinForms** pour Client et Éditeur
- **PostgreSQL** + **Npgsql** (comptes + `player_world_state` quand `Postgres.enabled` est `true`)
- **TCP** (état actuel) ; **UDP** prévu pour flux haute fréquence
- **Sérialisation binaire** (format `.fmap` versionné dans `MapSerializer`)

---

## 📊 État des modules (réaliste)

| Module | Statut | Détail court |
|--------|--------|----------------|
| **Frog.Core** | 🟢 Actif | `MapSerializer`, `TileType`, attributs, `PacketId`, `ChatChannel`. |
| **Frog.Server** | 🟢 En évolution | TCP, login/register, map, mouvement, collisions, **warps** (monde unique), chat 3 canaux, heartbeat, logout, `PlayerLeave`, sauvegarde joueur (mémoire ou PG), nettoyage sessions. |
| **Frog.Client** | 🟢 En évolution | WinForms : `FrogGameClient` + `Form1` (connexion, map, déplacements, chat 3 canaux, heartbeat, mêlée). |
| **Frog.Editor** | 🟠 En cours | Édition map / tiletypes / warps (base présente). |
| **Tests** | 🟢 Partiel | Couverture sur Core + helpers serveur ; à étendre (intégration TCP, PG). |

---

## 🧠 Roadmap

### 🧩 Frog.Core
- [ ] MapSerializerV2 (Block / Warp / Resource) si évolution format
- [ ] Enrichir `Map.Validate()` (bornes tuiles, warps, cohérence couches)
- [ ] Attributs additionnels (Door, NpcSpawn, zones…)
- [ ] **(Plus tard)** Champ / en-tête **version protocole** partagé client/serveur

### 🖥️ Frog.Server
- [x] Protocole TCP de base (frames, login, map, move, erreurs)
- [x] Sessions + idle timeout + heartbeat
- [x] Chat **global / map / whisper**
- [x] Persistance position **périodique** + restauration au login (`Persistence:saveIntervalSeconds`)
- [x] Tables PostgreSQL comptes + `player_world_state` (si PG activé)
- [ ] **UDP** : canal snapshots (positions / combat) + reprise perte
- [ ] **Instances** / changement de map serveur (hors map monde unique)
- [x] Warps côté serveur (téléport **même carte** après `MoveRequest` ; cible hors monde ignorée jusqu’aux instances)
- [x] Chargement carte monde depuis **fichier `.fmap`** (`Maps:worldMapPath`, relatif au dossier de l’exe ou chemin absolu)
- [x] **Mêlée** : `MeleeAttackRequest` / `MeleeAttackResult`, portée en **pixels** (`Frog.Core/Constants/WorldMetrics.cs`)
- [x] Logs réseau structurés : **JSON console** (`Logging:Console:FormatterName`), scopes `ConnectionId` / `RemoteEndPoint` / `Username`, `ServerNetworkLogs` + `PacketDispatcher` / `PacketSender`

### 🎮 Frog.Client
- [x] Client réseau selon `protocol_login_map.md` (`FrogGameClient` : Hello, login/register, map, move, positions, chat, heartbeat, mêlée, erreurs)
- [x] Rendu map tuile (couleurs par type + joueurs) — **pas** de tilesets PNG pour l’instant
- [ ] Logout bouton + polish HUD
- [ ] **(Plus tard)** Combat action complet (animations, i-frames, armes) — **mêlée pixel** déjà côté serveur (`MeleeAttackRequest`)

### 🗺️ Frog.Editor
- [ ] Palette / overlay attributs complet
- [ ] Outils avancés (rectangle, copier/coller, fill…)
- [ ] Undo/Redo
- [ ] Propriétés de carte, multi‑tilesets

### 🧪 Tests
- [x] Tests `MapSerializer`, mouvement, warps, chat parse, store mémoire
- [ ] Tests intégration client ↔ serveur (TCP)
- [ ] Tests PostgreSQL (conteneur / fixture)

### ⚔️ Combat (vision)
- [x] Mêlée **portée pixel** + paquets `MeleeAttackRequest` / `MeleeAttackResult` (résolution serveur)
- [ ] Hitboxes / directions d’attaque, i-frames, armes
- [ ] Magie, niveaux, stats, éléments (extensions)

---

## 🚀 Exécution rapide

```bash
dotnet build Frog.Creator.sln
dotnet run --project Frog.Server/Frog.Server.csproj
dotnet run --project Frog.Client/Frog.Client.csproj
dotnet test Frog.Tests/Frog.Tests.csproj
```

Configurer `Frog.Server/appsettings.json` : `Server`, `Postgres`, `Sessions`, `Persistence`, **`Maps`** (`worldMapPath` vers un `.fmap` exporté par l’éditeur ; voir `Frog.Server/Maps/README.txt`).

---

## 💬 Crédits & Origine

Basé sur le projet open-source **FRoG Creator OSE v0.6.3** :  
👉 [https://github.com/Alexoune001/FRoG-Creator-OSE-V0.6.3](https://github.com/Alexoune001/FRoG-Creator-OSE-V0.6.3)

Modernisé et réorganisé par **Netsun**, pour la planification, l’analyse et la migration technique.

---

## 📜 Licence

Projet sous licence **MIT**, libre d’utilisation et de modification.
