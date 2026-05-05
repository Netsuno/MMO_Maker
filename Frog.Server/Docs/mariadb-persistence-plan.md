# MariaDB — persistance complète (plan et schéma)

Le serveur utilise le **protocole MySQL** via **[MySqlConnector](https://mysqlconnector.net/)** (compatible **MariaDB** et MySQL).

**Secrets :** ne jamais committer les mots de passe. Utiliser `Frog.Server/appsettings.Local.json` (ignoré par Git) d’après `appsettings.Local.json.example`, ou les variables d’environnement (`MariaDb__ConnectionString`, etc.).

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
| **player_world_state** | Position / carte ; `character_uuid` optionnel → `frog_character`. |
| **frog_map** | Carte : `id`, `map_key`, `revision`, `content_sha256`, `fmap_blob` (LONGBLOB). |
| **frog_character** | Perso : `id` CHAR(36), `account_username`, `display_name`, `payload` JSON. |
| **frog_asset_blob** | Binaires dédupliqués (SHA-256). |
| **frog_map_editor_save** | Historique des sauvegardes éditeur. |

Contrainte **fk_pws_character** : ajoutée en C# après le script si elle n’existe pas (`information_schema`).

**Version moteur :** MariaDB **10.5+** recommandé (`ADD COLUMN IF NOT EXISTS`, `CREATE INDEX IF NOT EXISTS`, `JSON`).

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
| `Database/MariaDbSchemaBootstrap.cs` | Script + FK optionnelle + seed `demo` |
| `Database/MariaDbMapBlobStore.cs` | Lecture / `UpsertMap` |
| `Persistence/MariaDbPlayerStateStore.cs` | État monde joueur |
| `Database/MariaDbAccountRepository.cs` | Comptes |
| `Config/MariaDbOptions.cs` | Options `MariaDb` |

---

## 6. Phases suivantes

Protocole client (HEAD puis blob), CRUD persos, éditeur « Publier », migrations versionnées (Flyway / scripts numérotés), pool et observabilité.

---

*Document vivant.*
