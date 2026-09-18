# Rôles PostgreSQL least-privilege (P10-5 lot D)

## Règle bloquante

Le **runtime hébergé** se connecte en **`frog_runtime`** : `LOGIN NOSUPERUSER NOCREATEDB NOCREATEROLE`. DML uniquement (`SELECT` / `INSERT` / `UPDATE` / `DELETE`) sur `auth`, `content`, `ops`, `player`, `world`. **Pas de DDL** (`CREATE TABLE`, `DROP TABLE`, `ALTER TABLE`, `CREATE ROLE`).

`docker-compose.yml` (`POSTGRES_USER=frog`) crée un **superutilisateur d’instance** pour la démo locale. **Ce n’est pas le profil hébergé.**

## Rôles

| Rôle | Usage | Privilèges |
| --- | --- | --- |
| `frog_runtime` | `Frog.Server` (chaîne `PostgreSql:ConnectionString`) | DML product schemas ; **pas** CREATE sur schéma ; **pas** SUPERUSER |
| `frog_migrate` | `dotnet ef database update` / migrate out-of-band | DDL + DML |
| `frog_publish` | éditeur / publication snapshots | DML `content` + `world` ; SELECT ailleurs |
| `frog_ops` | **recommandé** `Frog.OpsCli` | DML `auth` + `ops` ; SELECT ailleurs. Invite/create ≠ GM |

## Appliquer (hébergé)

Après CREATE DATABASE + migrations (`frog_migrate` ou superuser bootstrap) :

```bash
export FROG_PG_RUNTIME_PASSWORD='…'
export FROG_PG_MIGRATE_PASSWORD='…'
export FROG_PG_PUBLISH_PASSWORD='…'
export FROG_PG_OPS_PASSWORD='…'   # optionnel
./scripts/postgres-apply-roles.sh --connection 'Host=…;Database=frog;Username=ADMIN;Password=…'
```

Overlay local gitignoré : `Username=frog_runtime` (voir `appsettings.Local.json.example`).

CI / `FROG_POSTGRES_TEST_CONNECTION_STRING` reste un admin de test (`frog_test`) pour CREATE DATABASE isolé — pas le contrat runtime.

## Preuve

`PostgresLeastPrivilegeTests` : `CREATE TABLE` / `DROP TABLE` / `CREATE ROLE` refusés sous le rôle runtime ; DML `auth.accounts` OK ; `rolsuper = false`.
