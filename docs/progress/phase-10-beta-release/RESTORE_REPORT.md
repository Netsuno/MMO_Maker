# Phase 10 — RESTORE_REPORT (P10-7)

**Pas une gate.** Pas de `PHASE 10 GATE REACHED`. Campagne restore **démo / CI**, pas un dump production.

## Commandes reproductibles

```bash
export FROG_POSTGRES_TEST_CONNECTION_STRING='Host=127.0.0.1;Port=5432;Database=frog_test;Username=frog_test;Password=frog_test_local_only'
dotnet test tests/Frog.Persistence.IntegrationTests/Frog.Persistence.IntegrationTests.csproj \
  -c Release \
  --filter "FullyQualifiedName~.Phase10BackupRestoreRowsTests"
```

Le test :

1. Crée deux bases jetables (`frog_p107s_*` / `frog_p107d_*`).
2. Migre + seed Phase 7, puis insère des **lignes réelles** : comptes, persos, or, inventaire, banque, guilde + membres, amitié acceptée, blocage, `player.trade_executions` + replay `trade.commit`, mute + ban, grant opérateur.
3. `scripts/postgres-backup.sh` (`pg_dump -Fc`) → `scripts/postgres-restore.sh` (`pg_restore`) sur la base destination.
4. Asserts EF/SQL sur la base restaurée (guildes, amis, blocs, ledger d’échange, sanctions, or/piles).
5. Publie `server-linux-x64` hors dépôt (`/tmp/frog-p107-pkg-*`) et démarre **`Frog.Server` publié** contre la base restaurée.
6. TCP Hello + login du compte sain → OK ; login du compte banni → `Compte banni.`

## PROUVÉ (CI `postgres-integration`)

| Ligne | Preuve |
| --- | --- |
| Comptes / persos / or / banque objet | seed + asserts post-restore |
| Guildes + membres | `player.guilds` / `guild_members` |
| Amis acceptés | `player.friendships` status `accepted` |
| Blocage | `player.character_blocks` |
| Échange commité + replay `request_id` | `player.trade_executions` + `TryReplayAsync` |
| Mute + ban | `ops.account_sanctions` ; serveur publié refuse le login banni |
| Serveur **publié** lit la base restaurée | process `Frog.Server` (pas seulement l’hôte in-process) |

## RESTANT

- Chiffrement des dumps, rétention 7 versions, restore hors répertoire d’exécution opérateur : runbook Phase 9, **non** automatisé ici.
- Volume / durée ≤ 30 min sur un dump « gros monde » : **non mesuré** (bases jetables CI).
- Crash pendant mutations au moment du dump : **absent**.
- Mode maintenance / drain : stub `MaintenanceService.cs`.
- Recette humaine 2 machines après restore : P10-4, pas ce rapport.
