# État du projet FRoG

- Dernière mise à jour : 2026-08-22 03:50 UTC
- Branche / commit : `cursor/phase0-baseline-audit-02c7`
- Phase active : **Phase 3 partielle faite** ; **Task 4 PostgreSQL bloquée (décision humaine)**
- Environnement : Ubuntu 24.04, .NET SDK 8.0.424

## Vérifié comme fonctionnel

- Build/test C# 12 — **102 tests** verts
- Frontières d’architecture (Core / Legacy / pas de cycles)
- Inventaire VB6 régénéré
- Spec `.fcc` + fixtures map1–3
- `Frog.Legacy.LegacyFccMapReader` : header, 31×31, warps/blocks, rapport SHA-256, rejet tronqué

## Implémenté, mais non vérifié

- Éditeur / client WinForms ; MariaDB réelle ; E2E TCP
- Trailer NPC/Pano/Fog des `.fcc` ; packing String/*Set des 88 octets tuile
- Couches Anim/Mask3/Fringe3 (signalées en warning)

## En cours

- Aucune (attente décision Task 4, ou poursuite Task 10 hors PG / polish reader)

## Bloqueurs — décision humaine requise

**Task 4 — persistance :** le PRD impose **PostgreSQL** ; le dépôt a une stack **MariaDB** (migrations v1–v10, repos, éditeur). Options :

1. Migrer vers PostgreSQL (EF Core / Npgsql) comme source de vérité  
2. Conserver MariaDB et amender le PRD  
3. Dual-run temporaire avec plan explicite  

Sans ce choix, les repositories PostgreSQL du PRD ne doivent pas être improvisés.

## Commandes de validation

```bash
dotnet restore Frog.Creator.sln
dotnet build Frog.Creator.sln -c Release --no-restore
dotnet test Frog.Tests/Frog.Tests.csproj -c Release --no-build
```

## Résultat de la dernière validation

- Build : **PASS**
- Tests : **102 réussis, 0 échoués, 0 ignorés**
- Intégration PostgreSQL : **NOT RUN** (bloqué)
- E2E : **NOT RUN**

## Prochaine tâche

- Après décision DB : Task 4 (santé PG ou ADR MariaDB)  
- Sinon : enrichir `LegacyFccMapReader` (trailer NPC, String/*Set) + golden masters attendus dans `fixtures/expected/`
