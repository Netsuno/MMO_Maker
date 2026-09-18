# Phase 9 — PACKAGING_GUIDE

**Status:** P9-4 (operator publish layouts). No installer, no store listing, no code-signing.
**SDK pin:** `global.json` → **8.0.424** (`rollForward: latestFeature`).
**Protocol:** `FrogWireProtocol.Version = 10` — packaged client and server **must** be the same generation.
**Proof of start:** `PackagedServerPostgreSqlProcessTests` (job `postgres-integration` in [`.github/workflows/ci.yml`](../../../.github/workflows/ci.yml)). This file does **not** invent a CI run URL.

## What an operator runs

From the repo root, with SDK 8.0.424 on PATH:

```bash
# Linux / CI agent — server that this machine can actually start
./scripts/publish-frog.sh --target server-linux-x64

# Cross-publish Windows layouts from Linux (EnableWindowsTargeting). Do not run the EXEs here.
./scripts/publish-frog.sh --target server-win-x64 --target client-win-x64 --target editor-win-x64 --force

# All four trees
./scripts/publish-frog.sh --target all --force
```

Windows (PowerShell):

```powershell
./scripts/publish-frog.ps1 -Target server-win-x64,client-win-x64,editor-win-x64 -Force
```

Equivalent raw `dotnet publish` (same flags the scripts use):

```bash
dotnet publish Frog.Server/Frog.Server.csproj  -c Release -r linux-x64 --self-contained false -o artifacts/publish/server-linux-x64 -p:PublishSingleFile=false
dotnet publish Frog.Server/Frog.Server.csproj  -c Release -r win-x64   --self-contained false -o artifacts/publish/server-win-x64   -p:PublishSingleFile=false
dotnet publish Frog.Client/Frog.Client.csproj  -c Release -r win-x64   --self-contained false -o artifacts/publish/client-win-x64   -p:PublishSingleFile=false
dotnet publish Frog.Editor/Frog.Editor.csproj  -c Release -r win-x64   --self-contained false -o artifacts/publish/editor-win-x64   -p:PublishSingleFile=false
```

Then copy `appsettings.Local.json` **after** publish (see Config overlay). Zip if you want a drop:

```bash
(cd artifacts/publish && zip -r server-linux-x64.zip server-linux-x64)
```

`artifacts/` is gitignored. Do not commit publish trees or dumps.

## RID matrix

| Layout | Project | TFM | RID | Runnable where | Host file |
| --- | --- | --- | --- | --- | --- |
| `server-linux-x64` | `Frog.Server/Frog.Server.csproj` | `net8.0` | `linux-x64` | Linux x64 + .NET 8 runtime | `Frog.Server` |
| `server-win-x64` | `Frog.Server/Frog.Server.csproj` | `net8.0` | `win-x64` | Windows x64 + .NET 8 runtime | `Frog.Server.exe` |
| `client-win-x64` | `Frog.Client/Frog.Client.csproj` | `net8.0-windows` | `win-x64` | Windows only (WinForms) | `Frog.Client.exe` |
| `editor-win-x64` | `Frog.Editor/Frog.Editor.csproj` | `net8.0-windows` | `win-x64` | Windows only (WinForms + WPF) | `Frog.Editor.exe` |

Rules:

- **Framework-dependent.** Not `--self-contained`. Install the .NET 8 runtime (or the SDK) on the host.
- **Not single-file.** `Frog.Server/Program.cs` `TryLoadPostgreSqlAuthBackend` does `Assembly.LoadFrom(AppContext.BaseDirectory + "Frog.Persistence.PostgreSql.dll")`. A single-file publish would hide that DLL. `Frog.Server/Build/CopyPostgreSqlRuntime.targets` publishes the persistence project as **portable `net8.0`** (it strips the host RID via `RemoveProperties`) and copies those DLLs next to the RID-specific host. Forwarding `linux-x64` / `win-x64` into that class library fails restore (`NETSDK1047`).
- **x64 only** (`PlatformTarget=x64` on the three executables).
- Linux agents can **produce** the Windows layouts (`Directory.Build.props` `EnableWindowsTargeting=true`) but cannot **launch** WinForms/WPF. From-source editor / gameplay / Phase 8 smokes remain the `windows-latest` jobs in `ci.yml` (including `scripts/verify-phase8-screenshot-manifest.ps1`). Those smokes do **not** launch `publish-frog.ps1` output.

## Output tree

Default root: `artifacts/publish/<layout>/`. Each tree also gets `packaging-manifest.json` (SDK, RID, protocol v10, git SHA, UTC time).

### Server (`server-linux-x64` / `server-win-x64`)

Required (script-enforced):

| File | Why |
| --- | --- |
| `Frog.Server` or `Frog.Server.exe` | RID apphost |
| `Frog.Server.dll` | managed entry |
| `Frog.Server.runtimeconfig.json` / `Frog.Server.deps.json` | shared-framework probe |
| `Frog.Persistence.PostgreSql.dll` | runtime auth / world backend |
| `Npgsql.dll`, `Npgsql.EntityFrameworkCore.PostgreSQL.dll`, `Microsoft.EntityFrameworkCore.dll`, `Microsoft.EntityFrameworkCore.Relational.dll`, `EFCore.NamingConventions.dll` | EF/Npgsql stack copied by `CopyPostgreSqlRuntime.targets` |
| `appsettings.json` | committed defaults (PG **off**, bind `127.0.0.1:6000`, placeholder DSN) |
| `appsettings.Local.json.example` | overlay template |

**Must not** be in the published tree: `appsettings.Local.json`. `Frog.Server.csproj` sets `CopyToPublishDirectory=Never` for that file. The publish script fails if it is present (secret leak). Copy the overlay **after** publish, on the machine that runs the host.

Leftover, not required: `Database/schema_frog_mariadb_v1.sql` may appear because the csproj still copies the frozen MariaDB schema. Leave `MariaDb:enabled=false`. No MariaDB redistributable is packaged.

### Client (`client-win-x64`)

`Frog.Client.exe` + `Frog.Client.dll` + `Frog.Core.dll` + `Frog.Application.dll`. No `appsettings` — the shell defaults to `127.0.0.1:6000` (`Frog.Client/MainShellForm.cs`). Tilesets/maps arrive from the server; there is no extra asset zip.

### Editor (`editor-win-x64`)

`Frog.Editor.exe` + `Frog.Persistence.PostgreSql.dll` + `appsettings.Local.json.example`. Overlay uses **PascalCase** `PostgreSql:ConnectionString` (`Frog.Editor/appsettings.Local.json.example`) — different shape than the server file. Asset root defaults to `<exe>/Assets` or `FROG_PROJECT_ASSET_ROOT` (`ProjectAssetRoot`). Playtest looks for `Frog.Server` / `Frog.Client` under the repo `bin/` tree or a remembered path (`EditorFrogServerLauncher`, `EditorFrogClientLauncher`); a packaged editor on a machine without the repo must be pointed at the packaged server/client EXEs.

## Config overlay (packaged server + PostgreSQL)

Same model as development (`SECURITY_MODEL.md` §6):

| File | In git? | In the zip? | Role |
| --- | --- | --- | --- |
| `appsettings.json` | yes | yes | Safe defaults. `PostgreSql.enabled=false`, `MariaDb.enabled=false`, bind loopback, password `NOT_A_PRODUCTION_SECRET` |
| `appsettings.Local.json.example` | yes | yes | Template (`VOTRE_MOT_DE_PASSE`) |
| `appsettings.Local.json` | **no** (`.gitignore` `**/appsettings.Local.json`) | **no** | Real DSN. Operator-created |
| Env `FROG_POSTGRES_CONNECTION_STRING` | n/a | n/a | Used if `PostgreSql:ConnectionString` is empty (`FrogServerHostFactory`) |

`Host.CreateDefaultBuilder` plus the factory reload JSON from the **content root**. Supported operator path: **current directory = publish folder** (or pass `--contentRoot <that folder>`). `scripts/run-packaged-server.sh` does both.

To run a packaged server with PostgreSQL enabled:

1. `./scripts/publish-frog.sh --target server-linux-x64` (or the Windows RID).
2. `cp artifacts/publish/server-linux-x64/appsettings.Local.json.example artifacts/publish/server-linux-x64/appsettings.Local.json`
3. Edit Local: `PostgreSql.enabled=true`, real `connectionString`, keep `MariaDb.enabled=false`, keep `Server.bindAddress=127.0.0.1` unless `allowNonLoopbackBind=true` (clear-text TCP; `PlaceholderSecretPolicy` refuses a public bind when an **enabled** backend still has a known placeholder; disabled MariaDB placeholders are ignored).
4. PostgreSQL 16 must exist. Dev Compose: `docker compose up -d postgres` (user `frog`, password `frog_dev_only`, db `frog` — **not** production).
5. Start (see [`OPERATIONS_RUNBOOK.md`](OPERATIONS_RUNBOOK.md)):
   ```bash
   ./scripts/run-packaged-server.sh start --dir artifacts/publish/server-linux-x64 --foreground
   ```
6. First start runs `Database.Migrate()` (automatic). Production composition **requires at least one published map** (`PublishedWorldBootstrapHostedService`). An empty migrated database will exit with that error — publish a map from the editor, or restore a dump (`BACKUP_RESTORE_RUNBOOK.md`).
7. Stop: Ctrl+C / SIGTERM, or `./scripts/run-packaged-server.sh stop --dir …` (writes `FROG_SHUTDOWN_FILE`, same path as `ShutdownFileWatcherService`).

Do not set `PostgreSql:allowInMemoryFallback=true` on a hosted world. Do not enable `MariaDb:enabled`.

## Version stamp / compatibility

`packaging-manifest.json` records `protocolVersion` (from `FrogWireProtocol.Version`), SDK, RID, and `gitSha`. Ship matching client + server trees. Mixing a v10 client with an older server is unsupported.

## Smoke / gates (do not invent URLs)

| What | How | Where it runs |
| --- | --- | --- |
| Layout files + PG DLL next to host | `./scripts/publish-frog.sh` / `packaged-server-smoke.sh --layout-only` | Any SDK host; CI step on `postgres-integration` |
| Packaged process starts with PG, login + shop persist, graceful stop | `PackagedServerPostgreSqlProcessTests` | Ubuntu `postgres-integration` (needs `FROG_POSTGRES_TEST_CONNECTION_STRING`) |
| From-source WinForms editor / client smokes | Existing `windows-latest` editor / gameplay / Phase 8 smokes ×3 | `dotnet test` of `tests/Frog.Editor.WindowsSmokeTests` — **not** `artifacts/publish/client-win-x64` / `editor-win-x64` |
| Packaged `Frog.Client.exe` / `Frog.Editor.exe` from `publish-frog.ps1` actually launched | **Not proven.** Linux agents cannot run WinForms. No CI step starts the publish-layout EXEs. | Honest residual: layout publish only |

```bash
./scripts/packaged-server-smoke.sh --layout-only
# with a disposable PG:
export FROG_POSTGRES_TEST_CONNECTION_STRING='Host=127.0.0.1;Port=5432;Database=frog_test;Username=frog_test;Password=frog_test_local_only'
./scripts/packaged-server-smoke.sh
```

This guide does **not** claim that `publish-frog.ps1` client/editor packages have been launched. Server packaging has Linux proofs (`PackagedServerPostgreSqlProcessTests` + layout-only smoke). Windows CI smokes prove from-source test hosts, not the published RID trees.

## Out of scope

- Store / code-signing / MSI / ClickOnce
- Docker image for `Frog.Server` or MariaDB
- `.fcc` importer / `Frog.Legacy`
- Mute / kick / ban (P9-1)
- Weakening Phase 8 screenshot SHA gates
