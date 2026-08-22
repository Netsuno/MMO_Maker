# État du projet FRoG

- Dernière mise à jour : 2026-08-22 03:05 UTC
- Branche / commit : `cursor/phase0-baseline-audit-02c7` (à jour après Task 2)
- Phase active : **Phase 1 — Task 3 (frontières de projets)**
- Dernier commit vert local : build+tests C# 12 sans preview (voir validation ci-dessous)
- Environnement : Ubuntu 24.04, .NET SDK 8.0.424, PostgreSQL absent, MariaDB non démarrée, Docker absent

## Vérifié comme fonctionnel

- `dotnet restore/build/test` **sans** `LangVersion=preview` (C# 12.0 dans `Directory.Build.props`)
- `EnableWindowsTargeting` ancré pour agents Linux
- Suite `Frog.Tests` : **86 réussis** (dont régression MapRequest fingerprint / stats copy)
- Audit phase 0 : `docs/BASELINE_AUDIT.md`, `docs/STATUS.md`, `docs/TESTING.md`

## Implémenté, mais non vérifié

- Éditeur / client WinForms (non exécutés sur Linux)
- Persistance MariaDB réelle (test intégration no-op sans env)
- E2E TCP client↔serveur

## En cours

- Une seule tâche : **Task 3 — établir / documenter les frontières de projets + test d’architecture**

## Bloqueurs

- **Produit :** PRD impose PostgreSQL ; le dépôt utilise MariaDB — décision humaine avant Task 4
- Sources VB6 / fixtures `.map` absentes (bloque Phase 2+)
- Pas de runtime WinForms sur cet agent Linux

## Commandes de validation

```bash
dotnet restore Frog.Creator.sln
dotnet build Frog.Creator.sln -c Release --no-restore
dotnet test Frog.Tests/Frog.Tests.csproj -c Release --no-build
```

Voir aussi `docs/TESTING.md`.

## Résultat de la dernière validation

- Build : **PASS** (0 warning, 0 error, C# 12)
- Tests : **86 réussis, 0 échoués, 0 ignorés**
- Intégration PostgreSQL : **NOT RUN**
- Intégration MariaDB : **NOT RUN**
- E2E : **NOT RUN**

## Prochaine tâche

- Task 3 : graphe de dépendances documenté + test d’architecture (Core sans UI/DB/sockets ; pas de DbContext/Npgsql/MySql dans formulaires).

## Référence

- [`docs/BASELINE_AUDIT.md`](BASELINE_AUDIT.md)
- [`docs/TESTING.md`](TESTING.md)
