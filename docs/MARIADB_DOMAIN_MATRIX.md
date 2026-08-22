# MariaDB — état par domaine (héritage)

Source de vérité opérationnelle du **nouveau produit** : **PostgreSQL** (ADR-0002).  
MariaDB = runtime historique encore branché ; **aucune nouvelle fonctionnalité MariaDB**.

| Domaine | Projet | Composants | Statut | Plan |
| --- | --- | --- | --- | --- |
| Cartes (blob `.fmap`) | Server, Editor | `MariaDbMapBlobStore`, `MariaMapBlobPublisher` | Héritage actif si `MariaDb.enabled` | Remplacer par `IMapRepository` PG ; retirer double écriture |
| Événements carte | Server, Editor | `MariaDbMapEventStore`, `MapEventsMariaDb*` | Héritage | Reporter vers tables PG `world` / contenu après MVP cartes |
| Comptes / auth | Server | `MariaDbAccountRepository` | Héritage | Migrer schéma `auth` PG (Phase gameplay) |
| Personnages / stats / flags | Server | `MariaDbCharacter*`, `MariaDbPlayerStateStore` | Héritage | Migrer schéma `player` PG |
| Inventaire | Server | migrations MariaDB v7+ | Héritage | Migrer avec tranche inventaire |
| Schema bootstrap | Server | `MariaDbSchemaBootstrap`, V2–V10 | Héritage | Gel : pas de V11+ MariaDB |
| Tests | Frog.Tests | `MariaDbSchemaIntegrationTests` | Env-gated, non bloquant | Conserver jusqu’à retrait runtime |
| Package | Editor, Server | `MySqlConnector` 2.4.0 | Héritage | Retirer quand plus aucun appel |

**Interdit :** nouvelle table MariaDB, nouvelle dépendance MySqlConnector, double écriture PG+MariaDB pour une même fonctionnalité.

**PostgreSQL CI :** job `postgres-integration` dans `.github/workflows/ci.yml` (Ubuntu + service Postgres 16) — **requis**.
