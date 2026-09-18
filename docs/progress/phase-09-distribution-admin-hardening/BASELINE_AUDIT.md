# Phase 9 — Baseline audit (P9-0)

**Audited tip:** `5af47b9cf6ba18a82dba5eee933fc1d0e6afa3eb`  
**Branch:** `cursor/phase9-distribution-admin-hardening` (created from that `main` tip)  
**Date:** 2026-09-17  
**Repo:** https://github.com/Netsuno/MMO_Maker  

This is a **new** Phase 9 audit. Do not treat [`docs/BASELINE_AUDIT.md`](../../BASELINE_AUDIT.md) as current — that file is the Phase 0 snapshot of `6df9f55` (2026-08-22) when PostgreSQL, Application, and Phase 7–8 did not exist.

**CI:** this branch tip has **no CI run of its own**. Workflow [`.github/workflows/ci.yml`](../../../.github/workflows/ci.yml) fires on `push` to `main`/`master` and on pull requests targeting those branches only. Baseline **main** CI at the same SHA: https://github.com/Netsuno/MMO_Maker/actions/runs/35286923042 SUCCESS (README merge PR #6). Prior main SUCCESS on Phase 8 merge `1cd57ba`: https://github.com/Netsuno/MMO_Maker/actions/runs/35285230766. Do not invent a CI URL for later commits on this branch until a run exists.

---

## 1. Repo structure / components delivered

Solution: `Frog.Creator.sln` (11 projects).

| Project | TFM | Role on this tip |
| --- | --- | --- |
| `Frog.Core` | `net8.0` | Domain, `.fmap`, `FrogWireProtocol` v10, Phase 7/8 wire, password hasher, event planner |
| `Frog.Application` | `net8.0` | Ports, map/content sessions, playtest launcher, identity rules |
| `Frog.Persistence.PostgreSql` | `net8.0` | EF Core 8 / Npgsql, 24 Up migrations, product repositories |
| `Frog.Server` | `net8.0` exe | Authoritative TCP host (`Program.cs` → `FrogServerHostFactory` → `GameServerService`) |
| `Frog.Client` | `net8.0-windows` WinExe | WinForms player (`MainShellForm` + `FrogGameClient`) |
| `Frog.Editor` | `net8.0-windows` WinExe | Map + content editors; temporary WPF shell (ADR-0004) |
| `Frog.Legacy` | `net8.0` | Experimental `.fcc` reader — **out of product path** (ADR-0003) |
| `Frog.Tests` | `net8.0` | Unit / in-memory (412 PASS on Phase 8 acceptance; not re-counted in P9-0) |
| `tests/Frog.Persistence.IntegrationTests` | `net8.0` | Isolated PostgreSQL (174 PASS on Phase 8 acceptance) |
| `tests/Frog.Editor.WindowsSmokeTests` | `net8.0-windows` | Editor / gameplay / Phase 8 UI smokes (Windows CI) |
| `tests/Frog.PlaytestHeadlessClient` | `net8.0` | Headless playtest client |

**Delivered product (Phases 2–8 on main):**

- Editor workspace, map MVP, playtest (Phases 3–5)
- Content editors: tilesets, NPCs, items, spells, classes, shops, resources (Phase 6)
- Authoritative gameplay: auth, characters, movement, melee, inventory, equipment, ground, shop, bank, chat Global/Map/Whisper (Phase 7)
- Typed map events, quests, dialogues, craft, professions, regions/weather, Interact `activationId` v10 (Phase 8)

**Not delivered (relevant to Phase 9):**

- Mute / kick / ban, operator roles (stubs only)
- TLS / public bind hardening
- Backup / restore scripts
- Installer or publish layout
- Metrics / load certification
- Guilds / groups / P2P trades (P9-S **DEFERRED**)

Graph matches [`docs/ARCHITECTURE.md`](../../ARCHITECTURE.md). Editor/Client/Server do not reference `Frog.Legacy`. Server loads `Frog.Persistence.PostgreSql.dll` at runtime via `Program.TryLoadPostgreSqlAuthBackend`.

---

## 2. Real TODOs / debt (distinguish historical)

Counted on this tip: **106** `.cs` files whose first line is `// TODO: Implémenter`; **128** `TODO` matches in `*.cs`.

### Historical skeleton (do **not** treat as Phase 9 work)

Unused placeholder files left from the original project layout. Gameplay does **not** use them (Phase 7/8 known issues already said this):

- `Frog.Client/Models/*`, `Frog.Client/Services/*`, `Frog.Client/Controls/*`, `Frog.Client/Forms/OptionsForm.cs`, `DialogForm.cs`, `Frog.Client/Network/PacketReader.cs` / `PacketWriter.cs` / `NetworkService.cs`
- `Frog.Server/Models/Guild.cs`, `Map.cs`, `Player.cs`, `Quest.cs`, `Role.cs`, `Permission.cs`, … and `Frog.Server/Services/GuildService.cs`, `AdminCommandService.cs`, `SecurityService.cs`, `MaintenanceService.cs`, …
- `Frog.Editor/Forms/ItemEditorForm.cs` etc. (real editors live under `Frog.Editor/Forms/GameData/` and `Forms/Phase8/`)
- `Frog.Core` placeholders: `GameLimits.cs` still talks about “confirmer avec VB6”; `ItemSerializer` / `NpcSerializer` throw `NotImplementedException`

These are **historical**. ADR-0003: do not revive them as FRoG parity.

### Live debt that Phase 9 **must** see

| Item | Evidence | Phase 9 relevance |
| --- | --- | --- |
| No operator moderation | `AdminCommandService.cs` is a one-line stub; no `PacketId` for mute/kick/ban | P9-1 |
| No permission model | `AccessRightEnum.cs`, `Role.cs`, `Permission.cs` stubs; event commands have *session/character* authority only ([`COMMAND_CATALOG.md`](../phase-08-quests-events-advanced-creation/COMMAND_CATALOG.md)) | P9-2 |
| `WorldFlagsPatchRequest` still on the wire | Rejected in PG production (`PacketDispatcher.HandleWorldFlagsPatchRequestAsync`) but opcode 34 remains | P9-2 |
| Default bind `127.0.0.1:6000` | `Frog.Server/appsettings.json`, `ServerOptions` | P9-2 / P9-4 |
| Placeholder DB passwords committed | `appsettings.json` now `NOT_A_PRODUCTION_SECRET` (P9-2); Compose still `frog_dev_only` | P9-2 gate: public bind + placeholder refused |
| No TLS | Raw `TcpListener` (`Frog.Server/Network/ServerSocket.cs`) | P9-2 residual risk |
| No backup/restore | No `pg_dump` script under `scripts/` | P9-3 |
| No packaging | No publish profile, no installer, no server Docker image | P9-4 |
| No metrics | Console logging only; `PostgresDatabaseHealth` exists but is not an HTTP health endpoint | P9-5 |
| `docs/DATA_MODEL.md` stale | Documents only early `world` / `content.tilesets` / `ops.legacy_imports` — omits `auth`, `player`, Phase 6–8 catalogs | P9-3 / P9-6 docs |
| `docs/STATUS.md` was stale | Still said Phase 8 READY FOR RE-REVIEW on `cursor/phase0-baseline-audit-02c7` | Fixed in this P9-0 |
| `docs/BACKLOG.md` stale | Checkboxes stop at Phase 6 | Document-only debt |
| MariaDB leftover | `MySqlConnector` 2.4.0 on Server + Editor; `MariaDb:*` config; `scripts/apply-frog-mariadb-schema.ps1` | Frozen (ADR-0002). Do not extend. |
| Phase 8 leftovers | Wait-across-disconnect, loot-table editors, arbitrary scripting — [`KNOWN_ISSUES.md`](../phase-08-quests-events-advanced-creation/KNOWN_ISSUES.md) | **Not** Phase 9 gates |

No Phase 8 product regression was exercised in P9-0 (docs-only). None claimed.

---

## 3. Dependencies / versions

| Piece | Version / pin | Where |
| --- | --- | --- |
| .NET SDK | **8.0.424** (`rollForward: latestFeature`) | `global.json`, CI `setup-dotnet` |
| C# | **12.0** (`LangVersion` pinned) | `Directory.Build.props` |
| Target | `net8.0` / `net8.0-windows` | csproj files |
| EF Core / Npgsql provider | **8.0.11** | `Frog.Persistence.PostgreSql`, Server (runtime-only) |
| Npgsql (tests) | **8.0.6** | `Frog.Persistence.IntegrationTests` |
| EFCore.NamingConventions | **8.0.3** | Persistence + Server |
| Microsoft.Extensions.* | **8.0.0** | Server, Application, Editor |
| MySqlConnector | **2.4.0** | Server, Editor — **legacy only** |
| xunit | **2.9.2** | test projects |
| Microsoft.NET.Test.Sdk | **17.11.1** | test projects |
| PostgreSQL | **16** (`postgres:16` CI, `postgres:16-alpine` Compose) | `.github/workflows/ci.yml`, `docker-compose.yml` |
| Protocol | `FrogWireProtocol.Version = **10**` | `Frog.Core/Constants/FrogWireProtocol.cs` |

No `Directory.Packages.props`. Windows targeting on Linux restore/build: `EnableWindowsTargeting=true` in `Directory.Build.props`. `WarningsAsErrors=true`.

---

## 4. Config paths

| Path | Purpose |
| --- | --- |
| `Frog.Server/appsettings.json` | Committed defaults: bind `127.0.0.1:6000`, `PostgreSql.enabled=false`, `MariaDb.enabled=false`, placeholder passwords, session idle 300s / cleanup 30s, save interval 45s |
| `Frog.Server/appsettings.Local.json` | **Gitignored** overlay (`.gitignore`) |
| `Frog.Server/appsettings.Local.json.example` | Template: enable PostgreSQL, disable MariaDB |
| `Frog.Editor/appsettings.Local.json.example` | Editor PG/MariaDB overlay (`ConnectionString` PascalCase — different shape than server) |
| Env `FROG_POSTGRES_CONNECTION_STRING` | Server fallback if `PostgreSql:ConnectionString` empty (`FrogServerHostFactory`) |
| Env `FROG_POSTGRES_TEST_CONNECTION_STRING` | PG integration tests |
| Env `FROG_EDITOR_FORCE_IN_MEMORY=1` | Editor CI smoke |
| Env `MARIADB_TEST_CONNECTION_STRING` | Optional legacy tests only |
| Playtest env | `PlaytestRuntimeOptions` / `PlaytestChildEnvironment` (editor-triggered; MariaDB forced off) |
| `docker-compose.yml` | Dev PG only: user `frog`, password `frog_dev_only`, db `frog`, volume `frog_pg_data` |

Production composition **requires** `PostgreSql:Enabled=true` and a connection string (`FrogServerHostFactory` throws if neither PG nor `AllowInMemoryFallback`). In-memory fallback is for unit tests only.

---

## 5. PostgreSQL migrations overview

Source: `Frog.Persistence.PostgreSql/Migrations/`. **24** Up migrations. Latest: `20260917204500_MapEventExecutionRequestIdGlobalUnique`. Applied automatically:

- Server: `PostgreSqlAuthScope` → `Database.Migrate()`
- Editor: `EditorPostgreSqlScope.MigrateAsync` / factory `Database.Migrate()`
- Tests: `IsolatedPostgresFixture`

| Migration | What it added (short) |
| --- | --- |
| `20260822040506_InitialMapPersistence` | schemas + `world.maps` / cells / warps |
| `20260822130000_ModernMapIdentity` | drop FRoG `legacy_id` |
| `20260822141924_DraftPublishSeparation` | published map snapshots |
| `20260823223136_TilesetDraftPublish` | tilesets draft/publish |
| `20260823224117_NpcDraftPublish` | NPCs |
| `20260823225349_ItemDraftPublish` | items |
| `20260823225941_SpellDraftPublish` | spells |
| `20260823230826_ClassDraftPublish` | classes |
| `20260823231902_ShopDraftPublish` | shops |
| `20260823233332_ResourceDraftPublish` | resources + spawns |
| `20260825224547_AuthAccountsAndSessions` | `auth.accounts`, `auth.auth_sessions` |
| `20260826014225_PlayerCharactersInventoryBankGround` | `player.characters`, inventory, bank, ground |
| `20260826111539_BankGoldShopStockEconomy` | bank gold, shop stock, economy ids |
| `20260826210000_EconomyRequestIdempotencyScope` | economy idempotency scope |
| `20260826213129_PublishedWorldRuntimeBindings` | `world.runtime_map_bindings`, spawn settings |
| `20260827021141_EconomyRequestIdCharacterScopedKey` | character-scoped economy keys |
| `20260828011225_MonsterKillRewards` | kill-reward ledger |
| `20260829133744_MapEventDraftPublish` | map-event definitions/placements |
| `20260829150740_CharacterWorldState` | switches / variables |
| `20260829154905_Phase8PlayerProgress` | quests / professions / craft requests |
| `20260829180742_Phase8ContentDefinitions` | `content.phase8_content_definitions` |
| `20260829182719_Phase8QuestObjectiveProgress` | quest objective counters |
| `20260830155511_MapEventExecutionRequests` | event execution ledger |
| `20260916221500_MapEventActivationLedger` | `activation_id` + `wait_ordinal` |
| `20260917204500_MapEventExecutionRequestIdGlobalUnique` | unique `request_id` |

**Schemas actually in `FrogDbContext` / snapshot:** `auth`, `content`, `ops`, `player`, `world`.  
[`docs/DATA_MODEL.md`](../../DATA_MODEL.md) lists only `world` / `content` (tilesets) / `ops` — **incomplete**.

No moderation / ban / mute tables exist.

---

## 6. Start / stop commands

### Build and test (from repo root)

```bash
dotnet restore Frog.Creator.sln
dotnet build Frog.Creator.sln -c Release
dotnet test Frog.Tests/Frog.Tests.csproj -c Release --no-build

docker compose up -d postgres
export FROG_POSTGRES_TEST_CONNECTION_STRING='Host=127.0.0.1;Port=5432;Database=frog_test;Username=frog_test;Password=frog_test_local_only'
dotnet test tests/Frog.Persistence.IntegrationTests/Frog.Persistence.IntegrationTests.csproj -c Release
```

Windows smokes (CI / a Windows host only):

```powershell
dotnet test tests/Frog.Editor.WindowsSmokeTests/Frog.Editor.WindowsSmokeTests.csproj -c Release
```

### Run binaries

```bash
# Requires appsettings.Local.json (or env) with PostgreSql.enabled=true for production mode
dotnet run --project Frog.Server/Frog.Server.csproj
dotnet run --project Frog.Client/Frog.Client.csproj      # Windows
dotnet run --project Frog.Editor/Frog.Editor.csproj      # Windows
```

**Start:** `Program.Main` builds the generic host; `GameServerService.ExecuteAsync` binds `TcpListener` and logs `ServerStarted`.  
**Stop:** host cancellation → stop accepting → dispose socket → await client handler tasks → `ServerStopped`. `Ctrl+C` / `IHost.StopAsync` is the supported path. Playtest stop is owned by `PlaytestOwnedProcessLauncher`.

There is **no** packaged `systemctl` unit, Docker image for the game server, or graceful-drain beyond the hosted-service stop shown above.

---

## 7. Windows / Linux / PostgreSQL requirements

| Surface | OS | Notes |
| --- | --- | --- |
| `Frog.Server` | Linux or Windows | `net8.0` console, `PlatformTarget=x64` |
| `Frog.Client` / `Frog.Editor` | **Windows only** at runtime | `net8.0-windows`, WinForms; Editor also WPF |
| Unit tests `Frog.Tests` | Linux or Windows | Linux needs Windows targeting (already in Directory.Build.props) |
| PG integration | Linux or Windows + PostgreSQL 16 | Isolated DB per fixture |
| Editor / gameplay / Phase 8 smokes | **windows-latest** CI | Not run on Linux agents |
| PostgreSQL | 16 | Compose alpine for dev; CI official `postgres:16` |
| MariaDB | Not required | Legacy opt-in only ([`docs/TESTING.md`](../../TESTING.md)) |

SDK **8.0.424** is pinned. Client/editor cannot be smoke-verified on this Linux Cloud Agent.

---

## 8. TCP protocol limits known today

Framing: little-endian Int32 length + payload. First payload byte = `PacketId`.

| Limit | Value | Source |
| --- | --- | --- |
| Protocol version | 10 | `FrogWireProtocol.Version` |
| Max frame | **1 MiB** (length ≤ 0 or > 1 048 576 → drop) | `Frog.Server/Network/ClientSession.cs`, `Frog.Tests/TcpFramingProtocolTests.cs` |
| Chat message | 512 UTF-8 bytes | `ChatProtocolLimits.MaxMessageUtf8Bytes` |
| Chat username / whisper target | 64 UTF-8 bytes | `ChatProtocolLimits.MaxUsernameUtf8Bytes` |
| Chat channels | Global, Map, Whisper | `ChatChannel` |
| Chat rate | 8 messages / 10 s / session | `GameplayLimits` + `ChatRateLimiter` |
| Account username | 3–32 `[A-Za-z0-9_-]` | `AccountInputRules` |
| Password | 8–128 (login allows shorter for legacy verify) | `AccountInputRules` |
| Characters / account | 8 | `GameplayLimits.MaxCharactersPerAccount` |
| Inventory / bank | 30 / 40 slots | `GameplayLimits` |
| Ground items / map | 200 | `GameplayLimits` |
| Movement packets | 50 / rolling second | `MovementPacketRateGate` |
| Position sync speed | 200 px/s + 28 px slack | `WorldMetrics` |
| Move step | 8 px / request; tile 32 px | `WorldMetrics` |
| Login failures | 8 / 1 minute / key | `LoginRateLimiter` |
| Reconnect token | 32 random bytes, URL-safe Base64; SHA-256 stored; **12 h** lifetime | `PostgresAuthSessionRepository`, `PacketDispatcher` login |
| Session idle | 300 s (config) | `appsettings.json` / `SessionOptions` |
| Periodic position save | ≥ 10 s, default 45 s | `PersistenceOptions` |
| Interact | Guid `activationId` (v10) | `PacketId.InteractRequest` |
| Event runtime | pages 50, commands/page 64, branch 8, CE depth 4, steps 256, wait 60 s, 4 active execs / character | `MapEventRuntimeLimits` |
| Character payload KV | 4 MiB / entry (legacy MariaDB path) | `CharacterPayloadKvLimits` |

Opcodes 1–77 + `Error=255`. No admin / guild / party / trade opcodes.

---

## 9. Security / ops risks (current, not hypothetical cleanup)

| Risk | Evidence | Severity for a hosted world |
| --- | --- | --- |
| TCP in clear text | `ServerSocket` / `TcpClient`, no TLS | High if bound beyond localhost |
| Default bind localhost | Safe default; operators must change bind without TLS guidance | Medium (docs/packaging gap) |
| Placeholder secrets in git | `appsettings.json` `changeme`; Compose `frog_dev_only` | Medium (dev-only if never used in prod) |
| No mute/kick/ban | Anyone who can connect can keep chatting / playing until process kill | High for ops |
| No operator ACL | Every authenticated account is a player; no GM bit | High for P9-1 |
| `WorldFlagsPatchRequest` still parsed | Rejected in PG prod; still a foot-gun if someone runs non-PG | Medium |
| Login rate limit is in-memory | Process restart clears windows; keying is caller-supplied | Medium |
| Reconnect token is bearer | 12 h; hashed at rest; stolen token = session | Medium |
| Password hasher is solid | PBKDF2-SHA256 600k, `$frog-v1$`, timing-safe reject | Positive — keep |
| “Already connected” is username-scoped | `ConnectionManager.TryCreateSession` — no multi-session; reconnect displaces | Known Phase 8 behavior |
| MariaDB can still be enabled | `MariaDb:enabled` + MySqlConnector | Residual; ADR says freeze, not delete |
| No backup | Volume `frog_pg_data` is the only durability | High for ops |
| No packaged stop/start | Host Ctrl+C only | Medium |
| Editor publishes to the same PG | Author machine with the connection string is a world admin | High — treat as part of P9-2 trust boundary |
| CI does not run on this branch until a PR to main exists | `ci.yml` `on:` filter | Process risk, not a product bug |

---

## 10. Load thresholds to measure (proposed, **not measured**)

P9-0 did not run a load tool. These are **hypotheses** derived from existing caps, for P9-5 to confirm or replace.

| Signal | Proposed starting target | Why this number |
| --- | --- | --- |
| Concurrent authenticated sessions | 25 / 50 / 100 | README success story is “two remote players”; 25 is a small hosted world; 100 is a stretch before AOI |
| Concurrent TCP connections (pre-auth) | 2× sessions | Login storms + reconnects |
| Movement + position-sync combined | ≤ 50 packets/s/session (already hard-capped) | `MovementPacketRateGate` |
| Chat accepted | 8 / 10 s/session | Already enforced; measure reject rate under spam |
| Interact / event activations | 5 sustained / character; burst 20 | Runtime allows 4 active executions |
| Economy + quest turn-in TPS (PG) | 10 committed tx/s | Single-node; measure p95 commit time |
| Frame rejects (oversize / malformed) | 0 in happy path; 100% drop > 1 MiB | Framing cap |
| Idle disconnect accuracy | ~300 s | `Sessions:idleTimeoutSeconds` |
| Host CPU (Linux server) | < 70% at the certified session count | Unknown today |
| PG connections | ≤ 20 from one server process | No pool settings documented; measure |
| Restart recovery | Full reconnect of certified N within 60 s | Needs harness |

**Do not** publish these as certified numbers without a `LOAD_REPORT`. P9-5 measured and revised them in [`LOAD_REPORT.md`](LOAD_REPORT.md) (100 authed mixed in-memory / 200 TCP Hello on a 4-core Linux agent; idle 300 s, economy TPS, interact, restart-reconnect, and PG pool size **not** certified).

---

## 11. Scripts present

| Script | Role |
| --- | --- |
| `scripts/ci-guard-lifecycle-logs.ps1` | Fail CI on crash markers |
| `scripts/verify-phase8-screenshot-manifest.ps1` | Phase 8 PNG SHA gate |
| `scripts/update-phase8-screenshot-manifest.ps1` | Local manifest refresh |
| `scripts/test-phase8-screenshot-manifest.ps1` | Manifest self-test |
| `scripts/windows-editor-smoke.ps1` | Local editor smoke |
| `scripts/apply-frog-mariadb-schema.ps1` | **Legacy** MariaDB — do not extend |

No backup, package, or load scripts.

---

## 12. P9-S reminder

Guilds / groups / trades: **DEFERRED**. See [`PHASE_PLAN.md`](PHASE_PLAN.md). Stubs `Guild.cs` / `GuildService.cs` are historical, not a start of work.
