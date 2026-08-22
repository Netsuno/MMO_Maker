# Architecture — frontières actuelles vs cible PRD

## Graphe actuel (vérifié)

```text
Frog.Editor ──► Frog.Core
Frog.Client ──► Frog.Core
Frog.Server ──► Frog.Core
Frog.Legacy ──► Frog.Core
Frog.Application ──► Frog.Core
Frog.Persistence.PostgreSql ──► Frog.Application, Frog.Core
Frog.Tests ──► Frog.Core, Frog.Server, Frog.Legacy
Frog.Persistence.IntegrationTests ──► Persistence, Application, Core
```

Packages notables :

| Projet | Packages UI / DB / host |
| --- | --- |
| Frog.Core | aucun |
| Frog.Application | aucun |
| Frog.Persistence.PostgreSql | EF Core 8, Npgsql, NamingConventions |
| Frog.Client | WinForms (TFM `net8.0-windows`) |
| Frog.Editor | WinForms + WPF + MySqlConnector (héritage) |
| Frog.Server | Hosting/Logging/Config + MySqlConnector (héritage) |
| Frog.Tests | xUnit |

## Règles appliquées (tests d’architecture)

1. `Frog.Core` ne référence aucun autre projet FRoG.
2. `Frog.Core` ne référence pas MySqlConnector, Npgsql, EF Core, WinForms, WPF.
3. `Frog.Application` ne référence que `Frog.Core`.
4. `Frog.Persistence.PostgreSql` ne référence pas Editor/Client/Server.
5. Aucune dépendance circulaire.
6. Surfaces UI éditeur : pas de `MySqlConnection` / `DbContext` / `FrogDbContext` directs.

## Écarts restants vs PRD

| Cible PRD | Écart |
| --- | --- |
| `Frog.Protocol`, `Frog.Rendering` | Absents |
| MariaDB dans Editor/Server | Héritage temporaire (ADR-0002) |
| Protocol hors Core | Types sous `Frog.Core/Protocol/` |
