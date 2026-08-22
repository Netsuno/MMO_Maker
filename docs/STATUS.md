# État du projet — MMO Maker

- Dernière mise à jour : 2026-08-22 04:40 UTC
- Branche / commit : `cursor/phase0-baseline-audit-02c7` / 3604bd7a5146ab66ddd44e8d28314cbcea41eaaa
- **Phase 2 — Clarification produit : READY FOR REVIEW**
- Prochaine phase **non commencée** : Phase 3 — Shell éditeur
- Rapport : [`docs/progress/phase-02-clarification/REVIEW_REQUEST.md`](progress/phase-02-clarification/REVIEW_REQUEST.md)
- Environnement audit : Ubuntu 24.04, .NET 8.0.424, PostgreSQL 16.15

## Vérifié comme fonctionnel

- Build C# 12 + tests unitaires `Frog.Tests`
- Intégration PostgreSQL (migrations, santé, round-trip, conflit, rollback, import ops)
- CI : job Windows unitaires + job Ubuntu PostgreSQL
- ADR-0002 / 0003 / 0004 ; matrice MariaDB ; backlog sans import `.fcc`

## Implémenté, mais non vérifié

- Éditeur (coque WPF + WinForms) — UI non exécutée sur Linux
- Client WinForms ; E2E TCP
- Runtime MariaDB optionnel (héritage)

## Différé (hors chemin critique)

- `Frog.Legacy` / fixtures `.fcc` (expérimental)

## Bloqueurs

- Smoke UI Windows requis pour Phase 3 gate (environnement Linux insuffisant)

## Commandes de validation

```bash
dotnet restore Frog.Creator.sln && dotnet build Frog.Creator.sln -c Release --no-restore
dotnet test Frog.Tests/Frog.Tests.csproj -c Release --no-build
export FROG_POSTGRES_TEST_CONNECTION_STRING='Host=127.0.0.1;Port=5432;Database=frog_test;Username=frog_test;Password=frog_test_local_only'
dotnet test tests/Frog.Persistence.IntegrationTests/Frog.Persistence.IntegrationTests.csproj -c Release
```

## Résultat de la dernière validation

- À renseigner dans `docs/progress/phase-02-clarification/TEST_RESULTS.md`

## Prochaine phase proposée (non commencée)

- Phase 3 : shell éditeur (arbre / canvas / panneaux / status) inspiré RPG Maker
