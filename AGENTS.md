# AGENTS.md

## Cursor Cloud specific instructions

FRoG Creator is a **.NET 8 solution** (`Frog.Creator.sln`). Standard build/run/test commands live in `README.md` (section "Exécution rapide") and `Docs/premier-monde.md`. The notes below only cover non-obvious things that matter when working on this repo from the Linux cloud VM.

### Two .NET SDKs are required (8.0 AND 9.0)

- The projects target `net8.0` / `net8.0-windows`, but `Directory.Build.props` sets `LangVersion=latest`. `Frog.Server` uses a **C# 13** feature (`ref struct` / `ReadOnlySpan<byte>` locals inside `async` methods). This only compiles with the **.NET 9 SDK** (which provides C# 13); with only the .NET 8 SDK, `latest` = C# 12 and the build fails with `CS4012`.
- CI (`.github/workflows/ci.yml`) runs on `windows-latest`, whose image already ships a .NET 9 SDK, so `dotnet` transparently picks C# 13 there. Reproduce the same on the VM by keeping both SDKs installed (`dotnet --list-sdks` should show `8.0.x` and `9.0.x`; the default resolves to 9.0.x, which is correct).

### Only 3 of 5 projects are buildable/runnable on Linux

- `Frog.Core`, `Frog.Server`, `Frog.Tests` target `net8.0` and build/run/test on Linux.
- `Frog.Client` (WinForms) and `Frog.Editor` (WPF + WinForms) target `net8.0-windows` and **cannot build or run on Linux** (`NETSDK1100`). Do **not** run `dotnet build Frog.Creator.sln` / `dotnet restore Frog.Creator.sln` on the VM — it fails on those two projects. Build/restore the cross-platform projects individually instead (see README commands).

### Lint

- There is no dedicated linter. The effective gate is `dotnet build` with `WarningsAsErrors=true` (a clean build = 0 warnings). CI runs only build + test.
- `dotnet format --verify-no-changes` is **not** used by CI and is noisy on Linux: `.editorconfig` mandates CRLF but files check out with LF, producing many false `ENDOFLINE`/`CHARSET` errors. Don't treat it as the lint gate.

### Running the server + end-to-end testing

- `dotnet run --project Frog.Server/Frog.Server.csproj` starts the TCP server on `127.0.0.1:6000`. By default `MariaDb.enabled=false` (see `Frog.Server/appsettings.json`), so it runs fully in-memory with a bootstrap account **`demo`/`demo`** and a built-in fallback map (`Starter Meadow`, 20x20). MariaDB is optional.
- The player client/editor are Windows-only, so full GUI E2E is not possible on the Linux VM. To exercise the server end-to-end here, drive it over the documented binary TCP protocol (`Frog.Client/Docs/protocol_login_map.md`): frame = `Int32` LE length + payload; flow = read `Hello` (protocol version 9) → `LoginRequest` (id 2) → `LoginResult` (id 3) → `MapRequest` (id 4, empty body) → `MapData` (id 5). The map blob is a `.fmap` that `Frog.Core.IO.MapSerializer.Deserialize` can parse.
