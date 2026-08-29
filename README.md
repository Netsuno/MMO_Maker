# MMO Maker (C# / .NET 8)

Environnement de création de MMO 2D : **éditeur**, **client** et **serveur** autoritaire, avec **PostgreSQL** comme source de vérité.

- Inspiration **fonctionnelle** : [FRoG Creator OSE 0.6.3](https://github.com/Alexoune001/FRoG-Creator-OSE-V0.6.3) (idées de gameplay / éditeur, **sans compatibilité** — ADR-0003).
- Inspiration **ergonomique** : principes RPG Maker (workspace, arbre de cartes, palette, outils) — identité et code originaux.
- Dépôt : [Netsuno/MMO_Maker](https://github.com/Netsuno/MMO_Maker)
- PRD d’exécution : `PRD_MMO_Maker_CSharp.md` ; état factuel : [`docs/STATUS.md`](docs/STATUS.md)

## Stack

| Couche | Choix |
| --- | --- |
| Langage | C# 12 / .NET 8 |
| Éditeur / client | Windows, WinForms (+ coque WPF temporaire, ADR-0004) |
| Serveur | .NET 8 console / Generic Host |
| Persistance produit | **PostgreSQL** (EF Core) |
| MariaDB | Héritage optionnel uniquement — voir [`docs/MARIADB_DOMAIN_MATRIX.md`](docs/MARIADB_DOMAIN_MATRIX.md) |

## Démarrage rapide

```bash
dotnet restore Frog.Creator.sln
dotnet build Frog.Creator.sln -c Release
dotnet test Frog.Tests/Frog.Tests.csproj -c Release --no-build

docker compose up -d postgres   # optionnel pour intégration
export FROG_POSTGRES_TEST_CONNECTION_STRING='Host=127.0.0.1;Port=5432;Database=frog_test;Username=frog_test;Password=frog_test_local_only'
dotnet test tests/Frog.Persistence.IntegrationTests/Frog.Persistence.IntegrationTests.csproj -c Release
```

Voir [`docs/TESTING.md`](docs/TESTING.md) et [`docs/BACKLOG.md`](docs/BACKLOG.md).

---

## Décisions produit (référence équipe)

Alignement (**vision MMO Maker** pour un MMO **2D Graal / Zelda SNES-like**).

| Sujet | Choix |
|--------|--------|
| **Hébergement & éditeur public** | **Un seul monde hébergé par vous** au début ; équipe comme auteurs. Plus tard : joueurs peuvent créer du contenu depuis l’éditeur, **toujours rattaché à votre monde / votre serveur** ; monétisation = **fonctionnalités**, pas court terme. |
| **Licence / compte** | **Liée au compte** (serveur uniquement vous). |
| **Plateformes** | **Windows seulement** pour l’instant. |
| **Rendu** | Vue **Zelda SNES / Graal Online**. Tuiles **32×32 px** pour l’instant (évolutif). |
| **Mouvement** | **Pixels** (autoritaire **serveur**). |
| **Combat mêlée** | **8 directions** ; **PvE only** au début ; timing **simple pour l’instant** (à affiner ensuite). Knockback **+ courte invulnérabilité** (style Zelda). **Joueurs :** pas traverser les uns les autres sauf maps avec **flag éditeur** (collision joueur désactivée / traversée). |
| **NPC ennemis** | **Statiques ou patrouille simple** ; comportement et événements carte via **moteur de commandes typées** data-driven (Phase 8) — pas d’exécution Lua/C# arbitraire côté serveur. |
| **Cartes** | Objectif **plusieurs maps vite** (+ warps) ; pas d’**instances** pour l’instant ; plus tard : instances normales **+ procédurales**. |
| **Téléchargement carte** | **HEAD révision / hash puis blob** tant que ça reste fiable ; cache client acceptable. |
| **Événements (RPG Maker)** | **À faire bientôt** : événements sur cases, **liste en DB réutilisable** entre maps/cases, association sauvegardée avec la carte ou la tuile. |
| **Personnages** | **Plusieurs slots** prévus ; **stats tôt** : **STR, AGI, DEX, INT, VIT, LUCK**. |
| **Objets / inventaire** | **Priorité importante** : au début chaque « type » d’objet prend **1 place**. Persistance relationnelle côté produit via PostgreSQL (MariaDB historique encore présente côté serveur). |
| **Mode offline jeu** | **Pas tranché** pour l’instant. |
| **Réseau** | **Contrôle autoritaire serveur**. **TCP** pour flux fiables ; UDP possible plus tard. |
| **Chat** | Actuel : global / map / whisper. Guilde/groupe plus tard. |
| **Archi dépôt** | Client / Serveur / Éditeur / Core / Application / Persistence séparés. |
| **Publier en DB depuis l’éditeur** | **Oui** (PostgreSQL via ports applicatifs). |
| **Héritage FRoG** | Inspiration fonctionnelle **uniquement** — **pas** d’import `.fcc` ni de compatibilité VB6 (ADR-0003). |

### Jalons technique proposés (ordre pour travailler en autonomie)

1. [x] **Multi-maps** stables + **flag carte** collisions joueurs + sync **révision / hash** carte côté client (warps, empreintes par `mapId`, rechargement UI après warp).  
2. [x] **Stats persistées + multi‑slots perso** (position **`character_world_state`**, stats JSON **Hero**, **`CharacterListRequest` / `CharacterSelectRequest`** + UI client liste / activer / écran carte).  
3. **Événements carte + catalogue DB** (déclencheurs, liaison tuile/map), éditeur minimal pour placer/traiter — **priorité P1** (voir section **Priorités courantes**).  
4. **PvE** : monstre piloté serveur + dégâts + knockback / i‑frames + mort / respawn NPC.  
5. **Items** : définition DB (effets) + façade client locale + inventaire grille simple.  
6. **Publication éditeur → PostgreSQL** (cartes puis événements / définitions progressives ; MariaDB = héritage seulement).  
7. **Phase 8** — événements carte, dialogues, quêtes, métiers, régions/météo, common events (moteur typé, PostgreSQL, serveur autoritaire).  
8. **Phase 9** — packaging, administration, durcissement prod, certification charge.  
9. Observabilité + **charges** : mesurer broadcasts par carte puis décisions **UDP / AOI**.

### Succès utilisateur minimal (vous l’avez défini)

**Deux joueurs à distance**, **chat fonctionnel**, **dialogue NPC**, **combattre un monstre**.

---

<details>
<summary><strong>Références historiques précédentes (table courte d’origine)</strong></summary>

| Élément | Détail conservé comme rappel |
|--------|-----------------------------|
| **Persistance joueur** | Sauvegarde **périodique** (défaut 45 s) + déco / logout / session expirée ; pas une save à chaque paquet. |
| **Version protocole** | `FrogWireProtocol.Version` dans **Hello** ; incrémenter si rupture binaire incompatible. |

</details>

---

## 🧱 Structure du projet

| Projet | Description |
|--------|--------------|
| **Frog.Core** | Domaine partagé, modèles, sérialiseur `.fmap`, protocole (à extraire plus tard). |
| **Frog.Application** | Cas d’usage et ports (cartes, santé DB, imports ops). |
| **Frog.Persistence.PostgreSql** | EF Core / Npgsql, migrations, repositories. |
| **Frog.Client** | Client WinForms joueur. |
| **Frog.Editor** | Éditeur (coque WPF temporaire + WinForms). |
| **Frog.Server** | Serveur TCP autoritaire (MariaDB optionnelle héritée). |
| **Frog.Legacy** | Expérimental / différé (lecteur `.fcc` — ADR-0003). |
| **Frog.Tests** / **IntegrationTests** | Unitaires + PostgreSQL isolé. |

---

## Technologies principales

- **.NET 8.0 / C# 12**
- **WinForms** (+ coque WPF temporaire pour l’éditeur)
- **PostgreSQL** (source de vérité produit)
- **MariaDB / MySqlConnector** : héritage optionnel seulement
- **TCP** ; sérialisation `.fmap` pour cache/export

---

## 📊 État des modules (réaliste)

| Module | Statut | Détail court |
|--------|--------|----------------|
| **Frog.Core** | 🟢 Actif | `MapSerializer`, `TileType`, attributs, `PacketId`, `ChatChannel`. |
| **Frog.Server** | 🟢 En évolution | TCP, login/register, map(s), mouvement, collisions, **warps**, chat 3 canaux, heartbeat, logout, `PlayerLeave`, sauvegarde joueur (mémoire ou MariaDB), nettoyage sessions. |
| **Frog.Client** | 🟢 En évolution | WinForms : `FrogGameClient` + `MainShellForm` (écrans **Connexion → Perso → Carte**, map **PNG multi-couches**, déplacements, chat 3 canaux, heartbeat, mêlée). |
| **Frog.Editor** | 🟢 En évolution | UI type **RPG Maker** (sombre, menu Fichier / Édition / …, texte menu clair, **outils & type de tuile en listes déroulantes** pour colonnes étroites, tuiles A–D, arbre cartes, `.fmap` + manifeste). |
| **Tests** | 🟢 Partiel | Couverture sur Core + helpers serveur ; à étendre (intégration TCP, PG). |

---

## ✅ Feuille de route créateur (ordre logique)

Objectifs pour qu’une **personne seule** puisse assembler un mini‑MMO (éditeur + serveur + client) jusqu’aux **éditeurs d’objets/armes** et aux **scripts intégrés**. Les phases se suivent de façon raisonnable : contrat réseau et fidélité de la carte en premier, puis serveur/client jouables, enfin données jeu, scripting et distribution.

<details>
<summary><strong>Phase 1 — Fondations et contrat produit</strong> (fait)</summary>

- [x] Numéro de **version protocole** dans **Hello** (`FrogWireProtocol.Version` + lecture côté client, déconnexion si mismatch).
- [x] **`Frog.Client/Docs/protocol_login_map.md`** aligné avec Hello versionné et **`.fmap` / `MapSerializer.MapFileFormatVersion`**.
- [x] Guide **« premier monde en une session »** : [`Docs/premier-monde.md`](Docs/premier-monde.md).

</details>

<details>
<summary><strong>Phase 2 — Même monde partout (fidélité carte / tiles)</strong> (fait)</summary>

- [x] **Taille de tuile 32 px** partagée : `WorldMetrics.DefaultTileSizePixels`, rendu client, découpe `SrcX`/`SrcY` (mêlée : `MeleeRangePixels` = 56).
- [x] **Rendu client** : PNG par `TilesetId` si présents, **ordre des couches** = ordre `Map.Layers` ; couche **Attributes** en surcouche semi-transparente ; secours couleur si image absente.
- [x] **Manifeste** : à l’enregistrement carte, `{nom}.tilesets.json` à côté du `.fmap` ; client lit `Maps/{nomCarte}.tilesets.json`, `Tilesets/manifest.json`, ou `Tilesets/{id}.png`.

</details>

<details>
<summary><strong>Phase 3 — Éditeur de cartes (outil métier créateur)</strong> (partiel — **P1** dans « Priorités courantes »)</summary>

- [x] **Mini‑carte** (coin carte) : rectangle de vue, clic pour centrer ; pan / zoom Ctrl+molette notifient la mini-carte.
- [x] **Chrome RPG Maker** (approx.) : workspace sombre, **une seule barre de menus** (actions + raccourcis), tuiles + onglets A–D à droite, arbre « Cartes », bandeau titre carte.
- [x] **`Map.Validate()`** : au moins une couche, bornes tuiles, pas de doublon (x,y) par couche, warps (MapId ≥ 0, destination ≥ 0).
- [x] **`PropertyGrid`** : catégories / descriptions sur les propriétés **Tuile** ; validation via menu **Carte**.
- [x] **Événements carte (socle)** : tables **`frog_event_catalog`** / **`frog_map_event`**, sync **`MapEventsRequest`/`Result`**, surbrillance client (rectangle / losange / cercle selon `triggerKind`), **`InteractRequest`** + triggers **`interact`** / **`step_on`** / **`page`** (logs serveur structurés `MapEvent*`).
- [x] **Éditeur** : dialogue MariaDB (**CRUD catalogue** + placements + `trigger_kind`), raccourci **Ctrl+clic droit** sur le canevas → menu « événements sur cette tuile » ; **marqueurs** canevas + mini-carte (lecture MariaDB, `editor-workstate`).
- [x] **Suite P1 (socle)** : déclencheur **`auto_tile`** (heartbeat serveur) ; métadonnée catalogue **`script_key`** (wire JSON `scriptKey`, MariaDB, éditeur — exécution réservée **Phase 7**) ; **filtres** dans le dialogue événements (catalogue + placements).
- [ ] **Suite P1 (exécution)** : runtime scripts sandbox + API ; autres raffinements UX événements au besoin.

</details>

<details>
<summary><strong>Phase 4 — Serveur : monde vivant</strong> (**P1** persistance / logs)</summary>

- [x] **Changement de carte / multi‑maps** : warps inter-cartes, `CurrentMapId` session, `PositionUpdate` avec `MapId`, `MapRequest` + empreintes par carte ; client WinForms recharge la carte affichée après warp.
- [x] **Persistance MVP au‑delà de la position** : **`worldFlags`** → **`character_world_flag`** ; **stats** → **`character_stat`** ; **extras** → **`character_payload_kv`** (LONGTEXT, **v9–v10**) ; **inventaire** (**V7**) ; **aucun** type JSON SQL sur `frog_character` (**v10**) ; quêtes **à venir**.
- [x] **Collisions / règles** : même blob carte serveur et client ; **`MapCollision.IndexBlockedTiles`** + **`IsBlockedForPlayerCircle`** côté prédiction client et **`MapService`** côté serveur.
- [x] **Anti‑abus minimal** : plafond **50** paquets **`MoveRequest` + `PositionSyncRequest`** / seconde glissante par session (`MovementPacketRateGate`).
- [x] **Logs hébergement** : `ServerNetworkLogs.MapEvent*` (5021–5024), **`WorldFlagsPatched`** (5025), **`MovementRateLimited`** (5026, Debug).

</details>

<details>
<summary><strong>Phase 5 — Client joueur présentable</strong> (**P2** HUD / options)</summary>

- [ ] **HUD / UX** : connexion, chat lisible, retours combat basiques.
- [ ] Combat **action** étendu (vision Zelda‑like : directions, hitbox, i‑frames, armes animées si souhaité).
- [ ] **Options joueur** (plein écran, volume, résolution minimale).

</details>

<details>
<summary><strong>Phase 6 — Données de jeu (objets, armes, autres)</strong> (**P2**)</summary>

- [ ] Modèles **`Frog.Core`** + chargement **serveur** pour items ; persistance inventaire **relationnelle** (`character_inventory_slot` ↔ `frog_item_definition`) ; façade client locale + boucle **loot / équipement / utilisation** côté serveur.
- [ ] Premier **éditeur de données** (objets, armes ou table unifiée) avec IDs référençables par la carte.
- [ ] Boucle **loot / équipement / utilisation d’objet** résolue côté **serveur** (effets jeu MVP).

</details>

<details>
<summary><strong>Phase 7 — Gameplay essentiel</strong> (**ACCEPTED**)</summary>

- [x] Client gameplay : register/login, personnage, inventaire, équipement, banque, shop, combat mêlée/sort, respawn, reconnexion.
- [x] Serveur autoritaire PostgreSQL ; smoke Windows ×3 ; garde-fous lifecycle.
- Voir [`docs/progress/phase-07-essential-gameplay/`](docs/progress/phase-07-essential-gameplay/).

</details>

<details>
<summary><strong>Phase 8 — Quêtes, événements et création avancée</strong> (**IN PROGRESS**)</summary>

Roadmap autoritaire : `PRD_MMO_Maker_CSharp.md`. Moteur **data-driven typé** (pas Lua/C#/PowerShell arbitraire).

- [ ] **P8-1** — Modèle PostgreSQL événements carte + éditeur Events (pages, conditions, commandes, triggers).
- [ ] **P8-2** — Interpréteur d’événements serveur autoritaire + catalogue de commandes.
- [ ] **P8-3** — Dialogues et quêtes (progression serveur, journal client).
- [ ] **P8-4** — Métiers et recettes (craft instantané atomique).
- [ ] **P8-5** — Régions, météo et éclairage.
- [ ] **P8-6** — Common events et outils créateur avancés.
- Voir [`docs/progress/phase-08-quests-events-advanced-creation/`](docs/progress/phase-08-quests-events-advanced-creation/).

</details>

<details>
<summary><strong>Phase 9 — Distribution et confiance</strong> (**P3** — hors Phase 8)</summary>

- [ ] **Packaging** (ZIP ou installateur léger) + **exemple jouable**.
- [ ] **Admin minimal** : mute, kick ou ban (modération chat).
- [ ] Hygiène **sécurité** prod ; certification charge / backup-restore.
- [x] **CI** : [`.github/workflows/ci.yml`](.github/workflows/ci.yml) — build Release + tests PostgreSQL + smoke Windows.

</details>

_La liste technique **par composant** (Core / Server / Client / Editor / Tests) est dans **Roadmap** juste après cette section._

---

## 🎯 Priorités courantes (découpage des phases)

Ordre de travail **court** : ce qui débloque le **socle MMO** en premier (événements → persistance → joueur présentable → données → scripts → distribution).

| Rang | Phases / chantiers | Pourquoi en premier |
|------|---------------------|---------------------|
| **P1** | **Phase 3** — marqueurs / **événements carte** + **liste en DB** réutilisable (tuile / map). | Bloque **dialogue NPC**, quêtes simples, interactions case ; aligné décision produit « événements bientôt ». |
| **P1** | **Phase 4** — persistance **au-delà de la position** (flags / inventaire léger MVP si besoin) + **collisions / règles** alignées carte servie + **exploitation des logs**. | Monde **sauvegardé** et serveur **exploitable** pour une bêta. |
| **P2** | **Phase 5** — **HUD / UX** en jeu (chat lisible, retours combat) ; **options** (volume, fenêtre). L’écran **Connexion → Perso → Carte** est déjà posé (`MainShellForm`). | Objectif **deux joueurs + chat + dialogue + combat** avec une UI lisible. |
| **P2** | **Phase 6** — **objets / armes** dans `Frog.Core` + chargement serveur + **éditeur de données** + boucle **loot / équipement / utilisation** côté serveur. | Décision produit : **objets** importants tôt. |
| **P2** | **Phase 7** — **gameplay essentiel** (client, combat, économie, reconnexion). | **ACCEPTED** — voir `docs/progress/phase-07-essential-gameplay/`. |
| **P1** | **Phase 8** — **événements, dialogues, quêtes, métiers, régions/météo** (moteur typé PG). | En cours — bloque la boucle création → gameplay data-driven. |
| **P3** | **Phase 9** — **packaging** ZIP bêta, **admin** chat, **sécurité** prod, certification charge. | Après Phase 8 gate ; CI de base déjà en place. |

**Jalons techniques** (section plus haut) : **multi-maps** est coché ; enchaîner **événements + DB** puis **PvE** / **items** reste cohérent avec ce tableau.

---

## 🧠 Roadmap

### 🧩 Frog.Core
- [ ] MapSerializerV2 (Block / Warp / Resource) si évolution format
- [ ] Enrichir `Map.Validate()` (bornes tuiles, warps, cohérence couches)
- [ ] Attributs additionnels (Door, NpcSpawn, zones…)
- [x] **`FrogWireProtocol.Version`** dans **Hello** (`Frog.Core/Protocol/WireHello.cs`) + carte documentée (**`MapSerializer.MapFileFormatVersion`**)

### 🖥️ Frog.Server
- [x] Protocole TCP de base (frames, login, map, move, erreurs)
- [x] Sessions + idle timeout + heartbeat
- [x] Chat **global / map / whisper**
- [x] Persistance position **périodique** + restauration au login (`Persistence:saveIntervalSeconds`)
- [x] Tables MariaDB comptes + **`character_world_state`** (position par `frog_character`) + héritage `player_world_state` (si MariaDb activé)
- [ ] **UDP** : canal snapshots (positions / combat) + reprise perte
- [ ] **Instances** (donjons / zones isolées) — hors périmètre actuel (monde partagé multi-cartes uniquement)
- [x] Warps après `MoveRequest` : téléport **inter-cartes** si le blob cible est disponible ; **empreinte SHA** par `mapId` (`TryMatchMapFingerprint` / `MapRequest` corps 40 octets vs carte **courante** session)
- [x] Chargement carte monde depuis **fichier `.fmap`** (`Maps:worldMapPath`, relatif au dossier de l’exe ou chemin absolu)
- [x] **Mêlée** : `MeleeAttackRequest` / `MeleeAttackResult`, portée en **pixels** (`Frog.Core/Constants/WorldMetrics.cs`)
- [x] Logs réseau structurés : **JSON console** (`Logging:Console:FormatterName`), scopes `ConnectionId` / `RemoteEndPoint` / `Username`, `ServerNetworkLogs` + `PacketDispatcher` / `PacketSender`

### 🎮 Frog.Client
- [x] Client réseau selon `protocol_login_map.md` (`FrogGameClient` : Hello, login/register, map, move, positions, chat, heartbeat, mêlée, erreurs)
- [x] Rendu map : **tilesets PNG** (dossiers `Maps/` + `Tilesets/`, manifeste `.tilesets.json`) + secours couleurs ; **tuiles 32 px** (`WorldMetrics`)
- [x] Bouton **Logout** (`LogoutRequest` / `LogoutAck`, fermeture TCP côté serveur)
- [x] **Warp inter-cartes** : `PositionUpdate` local toujours appliqué ; `MapRequest` auto debouncé + empreintes par `mapId` dans `FrogGameClient`
- [x] **Multi-slots (MVP)** : `FrogWireProtocol` v4–v5, liste persos, **création** perso (`CharacterCreate*`), changement de perso actif (`MainShellForm` + `FrogGameClient`)
- [ ] Polish HUD
- [ ] **(Plus tard)** Combat action complet (animations, i-frames, armes) — **mêlée pixel** déjà côté serveur (`MeleeAttackRequest`)

### 🗺️ Frog.Editor
- [x] Outils **rectangle** + **pot de peinture** (flood fill 4-connexions sur la couche active)
- [x] **Undo / redo** (snapshots `MapSerializer`, profondeur limitée) + menu **Édition** + **Ctrl+Z** / **Ctrl+Y**
- [x] **Pinceau en traînée** (clic maintenu) ; `MainForm` réorganisé (dock correct, plus de doublons de palettes)
- [x] Dialogue renommer couche sans `Microsoft.VisualBasic` ; radio **Script** branchée dans la palette types
- [ ] Palette / overlay attributs complet (métier)
- [ ] Copier/coller sélection, multi‑sélection tuiles
- [ ] Propriétés de carte avancées, multi‑tilesets

### 🧪 Tests
- [x] Tests `MapSerializer`, **Hello / `FrogWireProtocol.Version`** (`WireHelloTests`), mouvement, warps, empreintes par carte (`TryMatchMapFingerprint`), chat parse, store mémoire
- [ ] Tests intégration client ↔ serveur (TCP) ; CI GitHub : voir `.github/workflows/ci.yml`
- [x] Seed `frog_map` + perso `Hero` + `character_uuid` sur sauvegardes (MariaDb activé)
- [ ] Tests MariaDB (conteneur / `MARIADB_TEST_CONNECTION_STRING`)

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
dotnet run --project Frog.Editor/Frog.Editor.csproj
dotnet test Frog.Tests/Frog.Tests.csproj
```

Test MariaDB réel (schéma + idempotence) : définir `MARIADB_TEST_CONNECTION_STRING` puis
`dotnet test Frog.Tests/Frog.Tests.csproj --filter "FullyQualifiedName~MariaDbSchemaIntegration"` (voir `Frog.Server/Docs/mariadb-persistence-plan.md`).

Configurer `Frog.Server/appsettings.json` : `Server`, `MariaDb`, `Sessions`, `Persistence`, **`Maps`** (`worldMapPath` vers un `.fmap` exporté par l’éditeur ; voir `Frog.Server/Maps/README.txt`).

**Premier monde (guide pas à pas)** : [`Docs/premier-monde.md`](Docs/premier-monde.md). Protocole détaillé : [`Frog.Client/Docs/protocol_login_map.md`](Frog.Client/Docs/protocol_login_map.md).

---

## 💬 Crédits & Origine

Basé sur le projet open-source **FRoG Creator OSE v0.6.3** :  
👉 [https://github.com/Alexoune001/FRoG-Creator-OSE-V0.6.3](https://github.com/Alexoune001/FRoG-Creator-OSE-V0.6.3)

Modernisé et réorganisé par **Netsun**, pour la planification, l’analyse et la migration technique.

---

## 📜 Licence

Projet sous licence **MIT**, libre d’utilisation et de modification.
