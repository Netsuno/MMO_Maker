# Private-testing package set (not public beta)

Record for **Netsun**. **Private testing only / not public beta.**  
This file is a packaging identity record. It is **not** a release announcement and does **not** write the Phase 10 gate sentence.

Binaries stay **outside git** (`artifacts/` is gitignored). This PR commits **this document only**.

## Identities

| Item | Value |
| --- | --- |
| Product tip (`main`) | `7f8e79b6488843dd7821f114b9f5e0004345830f` (merge PR #9 client UI) |
| CI `main` | [35461538022](https://github.com/Netsuno/MMO_Maker/actions/runs/35461538022) **SUCCESS** |
| Client badge | `v10.3.0` (`Frog.Client/Frog.Client.csproj` `Version` / `InformationalVersion`; `ClientVersion.Display`) |
| Protocol | `FrogWireProtocol.Version = 11` (Hello + social/trade opcodes 80–86) |
| SDK | `global.json` **8.0.424** (`rollForward: latestFeature`); publish host SDK `8.0.424` |
| Persist (hosted) | PostgreSQL 16 · runtime role `frog_runtime` (see [`POSTGRES_ROLES.md`](POSTGRES_ROLES.md)) |
| Publish UTC | 2026-09-19T18:47:00Z … 18:47:24Z |
| Command | `./scripts/publish-frog.sh --target all --force` after `dotnet restore Frog.Creator.sln` |

Each layout’s `packaging-manifest.json` records the same `gitSha` `7f8e79b6488843dd7821f114b9f5e0004345830f`, `protocolVersion` **11**, `selfContained: true`, `singleFile: false`.

## Archives (SHA-256 from real files)

Produced on this agent at `artifacts/publish/archives/` (gitignored). Recomputed with `sha256sum` (matches `SHA256SUMS` and each on-disk `packaging-manifest.json` `archiveSha256`). **Do not invent hashes.**

| Archive (primary first) | Bytes | SHA-256 |
| --- | --- | --- |
| **`client-win-x64.zip`** | 70245641 | `692166da1b6a40c789111e4b37532cf6892665224202483aeacb0cb3ac8289c2` |
| `server-linux-x64.zip` | 36740891 | `e9aba2c06e5c04957ded102a418533195ae9ac596c66aadfc1e067fbb0adf9b4` |
| `server-win-x64.zip` | 37485101 | `ab53ca3c1348fa23db4c7d3a529cf979f1d86b60ef5066e56b7e701a0361d049` |
| `editor-win-x64.zip` | 73762925 | `4a084988bf4bc7026b38a8f2ed30981d8441028add1f800c7d140c3eed6a483e` |

Checksum file: `artifacts/publish/archives/SHA256SUMS`.

Layouts on disk (also gitignored):

- `artifacts/publish/client-win-x64/`
- `artifacts/publish/server-linux-x64/`
- `artifacts/publish/server-win-x64/`
- `artifacts/publish/editor-win-x64/`

Cloud-agent copies (same bytes / same SHA-256) for Orchestrator fetch:

- `/opt/cursor/artifacts/private_testing_client_win_x64.zip`
- `/opt/cursor/artifacts/private_testing_server_linux_x64.zip`
- `/opt/cursor/artifacts/private_testing_server_win_x64.zip`
- `/opt/cursor/artifacts/private_testing_editor_win_x64.zip`
- `/opt/cursor/artifacts/private_testing_SHA256SUMS.txt`

## Compatible set

Ship **this generation together**. The Win x64 client is the primary tester package; it must talk to a **v11** server from the same tip. Phase 9 **v10** clients are refused at Hello (incompatible-version message).

| Role | Layout | Notes |
| --- | --- | --- |
| Player client (primary) | `client-win-x64` | `Frog.Client.exe` + bundled runtime (`hostfxr.dll`). No SDK. |
| Hosted / Linux ops | `server-linux-x64` | `Frog.Server` + `Frog.Persistence.PostgreSql.dll` sidecar. |
| Windows ops / sibling playtest | `server-win-x64` | Same protocol; layout produced on Linux (`EnableWindowsTargeting`). |
| Creator | `editor-win-x64` | Prefer sibling folders `../client-win-x64` and `../server-win-x64` (editor launchers). |

Licences fixture in each zip: `demo-world/LICENSES.md` + `DEMO_WORLD.md`. Repo licence: MIT.

## Connect (no invented secrets)

Operator supplies the live host, port, TLS trust material, and **provisioned** account. Nothing below is a password, DSN, or certificate.

1. Extract zips **outside** any git working tree.
2. Operator starts the matching server layout (Linux or Windows). Copy `appsettings.Local.json` **after** extract from `appsettings.Local.json.example` — that overlay is **gitignored** and must **not** appear in the zip (verified absent in this set).
3. **Host / port:** packaged `appsettings.json` default is loopback **`127.0.0.1:6000`**. Use that **only** if the operator says the server is local. Otherwise use the host/port the operator gives. Non-loopback bind requires `Server:AllowNonLoopbackBind=true` and is fail-fast without a cert when TLS is required (see [`guides/OPERATIONS.md`](guides/OPERATIONS.md), [`LOAD_HARNESS_TLS.md`](LOAD_HARNESS_TLS.md)).
4. **TLS:** default packaged `Server:tls:mode` is `Off` (local / CI smokes). Hosted / external profile: `Mode=Required` — **no silent cleartext fallback**. Client must trust the operator’s real chain (or the confined test CA the operator provides). Do not paste certs or keys into tickets.
5. **Registration:** beta profile is `Registration:Mode=ProvisionedOnly` (`appsettings.Local.json.example`). TCP `RegisterRequest` is refused (`Inscriptions fermees.`). `InviteOnly` is the same refusal until invites exist ([`CLOSED_BETA.md`](CLOSED_BETA.md)). Packaged `appsettings.json` stays `Open` so local smokes still work — **do not** treat that as public signup.
6. Player: run `Frog.Client.exe`, enter the operator host/port, **Connect**, then **Login** with the provisioned account. Windows may show SmartScreen (unsigned binary).
7. Expected honest errors: server down, protocol mismatch (v10 vs v11), bad credentials, banned account, TLS certificate rejected. Never put DB passwords in the client.
8. Accounts: `tools/Frog.OpsCli` (`create` / `reset-password` / `session-revoke`). `create` never grants GM ([`CLOSED_BETA.md`](CLOSED_BETA.md)).

Player-facing steps: [`guides/PLAYER_QUICKSTART.md`](guides/PLAYER_QUICKSTART.md). Ops: [`guides/OPERATIONS.md`](guides/OPERATIONS.md).

## Limits (this agent)

| Claim | Honest status |
| --- | --- |
| Four self-contained zips + SHA-256 | **Produced** on Linux from tip `7f8e79b…` |
| WinForms / WPF GUI launch (`Frog.Client.exe` / `Frog.Editor.exe`) | **Not proven on this Linux agent.** Layouts exist; Wine is never a pass. Windows CI `--smoke-launch`: [35403209506](https://github.com/Netsuno/MMO_Maker/actions/runs/35403209506) SUCCESS (older tip; not re-run here). |
| `server-win-x64` process start | Layout only on this host. |
| `server-linux-x64` Hello / READY | Layout + zip produced here. Process/Hello **not re-run** in this packaging pass. `main` CI [35461538022](https://github.com/Netsuno/MMO_Maker/actions/runs/35461538022) includes packaged Linux playtest on this tip. |
| Installer / code-signing / auto-update | Absent (deferred). |
| Public beta / store / open registration | **No.** Private testing only. |
| Secrets in archives | Scan: no `appsettings.Local.json`, no PEM/PFX/private-key markers. Fixture DSNs in `appsettings.json` remain the documented `NOT_A_PRODUCTION_SECRET` placeholders. |

Do not merge this as a product release. Do not start a new phase.
