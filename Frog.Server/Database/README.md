# Schéma MariaDB — tables Frog

## Contenu

| Fichier | Rôle |
|---------|------|
| **`schema_frog_mariadb_v1.sql`** | Crée toutes les tables et index (idempotent : `IF NOT EXISTS`). |

Tables : `accounts`, `player_world_state`, `frog_map`, `frog_character`, `frog_asset_blob`, `frog_map_editor_save`.

## Option A — Automatique (recommandé)

1. Crée une base vide (ex. `mmo_test`) dans MariaDB.
2. Dans `Frog.Server/appsettings.Local.json`, mets `MariaDb.enabled` à `true` et une `connectionString` valide.
3. Lance le serveur une fois : **`MariaDbSchemaBootstrap`** exécute ce script au démarrage, puis ajoute la FK `fk_pws_character` si besoin, et peut créer le compte `demo` / seed carte selon ta config.

## Option B — À la main (client SQL)

Depuis la racine du dépôt, avec le client `mysql` ou `mariadb` dans le `PATH` :

```bash
mysql -h 127.0.0.1 -P 3306 -u ton_user -p ta_base < Frog.Server/Database/schema_frog_mariadb_v1.sql
```

Sous Windows, tu peux aussi utiliser le script :

```powershell
.\scripts\apply-frog-mariadb-schema.ps1 -ServerHost 127.0.0.1 -Port 3306 -Database ta_base -User ton_user -Password "ton_mot_de_passe"
```

Ensuite, **un premier démarrage du serveur** avec MariaDB activé complète la FK `fk_pws_character` (si le script SQL seul ne l’inclut pas).

## Vérification rapide

```sql
SHOW TABLES;
DESCRIBE accounts;
DESCRIBE frog_map;
```
