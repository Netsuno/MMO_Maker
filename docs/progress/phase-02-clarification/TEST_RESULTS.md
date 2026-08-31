# Résultats de tests — Phase 02

Commit testé : 3604bd7a5146ab66ddd44e8d28314cbcea41eaaa  
Environnement : Ubuntu 24.04, SDK 8.0.424, PostgreSQL 16.15 local (`frog_test` / user test local).

| Commande | Environnement | Résultat | Détail | Ignorés | Artefact | Commit |
| --- | --- | --- | --- | --- | --- | --- |
| `dotnet restore Frog.Creator.sln` | Linux | PASS | implicite via build | — | aucun | HEAD |
| `dotnet build Frog.Creator.sln -c Release` | Linux, C# 12 | PASS | 0 warning, 0 error | — | `/tmp/p2-build.txt` | HEAD |
| `dotnet test Frog.Tests/Frog.Tests.csproj -c Release --no-build` | Linux | PASS | 104 réussis, 0 échoués, 0 ignorés, ~83 ms | aucun | `/tmp/p2-unit.txt` | HEAD |
| `dotnet test Frog.Tests … --filter ArchitectureBoundaryTests` | Linux | PASS | 8 réussis | aucun | `/tmp/p2-arch.txt` | HEAD |
| `dotnet test tests/Frog.Persistence.IntegrationTests/… -c Release --no-build` | Linux + PG 16 | PASS | 6 réussis, ~458 ms | aucun | `/tmp/p2-pg.txt` | HEAD |
| Smoke UI Windows éditeur | — | **NOT RUN** | Agent Linux sans runtime WinForms exécutable | — | — | — |
| E2E client-serveur | — | **NOT RUN** | Hors Phase 2 | — | — | — |
| Filtre MariaDB | — | **NOT RUN** | Variable absente ; non bloquant | — | — | — |

## Scan Phase 2

- Nouveaux `NotImplementedException` dans Application/Persistence : aucun ajout Phase 2.
- Tests `Skip` critiques : aucun dans la suite exécutée.
- Documents actifs : import `.fcc` uniquement comme **hors backlog** / différé (pas condition de livraison).
