# 🐸 FRoG Creator (Modern C# Edition.)

Projet de modernisation complète du **FRoG Creator OSE v0.6.3** (VB6) vers **C# / .NET 8**, en conservant la logique d’origine tout en modernisant l’architecture, les outils et la base de données.

**Dépôt GitHub :** [https://github.com/Netsuno/MMO_Maker](https://github.com/Netsuno/MMO_Maker)

---

## 🎯 Objectifs

- Migrer le moteur **VB6** (Client, Serveur, Éditeur) vers une base **C# .NET 8 (WinForms)**.
- Unifier la logique commune dans un projet central `Frog.Core`.
- Moderniser la communication réseau (voir **Décisions réseau** ci‑dessous).
- Sauvegarder les données dans une **base MariaDB** (protocole MySQL ; comptes, état monde, cartes `frog_map`, personnages `frog_character`, etc. — voir `Frog.Server/Docs/mariadb-persistence-plan.md` et la **vision schéma complet** objet/quêtes/économie : `Frog.Server/Docs/mariadb-schema-cible-complet.md`).
- Rendre l’éditeur compatible avec les formats d’origine tout en préparant l’extension du moteur.

---

## 🧭 Décisions produit (référence)

Alignement équipe (**vision MMO Maker** RPG Maker‑like pour un MMO **2D Graal / Zelda SNES‑like** ; [FRoG Creator OSE 0.6.3](https://github.com/Alexoune001/FRoG-Creator-OSE-V0.6.3) comme inspiration fonctionnelle, pas comme stack).

| Sujet | Choix |
|--------|--------|
| **Hébergement & éditeur public** | **Un seul monde hébergé par vous** au début ; équipe comme auteurs. Plus tard : joueurs peuvent créer du contenu depuis l’éditeur, **toujours rattaché à votre monde / votre serveur** ; monétisation = **fonctionnalités**, pas court terme. |
| **Licence / compte** | **Liée au compte** (serveur uniquement vous). |
| **Plateformes** | **Windows seulement** pour l’instant. |
| **Rendu** | Vue **Zelda SNES / Graal Online**. Tuiles **32×32 px** pour l’instant (évolutif). |
| **Mouvement** | **Pixels** (autoritaire **serveur**). |
| **Combat mêlée** | **8 directions** ; **PvE only** au début ; timing **simple pour l’instant** (à affiner ensuite). Knockback **+ courte invulnérabilité** (style Zelda). **Joueurs :** pas traverser les uns les autres sauf maps avec **flag éditeur** (collision joueur désactivée / traversée). |
| **NPC ennemis** | **Statiques ou patrouille simple** ; **scripts comportement rapidement après** (+ **Lua** pour événements côté map **créateur**, hors logique compilée dans le serveur core — pas priorité tout de suite mais prévu pour les « joueurs‑auteurs »). |
| **Cartes** | Objectif **plusieurs maps vite** (+ warps) ; pas d’**instances** pour l’instant ; plus tard : instances normales **+ procédurales**. |
| **Téléchargement carte** | **HEAD révision / hash puis blob** tant que ça reste fiable ; cache client acceptable. |
| **Événements (RPG Maker)** | **À faire bientôt** : événements sur cases, **liste en DB réutilisable** entre maps/cases, association sauvegardée avec la carte ou la tuile. |
| **Personnages** | **Plusieurs slots** prévus ; **stats tôt** : **STR, AGI, DEX, INT, VIT, LUCK**. |
| **Objets / inventaire** | **Priorité importante** : au début chaque « type » d’objet prend **1 place**. **Serveur :** effets / règles en **DB**. **Client :** mise en cache des infos « affichage / ambiance » pour ne pas surcharger la DB. |
| **Mode offline jeu** | **Pas tranché** pour l’instant. |
| **Réseau** | **Contrôle autoritaire serveur**. **TCP** pour flux fiables ; ajouter ou basculer **UDP** lorsque jugé meilleur compromis **sécurité / lag** à plus grande échelle (voir protocole, sans tout casser d’un coup). Cible volume **≤100 joueurs / carte au début**, architecture **scalable** si carton. |
| **Chat** | Actuel : global / map / whisper. **Guilde → chat guilde**, **groupe → chat groupe** lorsque ces systèmes existeront. **Logs chat** oui ; **kick modérateur plus tard**, **ban en DB** dès besoin plausible. |
| **Archi dépôt** | Conserver **Client / Serveur / Éditeur / Core** séparés. |
| **Publier en DB depuis l’éditeur** | **Oui** (priorité après socle maps / données). |
| **Conflit multi‑auteur carte** | **Plus tard**, pas problème actuel. |
| **Auth** | **Votre serveur seulement** pour l’instant. **RGPD** à préciser selon exposition publique. |
| **Audio** | **Qualité**, contenu livré avec le client (zip bêta). |
| **Distribution** | **Zip bêta testeurs** au début, pas storefront imposée. |
| **Héritage FRoG** | Conserver **l’idée d’un MMO éditable** comme base éducative / produit. **Rompre** : problèmes VB6 ; données **en clair peu sécurisées** tout en texte comme à l’époque. |

### Jalons technique proposés (ordre pour travailler en autonomie)

1. **Multi‑maps** stables + **flag carte** collisions joueurs + sync **revision/hash** carte côté client.  
2. **Stats persistées + multi‑slots perso** (DB + protocole login/sélection).  
3. **Événements carte + catalogue DB** (déclencheurs, liaison tuile/map), éditeur minimal pour placer/traiter.  
4. **PvE** : monstre piloté serveur + dégâts + knockback / i‑frames + mort / respawn NPC.  
5. **Items** : définition DB (effets) + façade client locale + inventaire grille simple.  
6. **Publication éditeur → MariaDB** (cartes puis événements / définitions progressives).  
7. Pipeline **Lua** événements auteur‑carte sandboxé (après métadonnées stables).  
8. Observabilité + **charges** : mesurer broadcasts par carte puis décisions **UDP / AOI**.

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
| **Frog.Core** | Modèles partagés, enums, interfaces, sérialiseurs binaires (maps, items, NPCs…), IDs de paquets, `ChatChannel`, **`WorldMetrics`** (tuile ↔ pixels). |
| **Frog.Client** | Client WinForms : TCP, login, map (rendu tuile simplifié), flèches, chat, heartbeat, mêlée ; doc `Frog.Client/Docs/protocol_login_map.md`. |
| **Frog.Editor** | Éditeur de cartes WinForms : outils brush/fill/rectangle, undo/redo, `.fmap`, tilesets. |
| **Frog.Server** | Serveur TCP : sessions, **carte `.fmap` ou `frog_map`**, mouvements **pixels**, **mêlée** (réseau évolutif UDP si besoin), chat, persistance MariaDB optionnelle, sauvegarde périodique joueur. |
| **Frog.Tests** | Tests unitaires (sérialisation, protocole, persistance mémoire, mouvements…). |

---

## 🔧 Technologies principales

- **.NET 8.0 / C# 12**
- **WinForms** pour Client et Éditeur
- **MariaDB** (ou MySQL) + **MySqlConnector** (schéma v1 : comptes, `player_world_state`, `frog_map`, persos, assets — activer avec `MariaDb.enabled` + `appsettings.Local.json` ; ne pas committer les secrets)
- **TCP** (état actuel) ; **UDP** prévu pour flux haute fréquence
- **Sérialisation binaire** (format `.fmap` versionné dans `MapSerializer`)

---

## 📊 État des modules (réaliste)

| Module | Statut | Détail court |
|--------|--------|----------------|
| **Frog.Core** | 🟢 Actif | `MapSerializer`, `TileType`, attributs, `PacketId`, `ChatChannel`. |
| **Frog.Server** | 🟢 En évolution | TCP, login/register, map(s), mouvement, collisions, **warps**, chat 3 canaux, heartbeat, logout, `PlayerLeave`, sauvegarde joueur (mémoire ou MariaDB), nettoyage sessions. |
| **Frog.Client** | 🟢 En évolution | WinForms : `FrogGameClient` + `Form1` (connexion, map **PNG multi-couches**, déplacements, chat 3 canaux, heartbeat, mêlée). |
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
<summary><strong>Phase 3 — Éditeur de cartes (outil métier créateur)</strong> (partiel)</summary>

- [x] **Mini‑carte** (coin carte) : rectangle de vue, clic pour centrer ; pan / zoom Ctrl+molette notifient la mini-carte.
- [x] **Chrome RPG Maker** (approx.) : workspace sombre, **une seule barre de menus** (actions + raccourcis), tuiles + onglets A–D à droite, arbre « Cartes », bandeau titre carte.
- [x] **`Map.Validate()`** : au moins une couche, bornes tuiles, pas de doublon (x,y) par couche, warps (MapId ≥ 0, destination ≥ 0).
- [x] **`PropertyGrid`** : catégories / descriptions sur les propriétés **Tuile** ; validation via menu **Carte**.
- [ ] **Marqueurs / événements** sur carte (**priorité équipe**) + liste d’événements persistée (**DB**) pour réutilisation multi‑cases / multi‑maps.

</details>

<details>
<summary><strong>Phase 4 — Serveur : monde vivant</strong></summary>

- [ ] **Changement de carte** et **multi‑maps** sous autorité serveur (paquets + état joueur).
- [ ] Persistance **au‑delà de la position** (inventaire, flags monde, quêtes — MVP puis extension).
- [ ] Alignement **collisions/règles** avec la carte servie ; anti‑abus minimal ; exploitation des **logs** pour hébergement.

</details>

<details>
<summary><strong>Phase 5 — Client joueur présentable</strong></summary>

- [ ] **HUD / UX** : connexion, chat lisible, retours combat basiques.
- [ ] Combat **action** étendu (vision Zelda‑like : directions, hitbox, i‑frames, armes animées si souhaité).
- [ ] **Options joueur** (plein écran, volume, résolution minimale).

</details>

<details>
<summary><strong>Phase 6 — Données de jeu (objets, armes, autres)</strong></summary>

- [ ] Modèles **`Frog.Core`** + fichier / chargement **serveur** pour items et équipements.
- [ ] Premier **éditeur de données** (objets, armes ou table unifiée) avec IDs référençables par la carte.
- [ ] Boucle **loot / équipement / utilisation d’objet** résolue côté **serveur** (effets jeu MVP).

</details>

<details>
<summary><strong>Phase 7 — Scripts créateur</strong></summary>

- [ ] Choix **runtime + sandbox** (Lua, C# isolé ou DSL ; limites CPU/mémoire ; pas de fichier/réseau arbitraire par défaut).
- [ ] **API documentée et stable** : hooks (connexion joueur, entrée carte, interaction, objet, PNJ/combat selon périmètre).
- [ ] Erreurs **lisibles créateur** (logs, fichier/ligne si possible) et stratégie de **rechargement** sans redémarrage total si faisable.

</details>

<details>
<summary><strong>Phase 8 — Distribution et confiance</strong></summary>

- [ ] **Packaging** (ZIP ou installateur léger) + **exemple jouable** (`.fmap` + tilesets dans le dépôt ou release).
- [ ] **Admin minimal** : mute, kick ou ban (modération liée aux canaux chat).
- [ ] Hygiène **sécurité** (secrets hors repo en prod ; comptes ; TLS éventuellement plus tard).
- [ ] **CI** sur le dépôt : build + tests à chaque push (compléter les tests intégration / PG progressivement).

</details>

_La liste technique **par composant** (Core / Server / Client / Editor / Tests) est dans **Roadmap** juste après cette section._

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
- [x] Tables MariaDB comptes + `player_world_state` (si MariaDb activé)
- [ ] **UDP** : canal snapshots (positions / combat) + reprise perte
- [ ] **Instances** / changement de map serveur (hors map monde unique)
- [x] Warps côté serveur (téléport **même carte** après `MoveRequest` ; cible hors monde ignorée jusqu’aux instances)
- [x] Chargement carte monde depuis **fichier `.fmap`** (`Maps:worldMapPath`, relatif au dossier de l’exe ou chemin absolu)
- [x] **Mêlée** : `MeleeAttackRequest` / `MeleeAttackResult`, portée en **pixels** (`Frog.Core/Constants/WorldMetrics.cs`)
- [x] Logs réseau structurés : **JSON console** (`Logging:Console:FormatterName`), scopes `ConnectionId` / `RemoteEndPoint` / `Username`, `ServerNetworkLogs` + `PacketDispatcher` / `PacketSender`

### 🎮 Frog.Client
- [x] Client réseau selon `protocol_login_map.md` (`FrogGameClient` : Hello, login/register, map, move, positions, chat, heartbeat, mêlée, erreurs)
- [x] Rendu map : **tilesets PNG** (dossiers `Maps/` + `Tilesets/`, manifeste `.tilesets.json`) + secours couleurs ; **tuiles 32 px** (`WorldMetrics`)
- [x] Bouton **Logout** (`LogoutRequest` / `LogoutAck`, fermeture TCP côté serveur)
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
- [x] Tests `MapSerializer`, **Hello / `FrogWireProtocol.Version`** (`WireHelloTests`), mouvement, warps, chat parse, store mémoire
- [ ] Tests intégration client ↔ serveur (TCP)
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
