# État du projet FRoG

- Dernière mise à jour : 2026-08-22 03:12 UTC
- Branche / commit : `cursor/phase0-baseline-audit-02c7`
- Phase active : **Phase 1 — Task 4 bloquée (PostgreSQL) ; Task 5 suivante (inventaire VB6)**
- Dernier commit vert : architecture boundaries + suite tests
- Environnement : Ubuntu 24.04, .NET SDK 8.0.424, PostgreSQL absent, MariaDB non démarrée, Docker absent

## Vérifié comme fonctionnel

- Build/test C# 12 sans preview (`Directory.Build.props`)
- `ArchitectureBoundaryTests` (Core pur, pas de cycles, UI éditeur sans SQL direct, Client sans packages DB)
- Suite `Frog.Tests` complète après Task 3
- Docs : `BASELINE_AUDIT`, `STATUS`, `TESTING`, `ARCHITECTURE`, `ADR-0001`

## Implémenté, mais non vérifié

- Éditeur / client WinForms
- Persistance MariaDB réelle
- E2E TCP

## En cours

- Une seule tâche : **Task 5 — régénérer l’inventaire depuis la source VB6** (Task 4 en attente de décision humaine)

## Bloqueurs

- **Décision requise — Task 4 :** PRD = PostgreSQL ; dépôt = MariaDB mature. Options :
  1. Migrer vers PostgreSQL (EF Core / Npgsql) comme source de vérité ;
  2. Conserver MariaDB et amender le PRD ;
  3. Dual-run temporaire (non recommandé sans plan).
- Sources VB6 absentes du dépôt (clonage upstream prévu pour Task 5)
- Pas de runtime WinForms sur cet agent Linux

## Commandes de validation

```bash
dotnet restore Frog.Creator.sln
dotnet build Frog.Creator.sln -c Release --no-restore
dotnet test Frog.Tests/Frog.Tests.csproj -c Release --no-build
```

## Résultat de la dernière validation

- Build : **PASS**
- Tests : exécuter après commit Task 3 (cible ≥ 91)
- Intégration PostgreSQL : **NOT RUN** / bloqué
- E2E : **NOT RUN**

## Prochaine tâche

- Cloner / référencer `Alexoune001/FRoG-Creator-OSE-V0.6.3`, régénérer inventaire des unités, comparer aux 157 annoncées, produire rapport d’écart.

## Référence

- [`docs/BASELINE_AUDIT.md`](BASELINE_AUDIT.md)
- [`docs/ARCHITECTURE.md`](ARCHITECTURE.md)
- [`docs/decisions/ADR-0001-project-boundaries.md`](decisions/ADR-0001-project-boundaries.md)
