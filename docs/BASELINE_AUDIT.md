# Baseline audit — FRoG Creator (Phase 0)

- Date d’audit : 2026-08-22 02:58 UTC
- Agent / branche : `cursor/phase0-baseline-audit-02c7`
- Commit de base audité : `6df9f55046db6e0ff9c0cc815048c28db1c07a39` (`main` — *Persistance perso MariaDB sans JSON SQL (migrations v8–v10)*, 2026-05-12)
- Dépôt : `https://github.com/Netsuno/MMO_Maker` (migration C# ; distinct du dépôt VB6 upstream `Alexoune001/FRoG-Creator-OSE-V0.6.3`)
- Environnement d’exécution : Ubuntu 24.04.4 LTS (`linux-x64`), Cloud Agent
- SDK installé pour l’audit : .NET SDK **8.0.424** (installé via `dotnet-install.sh` ; `dotnet` absent du PATH système au démarrage)
- Docker : **absent** (`docker` / `docker-compose` introuvables)
- Modifications Git non commises au démarrage : **aucune** (working tree clean sur `main`)

Ce document répond aux questions de la section 30 du PRD avec des preuves mesurées. Il ne déclare rien « terminé » sans commande ni résultat.

---

## 1. Git et périmètre

| Élément | Valeur mesurée |
| --- | --- |
| Branche de départ | `main` (à jour avec `origin/main`) |
| HEAD | `6df9f55` |
| Branches distantes notables | `origin/main`, `origin/cursor/setup-dev-environment-03f6` |
| CI GitHub (derniers runs visibles) | `main` push 2026-05-13 : **success** ; PR setup-dev-environment : **success** (logs détaillés du run main : HTTP 410 / expirés) |
| Source VB6 dans ce dépôt | **Absente** (aucun `.bas` / `.frm` / `.cls` / `.vbp`) |
| AGENTS.md / `.cursor` | **Absents** |
| Scripts | `scripts/apply-frog-mariadb-schema.ps1` uniquement |
| Workflow | `.github/workflows/ci.yml` — `windows-latest`, .NET 8, restore/build solution, test `Frog.Tests` |

---

## 2. Solutions et projets réellement présents

Solution : `Frog.Creator.sln` (5 projets).

| Projet | Existe | TFM | Type | Références projet | Packages clés | Compile (Linux*) | Testé | Utilisable E2E |
| --- | ---: | --- | --- | --- | --- | ---: | ---: | ---: |
| `Frog.Core` | oui | `net8.0` | lib | — | — | oui | via Tests | lib partagée |
| `Frog.Server` | oui | `net8.0` | Exe | Core | Hosting, Logging, Config, **MySqlConnector 2.4.0** | oui* | partiel (unit) | serveur TCP + MariaDB optionnelle |
| `Frog.Client` | oui | `net8.0-windows` | WinExe | Core | — | oui* (cross-compile) | non UI | WinForms ; non exécutable ici |
| `Frog.Editor` | oui | `net8.0-windows` | WinExe | Core | Config.*, **MySqlConnector 2.4.0** | oui* | non UI | WinForms+WPF ; non exécutable ici |
| `Frog.Tests` | oui | `net8.0` | tests | Core, Server | xunit 2.9.2, Test.Sdk 17.11.1 | oui* | **82 pass** | suite unitaire |

\* Sur Linux, `EnableWindowsTargeting=true` est requis pour Client/Editor. Avec `LangVersion=latest` (Directory.Build.props), **Server échoue** (CS8652 : `ReadOnlySpan` / `payload.Span` dans méthodes async de `PacketDispatcher.cs`). Contournement de mesure : `-p:LangVersion=preview` → build solution verte. Voir §5.

### Projets PRD cibles absents

| Cible PRD | Statut |
| --- | --- |
| `Frog.Application` | absent |
| `Frog.Protocol` (projet) | absent — types sous `Frog.Core/Protocol/` |
| `Frog.Legacy` | absent |
| `Frog.Persistence.PostgreSql` | absent |
| `Frog.Rendering` | absent |
| `tools/Frog.LegacyImporter` | absent |
| `Directory.Packages.props` | absent (versions dans chaque `.csproj`) |
| `docker-compose.yml` | absent |
| `fixtures/` (racine) | absent (`Frog.Tests/Fixtures/README.txt` seulement, sans binaires) |
| `docs/STATUS.md` / `docs/BASELINE_AUDIT.md` | créés par cette phase 0 |
| `docs/LEGACY_FORMATS.md`, `LEGACY_TRACEABILITY.csv`, `DATA_MODEL.md`, `PROTOCOL.md`, `TESTING.md` | absents (docs partielles ailleurs) |

`Directory.Build.props` existant : `Nullable=enable`, `WarningsAsErrors=true`, `LangVersion=latest`.

---

## 3. Commandes exécutées et résultats

### 3.1 SDK

```text
dotnet --version
8.0.424
```

### 3.2 Restore sans flags (Linux)

```text
dotnet restore Frog.Creator.sln
→ FAIL NETSDK1100 (Frog.Client, Frog.Editor) : EnableWindowsTargeting requis
```

### 3.3 Restore + build avec Windows targeting, LangVersion par défaut

```text
dotnet restore Frog.Creator.sln -p:EnableWindowsTargeting=true
→ PASS

dotnet build Frog.Creator.sln -c Release --no-restore -p:EnableWindowsTargeting=true
→ FAIL CS8652 ×3 dans Frog.Server/Network/PacketDispatcher.cs
   (lignes ~253, ~275, ~1188 : Span / ref struct dans async)
   Core, Client, Editor : OK ; Server : FAIL
```

### 3.4 Build + tests (mesure complète, contournement LangVersion)

```text
dotnet build Frog.Creator.sln -c Release -p:EnableWindowsTargeting=true -p:LangVersion=preview
→ PASS (0 warning, 0 error)

dotnet test Frog.Tests/Frog.Tests.csproj -c Release --no-build --verbosity normal
→ PASS — Total: 82, Passed: 82, Failed: 0, Skipped: 0
```

### 3.5 Intégration MariaDB

- Variable `MARIADB_TEST_CONNECTION_STRING` : **non définie** dans cet environnement.
- `MariaDbSchemaIntegrationTests` : early-return silencieux si absente (le test compte comme réussi sans vérifier la base).
- PostgreSQL / Npgsql : **0 occurrence** dans le dépôt.
- Conteneur / Compose : **aucun**.

### 3.6 Scénario réellement vert aujourd’hui

Suite unitaire `Frog.Tests` (sérialisation `.fmap`, validation carte, mouvement/warps en mémoire, protocole Hello/wire events, rate-limit, etc.) — **82/82** avec le contournement ci-dessus. Aucun E2E client↔serveur automatisé. Aucun import VB6 `.map`. UI non exécutée sur Linux.

---

## 4. Architecture réelle vs cible PRD

```text
Existant :
  Frog.Editor ──► Frog.Core
  Frog.Client ──► Frog.Core
  Frog.Server ──► Frog.Core
  Frog.Tests  ──► Frog.Core, Frog.Server
  Persistance : MySqlConnector + SQL manuel + MariaDbMigrationV2…V10 (pas EF Core)

PRD :
  Editor/Client/Server → Application / Protocol / Rendering / Persistence.PostgreSql / Legacy → Core
```

Écarts majeurs documentés (pas corrigés en phase 0) :

1. **Persistance** : MariaDB opérationnelle vs **PostgreSQL obligatoire** dans le PRD.
2. Pas de couches `Application` / `Legacy` / `Rendering` / ports applicatifs séparés.
3. Protocole et I/O carte vivent dans `Frog.Core` (acceptable temporairement ; frontières PRD non respectées).
4. ~108 fichiers stubs `// TODO: Implémenter …` (squelette structure) + `ItemSerializer` / `NpcSerializer` → `NotImplementedException`.
5. Format carte courant = **`.fmap` C# versionné (v3/v4, magic FMAP)**, pas le binaire VB6 `.map` Put/Get.
6. Fixtures VB6 : dossier prévu vide.

---

## 5. Fonctionnalités C# non vides (aperçu vérifié)

| Domaine | Preuve | Limite |
| --- | --- | --- |
| Modèle `Map` / couches / attributs Block/Warp | `Frog.Core/Models/*`, `Map.Validate` + tests | Pas de lecteur VB6 |
| Sérialiseur `.fmap` | `MapSerializer` + round-trip tests | Format moderne, pas legacy VB6 |
| Éditeur cartes | WinForms/WPF, brush/fill, undo, events MariaDB | Non smoke-testé ici |
| Serveur TCP | framing LE Int32 + PacketId, protocole v9 | Span-in-async fragile sous C# 12 |
| Client | login, map PNG, mouvement, chat | Windows only |
| Auth / persos / inventaire relationnel | MariaDB repos + migrations v7–v10 | MariaDB only ; test intégration env-gated |
| Rendu abstrait `IRenderer` | absent | GDI+/images ad hoc |

---

## 6. Base de données

| Question | Réponse |
| --- | --- |
| Moteur | **MariaDB/MySQL** via MySqlConnector |
| EF Core | non |
| Migrations | `schema_frog_mariadb_v1.sql` + `MariaDbMigrationV2`…`V10` au bootstrap |
| Tables clés | `accounts`, `frog_map`, `frog_character`, `character_*`, `frog_event_*`, `frog_item_definition`, … |
| Secrets | `appsettings.json` a un mot de passe placeholder local ; exemples `*.Local.json.example` ; README dit de ne pas committer les secrets |
| PostgreSQL | **non configuré** |

---

## 7. Réponses aux 10 questions phase 0 (PRD §30)

1. **Projets/tests existants ?** 5 projets listés §2 ; 17 classes de tests ; 82 faits exécutés.
2. **Commande environnement de test ?** Pas de Compose. Unitaire : `dotnet test Frog.Tests/Frog.Tests.csproj`. MariaDB : définir `MARIADB_TEST_CONNECTION_STRING` + script `scripts/apply-frog-mariadb-schema.ps1`. CI = Windows.
3. **PostgreSQL configuré/migré ?** Non. MariaDB oui (schema + migrations C#).
4. **Modèle carte / importeur ?** Modèle + `.fmap` oui. Importeur legacy VB6 : non.
5. **Fixtures/tilesets réels ?** Pas de binaires VB6. Samples C# en code (`MapSamples`). Fixtures folder vide.
6. **Premier scénario vert ?** Suite unitaire 82/82 (avec flags Linux documentés).
7. **Stubs ?** ~108 TODO squelette ; Item/Npc serializers NI ; `Class1.cs` vide.
8. **Architecture cible historique implémentée sous les mêmes noms ?** Non — noms `Frog.*` partiels seulement (Core/Client/Editor/Server/Tests).
9. **Changements non commis à préserver ?** Non au démarrage.
10. **Dépôt distinct du VB6 upstream ?** Oui (`Netsuno/MMO_Maker` vs `Alexoune001/FRoG-Creator-OSE-V0.6.3`).

---

## 8. Blocages ordonnés

1. **Décision produit MariaDB vs PostgreSQL (PRD)** — conflit d’autorité : le dépôt a une persistance MariaDB large et testée partiellement ; le PRD impose PostgreSQL. Migration ou exception documentée requise avant Task 4 / Phase 4.
2. **Build Linux non reproductible sans flags** — `EnableWindowsTargeting` + CS8652 Span-in-async sous `LangVersion=latest` / C# 12.
3. **Pas de Docker / PostgreSQL / MariaDB dans l’environnement Cloud** — intégration DB non exécutée.
4. **Pas de sources VB6 ni fixtures `.map`** — Phase 2 (caractérisation legacy) bloquée sans clone/fixtures autorisées.
5. **WinForms non exécutable sur cet agent Linux** — smoke UI reporté à un hôte Windows / CI.
6. **Documentation PRD manquante** (STATUS existait pas ; formats legacy, traçabilité, DATA_MODEL PostgreSQL).

---

## 9. Première tâche réellement manquante (après ce livrable)

Selon la feuille de route PRD « douze premières tâches » :

| # | Tâche | État après phase 0 |
| ---: | --- | --- |
| 1 | Audit | **Fait** (ce document + `docs/STATUS.md`) |
| 2 | Build/tests verts sans masquer les erreurs | **À faire** : corriger CS8652 sans `LangVersion=preview` ; documenter `EnableWindowsTargeting` pour agents Linux |
| 3 | Frontières de projets + test d’architecture | À faire |
| 4 | PostgreSQL de test | **Bloqué** tant que §8.1 non tranché |
| 5+ | Inventaire VB6, formats, modèle, reader… | Dépend de sources/fixtures VB6 |

**Prochaine tâche d’exécution :** Task 2 — rendre `dotnet build` / `dotnet test` verts sur C# 12 sans preview, et documenter la commande unique.

---

## 10. Matrice de statut (phase 0)

| Domaine | État mesuré | Confiance |
| --- | --- | --- |
| Inventaire fichiers C# solution | Réalisé | Élevé |
| Build CI Windows historique | Success (métadonnées GH) | Moyen (logs expirés) |
| Build local Linux | Fail défaut / Pass avec flags | Élevé |
| Tests unitaires | 82 pass (flags) | Élevé |
| PostgreSQL | Absent | Élevé |
| MariaDB | Code présent, non exercé ici | Moyen |
| Parité VB6 carte | Non démarrée | Élevé |
| Éditeur MVP PRD | Partiel (`.fmap` moderne) | Moyen |
| Flux client-serveur | Code présent, E2E auto absent | Moyen |
