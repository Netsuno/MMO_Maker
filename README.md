# MMO Maker

Environnement de création d’un MMO **2D** (vue Zelda SNES / Graal) : **éditeur**, **client** Windows et **serveur TCP autoritaire**.

Inspiration **fonctionnelle** : [FRoG Creator OSE 0.6.3](https://github.com/Alexoune001/FRoG-Creator-OSE-V0.6.3) — idées de gameplay et d’éditeur, **sans compatibilité** (ADR-0003). Ergonomie proche d’RPG Maker, code et identité originaux.

[![CI](https://github.com/Netsuno/MMO_Maker/actions/workflows/ci.yml/badge.svg?branch=main)](https://github.com/Netsuno/MMO_Maker/actions/workflows/ci.yml)
[![.NET 8](https://img.shields.io/badge/.NET-8-512BD4?logo=dotnet&logoColor=white)](https://dotnet.microsoft.com/)
[![C# 12](https://img.shields.io/badge/C%23-12-239120?logo=csharp&logoColor=white)](#stack)
[![PostgreSQL](https://img.shields.io/badge/PostgreSQL-source%20de%20vérité-4169E1?logo=postgresql&logoColor=white)](docs/decisions/ADR-0002-postgresql-source-of-truth.md)
[![Protocol v11](https://img.shields.io/badge/protocol-v11-0ea5e9)](#stack)
[![License: MIT](https://img.shields.io/badge/license-MIT-green)](#licence)

Dépôt : [Netsuno/MMO_Maker](https://github.com/Netsuno/MMO_Maker)

---

## Statut

Phase 9 est **acceptée** et fusionnée sur `main`. La Phase 10 (bêta fermée externe) est **ouverte** sur `cursor/phase10-beta-release` (PR Draft [#8](https://github.com/Netsuno/MMO_Maker/pull/8)) : lots P10-0…P10-8 livrés ou acceptés propriétaire (25×60 dédié + recette 2 PCs, **2026-09-19**). Paquet **docs candidate** P10-9 — pas de phrase de gate dans ce lot.

| Phase | Statut | Preuve |
| --- | --- | --- |
| **7** — Gameplay essentiel | ✅ **ACCEPTED** | [`docs/progress/phase-07-essential-gameplay/`](docs/progress/phase-07-essential-gameplay/) |
| **8** — Quêtes, événements, création avancée | ✅ **ACCEPTED** | Merge [`1cd57ba`](https://github.com/Netsuno/MMO_Maker/commit/1cd57bad694f530fa5699639f9e63008522507e0) · [CI SUCCESS](https://github.com/Netsuno/MMO_Maker/actions/runs/35285230766) |
| **9** — Distribution, admin, durcissement | ✅ **ACCEPTED** | Merge [`f74b34c`](https://github.com/Netsuno/MMO_Maker/commit/f74b34cca09dda819fe26747d48ee16d27007dfd) (PR [#7](https://github.com/Netsuno/MMO_Maker/pull/7)) · tip produit [`cab57b9`](https://github.com/Netsuno/MMO_Maker/commit/cab57b94c20f86af2cc61738bdf3307ed9626ef4) · [CI produit](https://github.com/Netsuno/MMO_Maker/actions/runs/35384819869) · [CI post-merge](https://github.com/Netsuno/MMO_Maker/actions/runs/35386572613) SUCCESS |
| **10** — Bêta fermée externe | 📝 **P10-9 docs candidate** | [`docs/progress/phase-10-beta-release/`](docs/progress/phase-10-beta-release/) — P10-0…P10-8 ; 25×60 dédié + recette 2 PCs **accepted by owner Marc Giroux on 2026-09-19** ; harness CI charge = 5 s. Phrase de gate **non écrite**. |

**Persistance produit :** **PostgreSQL** (EF Core / Npgsql). MariaDB = héritage optionnel uniquement — [ADR-0002](docs/decisions/ADR-0002-postgresql-source-of-truth.md), [matrice MariaDB](docs/MARIADB_DOMAIN_MATRIX.md).

**Protocole :** `FrogWireProtocol.Version = 11` sur cette branche (Hello, canaux Party/Guild, opcodes 80–86). `main` reste v10 jusqu’à fusion. Gel : [`SOCIAL_PROTOCOL_FREEZE.md`](docs/progress/phase-10-beta-release/SOCIAL_PROTOCOL_FREEZE.md).

**Preuves d’acceptation Phase 9** (CI verte, pas un dump) : Frog.Tests **454** · PG integration **181** · Phase 8 smoke **24×3** · Editor smoke **87×3** · Gameplay smoke **6×3** · manifeste 12 captures SHA-256. Les comptes Phase 8 (**412** / **174**) restent l’historique d’acceptation de cette phase.

Journal interne : [`docs/STATUS.md`](docs/STATUS.md) · tests : [`docs/TESTING.md`](docs/TESTING.md) · backlog : [`docs/BACKLOG.md`](docs/BACKLOG.md)

---

## Stack

| Couche | Choix |
| --- | --- |
| Langage | **C# 12 / .NET 8** |
| Client | WinForms (Windows) |
| Éditeur | WinForms + **coque WPF temporaire** ([ADR-0004](docs/decisions/ADR-0004-editor-wpf-shell-temporary.md)) |
| Serveur | Console / Generic Host, **TCP autoritaire** |
| Persistance produit | **PostgreSQL 16**, EF Core |
| MariaDB | Héritage optionnel seulement — **pas** la source de vérité |
| Cartes | `.fmap` versionné (cache / export) + catalogues publiés PostgreSQL |

---

## Phase 8 — ce qui est livré

Moteur **data-driven typé** (pas de Lua / C# / PowerShell arbitraire côté serveur). Contenu édité, publié dans PostgreSQL, interprété par le serveur autoritaire.

| Capacité | Détail |
| --- | --- |
| **Événements carte** | Pages, conditions, commandes typées, déclencheurs ; exécution serveur |
| **Quêtes & dialogues** | Journal client, progression et turn-in atomiques PostgreSQL |
| **Common events** | Appels réutilisables, sélection de page, profondeur bornée |
| **Régions** | Régions / météo / éclairage (catalogues publiés) |
| **TCP public** | `InteractRequest` + `activationId` Guid — idempotence (`Phase8InteractIdentityTcpTests`) |
| **Éditeur** | Formulaires contenu Phase 8 (dialogue, quête, recette, région, métier, météo, common event) |
| **Preuves** | Smokes Windows ×3 + tests d’intégration PostgreSQL |

Dossier d’acceptation : [`docs/progress/phase-08-quests-events-advanced-creation/`](docs/progress/phase-08-quests-events-advanced-creation/)

---

## Phase 9 — ce qui est livré

Distribution interne, modération et durcissement — **pas** une bêta externe (TCP clair, paquets client/éditeur non prouvés, charge 25×60 non mesurée). Dossier : [`docs/progress/phase-09-distribution-admin-hardening/`](docs/progress/phase-09-distribution-admin-hardening/).

| Capacité | Détail |
| --- | --- |
| **Modération** | Mute / kick / ban persistés (`ops.account_sanctions`), opcode 78, slash client, `IOperatorDirectory` |
| **Sessions** | `SessionTeardown` idempotent ; courses C2/C2b ban vs login/reconnect |
| **Sécurité** | `auth.operators`, bind non-loopback explicite, placeholders secrets, `WorldFlagsPatch` rejeté en PG |
| **Backup** | `pg_dump`/`pg_restore` schémas produit ; restore **avec lignes de sanctions** non certifié |
| **Packaging** | Scripts RID ; **serveur Linux** démarre ; lancement `Frog.Client.exe` / `Frog.Editor.exe` publiés **non prouvé** |
| **Charge** | Harness in-memory (200 Hello / 100 mixed) ; palier hébergé 25 joueurs **non certifié** |
| **Social (P9-S)** | **Différé** — repris en Phase 10 |

---

## Démarrage rapide

```bash
dotnet restore Frog.Creator.sln
dotnet build Frog.Creator.sln -c Release
dotnet test Frog.Tests/Frog.Tests.csproj -c Release --no-build

docker compose up -d postgres   # optionnel — intégration PostgreSQL
export FROG_POSTGRES_TEST_CONNECTION_STRING='Host=127.0.0.1;Port=5432;Database=frog_test;Username=frog_test;Password=frog_test_local_only'
dotnet test tests/Frog.Persistence.IntegrationTests/Frog.Persistence.IntegrationTests.csproj -c Release
```

Lancer les binaires :

```bash
dotnet run --project Frog.Server/Frog.Server.csproj
dotnet run --project Frog.Client/Frog.Client.csproj
dotnet run --project Frog.Editor/Frog.Editor.csproj
```

PostgreSQL local : `docker compose up -d postgres` (identifiants de **dev** dans `docker-compose.yml`). Activer `PostgreSql` dans `Frog.Server/appsettings.json` (ou un `appsettings.Local.json` non commité) pour le runtime produit.

MariaDB n’est **pas** requis. Les tests héritage (`Category=MariaDb`, `MARIADB_TEST_CONNECTION_STRING`) restent optionnels — voir [`docs/TESTING.md`](docs/TESTING.md).

Guide pas à pas historique : [`Docs/premier-monde.md`](Docs/premier-monde.md). Protocole login / carte : [`Frog.Client/Docs/protocol_login_map.md`](Frog.Client/Docs/protocol_login_map.md).

---

## Structure du projet

| Projet | Rôle |
| --- | --- |
| **Frog.Core** | Domaine partagé, `.fmap`, protocole (`FrogWireProtocol` v10, wire Phase 7/8) |
| **Frog.Application** | Cas d’usage et ports (cartes, catalogues, contenu) |
| **Frog.Persistence.PostgreSql** | EF Core / Npgsql, migrations, repositories produit |
| **Frog.Client** | Client WinForms joueur (connexion → perso → carte, gameplay, panneaux Phase 8) |
| **Frog.Editor** | Éditeur cartes + formulaires de contenu (coque WPF temporaire) |
| **Frog.Server** | Serveur TCP autoritaire ; PostgreSQL produit, MariaDB héritage optionnel |
| **Frog.Legacy** | Expérimental / différé (lecteur `.fcc` — ADR-0003) |
| **Frog.Tests** | Unitaires (**454** à l’acceptation Phase 9 ; **412** à l’acceptation Phase 8) |
| **Frog.Persistence.IntegrationTests** | Intégration PostgreSQL isolée (**181** à l’acceptation Phase 9 ; **174** à l’acceptation Phase 8) |

Architecture : [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) · workspace éditeur : [`docs/EDITOR_WORKSPACE.md`](docs/EDITOR_WORKSPACE.md) · modèle de données : [`docs/DATA_MODEL.md`](docs/DATA_MODEL.md)

---

## Décisions produit

Vision : un **seul monde hébergé**, équipe comme auteurs ; plus tard, les joueurs peuvent créer du contenu **toujours rattaché à ce monde**. Windows seulement pour l’instant. Tuiles **32×32**. Mouvement **pixel**, autorité **serveur**.

| Sujet | Choix actuel |
| --- | --- |
| **Persistance** | **PostgreSQL** (EF Core). MariaDB = runtime historique, gelé — pas de nouvelles tables. |
| **Événements** | **Livré (Phase 8)** : cas, pages, catalogue DB réutilisable, commandes typées, association carte / tuile. |
| **Scripts auteur** | Pas d’exécution Lua/C# arbitraire. Interpréteur de **commandes typées** côté serveur. |
| **Réseau** | TCP fiable, contrôle serveur. **TLS** `Mode=Required` livré (P10-5, pas AcceptAll). UDP / AOI hors périmètre. |
| **Combat** | Mêlée 8 directions, PvE d’abord ; knockback + i-frames à affiner. |
| **Cartes** | Multi-maps + warps ; pas d’instances pour l’instant. |
| **Objets** | Une place par type au début ; définitions et inventaire **relationnels PostgreSQL**. |
| **Chat** | Global / map / whisper **livrés**. Groupe / guilde / amis / blocage / échanges **livrés** (v11, opcodes 80–86) — gel [`SOCIAL_PROTOCOL_FREEZE.md`](docs/progress/phase-10-beta-release/SOCIAL_PROTOCOL_FREEZE.md). |
| **Héritage FRoG** | Inspiration uniquement — **pas** d’import `.fcc` ni parité VB6 ([ADR-0003](docs/decisions/ADR-0003-frog-inspiration-no-compatibility.md)). |
| **Publication éditeur** | Oui, vers PostgreSQL via les ports applicatifs. |

Succès utilisateur visé (bêta) : **deux joueurs à distance** sur le client livré, **chat + social + échange**, **dialogue NPC**, **combattre**, **éditeur publié** — voir [`BETA_SCOPE.md`](docs/progress/phase-10-beta-release/BETA_SCOPE.md). Recette 2 PCs **accepted by owner Marc Giroux on 2026-09-19**.

---

## Feuille de route

| Phase | Intitulé | Statut |
| --- | --- | --- |
| 2 | Clarification (ADR, PostgreSQL, pas de compat FRoG) | ✅ |
| 3 | Shell éditeur | ✅ |
| 4 | Map editor MVP | ✅ |
| 5 | Playtest éditeur → client / serveur | ✅ |
| 6 | Éditeurs de contenu essentiels (tilesets, NPC, items, sorts, classes, shops, ressources) | ✅ |
| 7 | Gameplay essentiel | ✅ **ACCEPTED** |
| 8 | Quêtes, événements, création avancée | ✅ **ACCEPTED** sur `main` |
| 9 | Packaging, admin (mute/kick/ban), sécurité, backup, charge mesurée | ✅ **ACCEPTED** sur `main` (PR #7) — résidus repris et traités en Phase 10 |
| 10 | Bêta fermée externe (social, trade, TLS, paquets autonomes, recette, 25 joueurs) | 📝 **P10-9 docs candidate** — [mandat](docs/progress/phase-10-beta-release/MANDATE.md) · [STATUS](docs/progress/phase-10-beta-release/STATUS.md) |

Dossiers d’avancement : [`docs/progress/`](docs/progress/).

**Chantier en cours :** Phase 10, branche `cursor/phase10-beta-release`, PR Draft #8, **P10-9 docs candidate**. CI Release (Windows smokes + PostgreSQL) : [`.github/workflows/ci.yml`](.github/workflows/ci.yml).

<details>
<summary><strong>Historique — checklist créateur (phases 1–6 d’origine)</strong></summary>

Conservé comme mémoire des fondations pré-PRD. Les événements carte **PostgreSQL** et le gameplay **Phase 7/8** remplacent les mentions MariaDB / « à faire » ci-dessous.

**Phase 1 — Fondations** (fait) : `FrogWireProtocol.Version` dans Hello ; [`Docs/premier-monde.md`](Docs/premier-monde.md) ; protocole documenté.

**Phase 2 — Fidélité carte / tiles** (fait) : tuiles 32 px (`WorldMetrics`), rendu PNG multi-couches, manifeste `.tilesets.json`.

**Phase 3 — Éditeur de cartes** (fait, puis étendu Phase 4–8) : chrome RPG Maker, `Map.Validate()`, mini-carte, outils brush/fill/rectangle, undo/redo. Les dialogues événements **MariaDB** (`frog_event_catalog`) sont **héritage** — le chemin produit est PostgreSQL + formulaires Phase 8.

**Phase 4 — Serveur monde vivant** (fait) : multi-maps / warps, collisions alignées, rate-limit mouvements, persistance au-delà de la position. Quêtes = **Phase 8** (plus « à venir »).

**Phase 5 — Client présentable** : écran Connexion → Perso → Carte posé ; polish HUD / options / combat action étendu restent ouverts (hors gate Phase 8).

**Phase 6 — Données de jeu** : éditeurs essentiels **livrés** (tilesets, NPC, items, sorts, classes, shops, ressources) — voir [`phase-06-essential-content-editors`](docs/progress/phase-06-essential-content-editors/).

</details>

<details>
<summary><strong>Roadmap technique par composant (dette / plus tard)</strong></summary>

### Frog.Core
- [x] `FrogWireProtocol.Version` dans Hello (aujourd’hui **v11** sur la branche Phase 10)
- [ ] MapSerializerV2 si évolution format
- [ ] Attributs additionnels (Door, NpcSpawn, zones…)

### Frog.Server
- [x] TCP : frames, login, map, move, chat, heartbeat, logout
- [x] Warps inter-cartes + empreintes SHA par `mapId`
- [x] Mêlée pixel (`MeleeAttackRequest` / `MeleeAttackResult`)
- [x] Gameplay Phase 7 + interpréteur d’événements Phase 8 (PostgreSQL)
- [ ] UDP snapshots / AOI (hors Phase 10)
- [ ] Instances (hors périmètre — monde partagé multi-cartes)

### Frog.Client
- [x] Réseau Hello versionné, login, map PNG 32 px, warps, chat, mêlée
- [x] Multi-slots perso ; gameplay Phase 7 ; panneaux dialogue / quêtes / craft / environnement
- [x] Options, volume, aide F1, rebind AZERTY/QWERTY — **P10-3a**
- [x] TLS client + validation certificat — **P10-5** (pas AcceptAll)
- [ ] Combat action complet (animations, i-frames, armes) — hors gate bêta si mêlée actuelle suffit

### Frog.Editor
- [x] Outils carte, undo/redo, playtest, éditeurs de contenu Phases 6 et 8 (PostgreSQL)
- [ ] Palette attributs métier complète, copier/coller multi-sélection

### Tests
- [x] Unitaires Core / protocole / Phase 7–8
- [x] Intégration PostgreSQL (dont TCP `InteractRequest` / `activationId`)
- [x] Smokes Windows éditeur, Phase 8, gameplay (×3 en CI)
- [ ] MariaDB : suite héritage optionnelle seulement (`Category=MariaDb`)

</details>

---

## Documentation

| Doc | Contenu |
| --- | --- |
| [`docs/STATUS.md`](docs/STATUS.md) | Journal de statut du dépôt |
| [`docs/TESTING.md`](docs/TESTING.md) | Unitaires, PG, smokes Windows, MariaDB héritage |
| [`docs/ARCHITECTURE.md`](docs/ARCHITECTURE.md) | Frontières de projets et ADRs |
| [`docs/DATA_MODEL.md`](docs/DATA_MODEL.md) | Schéma PostgreSQL |
| [`docs/BACKLOG.md`](docs/BACKLOG.md) | Backlog actif |
| [`docs/progress/phase-10-beta-release/`](docs/progress/phase-10-beta-release/) | Phase 10 — STATUS, matrice, LOAD_REPORT, guides, manifeste candidate |
| [`docs/progress/`](docs/progress/) | Rapports de phase (2 → 10) |
| [`docs/decisions/`](docs/decisions/) | ADR-0001 … ADR-0004 |

---

## Crédits

Inspiré de **FRoG Creator OSE v0.6.3** : [Alexoune001/FRoG-Creator-OSE-V0.6.3](https://github.com/Alexoune001/FRoG-Creator-OSE-V0.6.3).

Modernisé et réorganisé par **Netsun**.

---

## Licence

**MIT** — libre d’utilisation et de modification.
