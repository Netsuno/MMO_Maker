# ADR-0002 — PostgreSQL comme source de vérité

- Statut : accepté
- Date : 2026-08-22
- Décision produit : option **1** (migrer vers PostgreSQL)

## Contexte

Le PRD impose PostgreSQL. Le dépôt contenait une persistance MariaDB (MySqlConnector, SQL manuel, migrations v1–v10). Le 22 août 2026, le responsable a choisi de **migrer vers PostgreSQL** plutôt que d’amender le PRD.

## Décision

- **Source de vérité opérationnelle :** PostgreSQL 16, EF Core 8, fournisseur Npgsql.
- Schéma créé **uniquement** par migrations versionnées dans `Frog.Persistence.PostgreSql`.
- Ports applicatifs dans `Frog.Application` ; aucun `DbContext` / `NpgsqlConnection` dans les formulaires.
- `Frog.Core` reste sans dépendance base de données.
- MariaDB / MySqlConnector : **héritage temporaire** (serveur/éditeur existants). Pas de nouvelles tables MariaDB. Retrait progressif après parité des cas d’usage carte, puis comptes.

## Conséquences

- `docker-compose.yml` démarre PostgreSQL de développement.
- Tests d’intégration : base isolée par exécution, variable `FROG_POSTGRES_TEST_CONNECTION_STRING`.
- Identifiants Compose / tests sont **locaux uniquement**, jamais des secrets de production.
