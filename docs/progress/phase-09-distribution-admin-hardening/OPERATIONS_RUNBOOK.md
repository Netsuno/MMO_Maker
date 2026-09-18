# Phase 9 — OPERATIONS_RUNBOOK

**Status:** P9-4 start/stop + config overlay (packaged and from-source). Mute/kick/ban: P9-1 (`ModerateRequest` / slash commands).
**Companion docs:** [`PACKAGING_GUIDE.md`](PACKAGING_GUIDE.md), [`BACKUP_RESTORE_RUNBOOK.md`](BACKUP_RESTORE_RUNBOOK.md), [`SECURITY_MODEL.md`](SECURITY_MODEL.md).

## Prerequisites

| Piece | Notes |
| --- | --- |
| .NET 8 runtime or SDK | Pin **8.0.424** (`global.json`). Framework-dependent publish needs the shared runtime on the host. |
| PostgreSQL 16 | Product SoT (ADR-0002). Dev: `docker compose up -d postgres`. |
| Published world | Production PG start requires ≥1 published map (`PublishedWorldBootstrapHostedService`). |
| Operator grant | Out of band SQL into `auth.operators` — no TCP grant packet. |

MariaDB is **not** required. Leave `MariaDb:enabled=false`.

## Config overlay

Committed `Frog.Server/appsettings.json` (and the copy inside a publish folder) is **not** a production DSN:

- `Server.bindAddress=127.0.0.1`, `port=6000`, `allowNonLoopbackBind=false`
- `PostgreSql.enabled=false`, placeholder password `NOT_A_PRODUCTION_SECRET`
- `MariaDb.enabled=false`

Operator overlay (pick one):

1. Copy `appsettings.Local.json.example` → **gitignored** `appsettings.Local.json` next to the host (repo `Frog.Server/` for `dotnet run`, or the publish directory for a packaged host).
2. Or set `FROG_POSTGRES_CONNECTION_STRING` if the JSON connection string is left empty.

`FrogServerHostFactory` loads `appsettings.json` then `appsettings.Local.json` (optional) from the **content root**. For a packaged host, set the current directory to the publish folder (or `--contentRoot` that folder). `scripts/run-packaged-server.sh` does both.

Editor overlay is a **different JSON shape** (`Frog.Editor/appsettings.Local.json.example`, PascalCase `PostgreSql:ConnectionString`) and is also gitignored. Possession of that string is world-admin (`SECURITY_MODEL.md` §3).

Public bind (`Server.bindAddress` not loopback) requires `Server.allowNonLoopbackBind=true`. There is **no TLS** on `TcpListener`. `PlaceholderSecretPolicy` refuses to start if the bind is public **and** an **enabled** backend DSN still contains a known placeholder (`NOT_A_PRODUCTION_SECRET`, `changeme`, `frog_dev_only`, `VOTRE_MOT_DE_PASSE`, …). Placeholders on a **disabled** backend (MariaDB off while PostgreSQL is on) do not block start.

Do not commit `appsettings.Local.json`. Publish layouts exclude it (`CopyToPublishDirectory=Never`).

## From-source (development)

```bash
docker compose up -d postgres
cp Frog.Server/appsettings.Local.json.example Frog.Server/appsettings.Local.json
# set PostgreSql.enabled=true and the Compose DSN (dev only):
# Host=127.0.0.1;Port=5432;Database=frog;Username=frog;Password=frog_dev_only

dotnet run --project Frog.Server/Frog.Server.csproj
# Windows:
dotnet run --project Frog.Client/Frog.Client.csproj
dotnet run --project Frog.Editor/Frog.Editor.csproj
```

Compose passwords are **not** production secrets.

## Packaged start / stop

Publish first ([`PACKAGING_GUIDE.md`](PACKAGING_GUIDE.md)):

```bash
./scripts/publish-frog.sh --target server-linux-x64
cp artifacts/publish/server-linux-x64/appsettings.Local.json.example \
   artifacts/publish/server-linux-x64/appsettings.Local.json
# edit the Local file: PostgreSql.enabled=true + real DSN
```

Start:

```bash
# foreground (Ctrl+C = ConsoleLifetime stop)
./scripts/run-packaged-server.sh start --dir artifacts/publish/server-linux-x64 --foreground

# or background (log: <dir>/frog-server.log, pid: <dir>/frog-server.pid)
./scripts/run-packaged-server.sh start --dir artifacts/publish/server-linux-x64
```

Windows:

```powershell
./scripts/publish-frog.ps1 -Target server-win-x64 -Force
Copy-Item artifacts/publish/server-win-x64/appsettings.Local.json.example `
          artifacts/publish/server-win-x64/appsettings.Local.json
./scripts/run-packaged-server.ps1 start -Dir artifacts/publish/server-win-x64 -Foreground
```

Raw commands (no script):

```bash
cd artifacts/publish/server-linux-x64
export FROG_SHUTDOWN_FILE="$PWD/.frog-shutdown-request"
./Frog.Server --contentRoot "$PWD"
# or: dotnet Frog.Server.dll --contentRoot "$PWD"
```

**Start sequence:** `Program.Main` loads `Frog.Persistence.PostgreSql.dll` if present → host builds → `Database.Migrate()` on the PG backend → `PublishedWorldBootstrapHostedService` loads published maps → `GameServerService` binds `TcpListener` and logs `ServerStarted`.

**Stop (supported):**

| Method | What happens |
| --- | --- |
| Ctrl+C / SIGTERM | `ConsoleLifetime` → `IHost.StopAsync` → stop accepting → dispose socket → await client handlers → `ServerStopped` |
| `FROG_SHUTDOWN_FILE` appears | `ShutdownFileWatcherService` calls `StopApplication()` (same path). Used by `PackagedServerPostgreSqlProcessTests` and `run-packaged-server.sh stop` |
| `run-packaged-server.sh stop --dir …` | Writes the sentinel and sends SIGTERM; waits 20s |

There is **no** player-drain command yet (P9-5). Stopping drops TCP sessions. Banned accounts cannot reconnect (P9-1 revokes tokens); a kick is not a lasting ban.

Do not `Process.Kill` as the happy path — the packaged-process test treats kill as failure cleanup only.

## Migrations

Automatic: `Database.Migrate()` on server (and editor) start. There is no separate `dotnet ef database update` step for operators.

- Empty database: migrations apply, then the host **fails** if no published map exists. Publish from the editor, or restore a dump **instead of** migrating first (`BACKUP_RESTORE_RUNBOOK.md`: restore onto an empty DB, then start; `PostgresDatabaseHealth` should report 0 pending).
- After P9-1 sanction tables (`ops.account_sanctions` / `ops.moderation_events`), restore proofs must be re-run (P9-3 note).

`scripts/postgres-verify.sh` is the CLI health check (not an HTTP probe). `PostgresDatabaseHealth` is used in tests; there is still no HTTP `/health`.

## Logs

Console only (`Microsoft.Extensions.Logging.Console`). Background start appends to `<publish-dir>/frog-server.log`. No log rotation. No HTTP `/metrics`.

P9-5 process counters (structured log EventId **5030** `ops_metrics`): connections accepted/rejected, rate-limit hits (login/reconnect/chat/movement), PostgreSQL errors, active sessions. Optional JSON snapshot: set `FROG_OPS_METRICS_PATH` (interval `FROG_OPS_METRICS_INTERVAL_SECONDS`, default 15). Load probe: [`LOAD_REPORT.md`](LOAD_REPORT.md) / `./scripts/run-load-harness.sh`.

## First GM

Grant remains SQL (no TCP grant packet). Do not auto-promote the first registered account.

```sql
INSERT INTO auth.operators (account_id, granted_at_utc, granted_by, note)
SELECT id, now(), 'sql', 'initial operator'
FROM auth.accounts
WHERE username = 'your-gm-username';
```

## Mute / kick / ban (P9-1)

Operator commands are `PacketId.ModerateRequest` (78). The server calls `IOperatorDirectory.IsOperatorAsync(session.AccountId)` — a client “I am GM” flag is ignored/absent. From the WinForms client, type in chat:

- `/mute <username> <reason>`
- `/unmute <username> <reason>`
- `/kick <username> <reason>`
- `/ban <username> <reason>`
- `/unban <username> <reason>`

Mute persists in `ops.account_sanctions` (`kind=mute`) and rejects `ChatSend` only. Kick closes the live TCP session and writes `ops.moderation_events` (not a lasting ban). Ban persists (`kind=ban`), revokes reconnect tokens, drops the live session, and rejects login/reconnect. Kick, ban, logout, idle expire, and peer disconnect share one idempotent `SessionTeardown` (player-left to peers, character state save, Phase 8 execution cancel). Repeating kick/ban/disconnect is a no-op, not a double-dispose.

## Backup reminder

Stop or accept a live `player` snapshot, then [`BACKUP_RESTORE_RUNBOOK.md`](BACKUP_RESTORE_RUNBOOK.md) / `scripts/postgres-backup.sh`. Copy dumps off the box. `artifacts/` is gitignored.

## Do not

- Enable `MariaDb:enabled` for a new world
- Point a public bind at Compose / example passwords
- Ship `appsettings.Local.json` inside a zip
- Import `.fcc` / use `Frog.Legacy` as a product path
- Weaken Phase 8 screenshot SHA gates
- Invent a CI URL for this branch until a run targeting `main` exists
