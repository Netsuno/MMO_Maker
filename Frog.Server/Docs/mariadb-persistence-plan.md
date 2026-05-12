# MariaDB — persistance complète (plan et schéma)

Le serveur utilise le **protocole MySQL** via **[MySqlConnector](https://mysqlconnector.net/)** (compatible **MariaDB** et MySQL).

**Secrets :** ne jamais committer les mots de passe. Utiliser `Frog.Server/appsettings.Local.json` (ignoré par Git) d’après `appsettings.Local.json.example`, ou les variables d’environnement (`MariaDb__ConnectionString`, etc.).

**Créer les tables à la main :** voir [`Frog.Server/Database/README.md`](../Database/README.md) et le script `scripts/apply-frog-mariadb-schema.ps1`.

---

## 1. Configuration locale

1. Copier `appsettings.Local.json.example` → `appsettings.Local.json` dans **`Frog.Server/`**.
2. Renseigner `MariaDb:enabled`, `MariaDb:connectionString`, et éventuellement `Maps:databaseFallbackMapId`.
3. Chaîne typique : `Server=hôte;Port=3306;Database=nom;User Id=user;Password=***` (voir [connection strings](https://mysqlconnector.net/connection-options/)).

### 1.1 Tests avec une vraie base

```powershell
$env:MARIADB_TEST_CONNECTION_STRING = "Server=...;Port=3306;Database=...;User Id=...;Password=..."
dotnet test --filter "FullyQualifiedName~MariaDbSchemaIntegration"
```

---

## 2. Schéma v1 (`Database/schema_frog_mariadb_v1.sql`)

Exécuté au démarrage si `MariaDb.enabled` est `true` (`MariaDbSchemaBootstrap.Apply`).

| Table | Rôle |
|--------|------|
| **accounts** | Compte (login, hash, sel, `created_utc`). |
| **player_world_state** | (Héritage) position par compte ; `character_uuid` optionnel. Le serveur lit/écrit désormais **`character_world_state`**. |
| **character_world_state** | Position / carte **par personnage** (`character_uuid` PK → `frog_character`) ; base des **multi-slots**. |
| **frog_map** | Carte : `id`, `map_key`, `revision`, `content_sha256`, `fmap_blob` (LONGBLOB). |
| **frog_character** | Perso : `id` CHAR(36), `account_username`, `account_id` → `accounts.id` (migration v2), `display_name`, `payload` JSON. |
| **frog_asset_blob** | Binaires dédupliqués (SHA-256). |
| **frog_map_editor_save** | Historique des sauvegardes éditeur. |
| **frog_event_catalog** / **frog_map_event** | Événements carte (catalogue + placements). |
| **frog_item_definition** | Catalogue des **types** d’objets (slug, nom, pile max). |
| **character_inventory_slot** | **Inventaire relationnel** : une ligne par `(character_uuid, slot_index)` ; `item_definition_id` NULL = case vide ; FK vers `frog_item_definition`. |

**Décision persistance :** l’inventaire joueur **n’est pas** stocké dans le JSON `frog_character.payload` ; il vit dans **`character_inventory_slot`** (extension future : stacks séparés, équipement, banque — voir [`mariadb-schema-cible-complet.md`](mariadb-schema-cible-complet.md) §3.5).

Contrainte **fk_pws_character** : ajoutée en C# après le script si elle n’existe pas (`information_schema`).

**Version moteur :** MariaDB **10.5+** recommandé (`ADD COLUMN IF NOT EXISTS`, `CREATE INDEX IF NOT EXISTS`, `JSON`).

### 2.1 Migration « v2 » (`MariaDbMigrationV2`)

Idempotent, après v1 et `fk_pws_character` :

- Colonne **`accounts.id`** (`BIGINT UNSIGNED` AUTO_INCREMENT `UNIQUE`) — identifiant numérique stable ; **`username`** reste la **PK** pour les anciennes FK (`player_world_state`, `frog_map_editor_save`).
- **`frog_character.account_id`** NOT NULL → FK `fk_frog_character_account_id` vers `accounts(id)` ; **backfill** depuis `account_username` ; **`fk_fc_account`** (FK sur username) **supprimée** pour éviter le double rattachement.

Si des persos ont un `account_username` sans compte correspondant, le bootstrap lève une **exception** jusqu’à correction des données.

### 2.2 Migration « v3 » (`MariaDbMigrationV3`)

Idempotent, après v2 :

- Crée **`character_world_state`** si absent (même DDL que dans `schema_frog_mariadb_v1.sql` pour les installs neuves).
- **`INSERT IGNORE`…** depuis `player_world_state` : résout `character_uuid` ou le perso **Hero** du compte ; n’insère que si l’UUID existe dans **`frog_character`** (pas d’erreur FK).

Le serveur utilise **`IPlayerStateStore.TryGetForCharacter` / `UpsertForCharacter`** (plus d’écriture sur `player_world_state`).

### 2.3 Seed automatique `frog_map`

`MariaDbWorldMapSeeder` (hosted service, démarré avant le serveur TCP) : si `MariaDb.enabled`, `Maps:databaseFallbackMapId` &gt; 0 et aucune ligne `frog_map` pour cet id, insertion de **Starter Meadow** (même carte que `Frog.Core.Maps.MapSamples`).

### 2.4 Personnage par défaut

À chaque login, `ICharacterBootstrap.EnsureDefaultHero(username)` crée au besoin une ligne **frog_character** (`display_name = 'Hero'`). La position est lue/écrite dans **`character_world_state`** pour `Session.CharacterId`. Des persos supplémentaires sont créés via **`ICharacterBootstrap.TryCreateCharacter`** (protocole `CharacterCreateRequest`, max 8 par compte, nom validé côté serveur).

### 2.5 Migration « v7 » (`MariaDbMigrationV7`)

Idempotent, après v6 : si la table **`frog_item_definition`** n’existe pas encore, crée **`frog_item_definition`**, **`character_inventory_slot`**, l’index `idx_character_inventory_slot_item`, et un seed **`demo_item`** (`INSERT IGNORE`, aligné sur le script v1) — utile pour les bases créées avant l’extension inventaire.

---

## 3. Synchro client (cache carte)

Même principe qu’avant : `IMapBlobStore.TryGetHead` (révision + SHA-256) puis téléchargement du blob si différent. Implémentation : `MariaDbMapBlobStore`.

---

## 4. Chargement carte serveur

`MapService` : fichier `.fmap` d’abord, puis `frog_map` si `Maps:databaseFallbackMapId` > 0, sinon carte de secours.

Publication : `MariaDbMapBlobStore.UpsertMap(...)`.

---

## 5. Fichiers clés

| Fichier | Rôle |
|---------|------|
| `Database/schema_frog_mariadb_v1.sql` | DDL v1 |
| `Database/MariaDbSchemaBootstrap.cs` | Script + FK optionnelle + migration v2 + seed `demo` |
| `Database/MariaDbMigrationV2.cs` | `accounts.id` + `frog_character.account_id` |
| `Database/MariaDbMapBlobStore.cs` | Lecture / `UpsertMap` |
| `Services/MariaDbWorldMapSeeder.cs` | Seed `frog_map` si ligne absente |
| `Database/MariaDbCharacterBootstrap.cs` | Perso « Hero » par compte |
| `Persistence/MariaDbPlayerStateStore.cs` | État monde + `character_uuid` |
| `Database/MariaDbAccountRepository.cs` | Comptes |
| `Config/MariaDbOptions.cs` | Options `MariaDb` |
| `Frog.Core/Maps/MapSamples.cs` | Carte Starter Meadow partagée |

---

## 6. Phases suivantes

Protocole client (HEAD puis blob), CRUD persos, éditeur « Publier », migrations versionnées (Flyway / scripts numérotés), pool et observabilité.

## 7. Schéma MariaDB — vision long terme (complet)

Liste cible ER (compte, perso, objets inventaire définitions/instance, quêtes, monde, NPC, économie, social, audit, assets, etc.) et stratégie d’extension depuis la v1 : **[`mariadb-schema-cible-complet.md`](mariadb-schema-cible-complet.md)**.

---

*Document vivant.*
