# Architecture — MMO Maker

## Autorité produit

PRD `PRD_MMO_Maker_CSharp.md` v2.1 + ADR-0002 (PostgreSQL) + ADR-0003 (aucune compatibilité FRoG) + ADR-0004 (coque WPF temporaire).

## Graphe actuel

```text
Frog.Editor ──► Frog.Core          (+ MySqlConnector héritage)
Frog.Client ──► Frog.Core
Frog.Server ──► Frog.Core          (+ MySqlConnector héritage)
Frog.Legacy ──► Frog.Core          (expérimental / différé)
Frog.Application ──► Frog.Core
Frog.Persistence.PostgreSql ──► Frog.Application, Frog.Core
Frog.Tests ──► Core, Server, Legacy
Frog.Persistence.IntegrationTests ──► Persistence, Application, Core
```

## Règles

1. `Frog.Core` : domaine pur (pas UI, pas DB, pas sockets).
2. `Frog.Application` : ports uniquement → Core.
3. Persistence PostgreSQL : pas de référence Editor/Client/Server.
4. Editor/Client/Server : **ne référencent pas** `Frog.Legacy`.
5. Formulaires / code-behind : pas de `DbContext` / `NpgsqlConnection` / `MySqlConnection` (accès via services/ports).
6. Aucune nouvelle fonctionnalité MariaDB.

## UI éditeur

- Coque WPF + îlots WinForms (ADR-0004).
- Cible produit WinForms ; pas d’extension WPF hors panneaux existants.

## Hors chemin critique

- `Frog.Legacy`, fixtures `.fcc`, docs `LEGACY_FORMATS` (référence historique seulement).
