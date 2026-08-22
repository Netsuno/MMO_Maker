# Architecture — frontières actuelles vs cible PRD

## Graphe actuel (vérifié)

```text
Frog.Editor ──► Frog.Core
Frog.Client ──► Frog.Core
Frog.Server ──► Frog.Core
Frog.Legacy ──► Frog.Core
Frog.Tests  ──► Frog.Core, Frog.Server, Frog.Legacy
```

Packages notables :

| Projet | Packages UI / DB / host |
| --- | --- |
| Frog.Core | aucun |
| Frog.Client | WinForms (TFM `net8.0-windows`) |
| Frog.Editor | WinForms + WPF + **MySqlConnector** |
| Frog.Server | Hosting/Logging/Config + **MySqlConnector** |
| Frog.Tests | xUnit |

## Règles appliquées maintenant (tests d’architecture)

1. `Frog.Core` ne référence aucun autre projet FRoG.
2. `Frog.Core` ne référence pas MySqlConnector, Npgsql, EF Core, System.Windows.Forms, PresentationFramework.
3. Aucune dépendance circulaire entre projets de la solution.
4. Les fichiers sous `Frog.Editor/Forms/` et le code-behind WPF principal n’instancient pas `MySqlConnection` / `DbContext` directement (accès DB via services).

## Écarts connus vs PRD (non corrigés ici)

| Cible PRD | Écart |
| --- | --- |
| `Frog.Application`, `Frog.Protocol`, `Frog.Legacy`, `Frog.Rendering`, `Frog.Persistence.PostgreSql` | Absents |
| PostgreSQL / EF Core | MariaDB + SQL manuel |
| Formulaires → ports applicatifs uniquement | Editor Services utilisent encore MySqlConnector (dette acceptée jusqu’à décision PG) |
| Protocol hors Core | Types sous `Frog.Core/Protocol/` |

Voir `docs/BASELINE_AUDIT.md` et le bloqueur MariaDB vs PostgreSQL dans `docs/STATUS.md`.
