# État du projet FRoG

- Dernière mise à jour : 2026-08-22 03:20 UTC
- Branche / commit : `cursor/phase0-baseline-audit-02c7`
- Phase active : **Phase 2 — Task 6 (structures carte VB6)** ; Task 4 PostgreSQL toujours bloquée
- Environnement : Ubuntu 24.04, .NET SDK 8.0.424

## Vérifié comme fonctionnel

- Build/test C# 12 (91 tests architecture + métier)
- Inventaire VB6 régénéré depuis upstream (Forms/Modules/Classes = 105/44/8 ; +3 `.ctl`)

## Implémenté, mais non vérifié

- Éditeur / client WinForms ; MariaDB réelle ; E2E TCP

## En cours

- Une seule tâche : **Task 6 — extraire structures/enums de carte depuis `modTypes` / `modDatabase`**

## Bloqueurs

- **Task 4 — décision humaine :** PostgreSQL (PRD) vs MariaDB (dépôt). Options : (1) migrer PG, (2) amender PRD pour MariaDB, (3) dual-run planifié.
- Fixtures binaires `.map` VB6 non encore collectées (Task 7)
- WinForms non exécutable sur cet agent Linux

## Commandes de validation

```bash
dotnet restore Frog.Creator.sln && dotnet build Frog.Creator.sln -c Release --no-restore && dotnet test Frog.Tests/Frog.Tests.csproj -c Release --no-build
```

## Résultat de la dernière validation

- Build : **PASS**
- Tests : **91 réussis**
- Intégration PostgreSQL : **NOT RUN** (bloqué)
- E2E : **NOT RUN**

## Prochaine tâche

- Extraire `MapRec` / couches / attributs / `Data1..3` depuis les sources VB6 clonées ; rédiger le début de `docs/LEGACY_FORMATS.md`.
