# Phase 9 — PACKAGING_GUIDE

**Status:** stub (P9-0). No publish profile or installer in the repo.

## Facts

- SDK pin: `global.json` → 8.0.424
- Server: `net8.0` x64 — can publish for linux-x64 and win-x64
- Client / editor: `net8.0-windows` — Windows only
- CI already restores/builds the whole solution on `windows-latest` and PG tests on Ubuntu

## TBD (P9-4)

- `dotnet publish` layouts and RID matrix
- Where `appsettings.json` vs `appsettings.Local.json` live in a zip
- Whether the server zip includes `Frog.Persistence.PostgreSql.dll` next to the host (required — `Program.TryLoadPostgreSqlAuthBackend`)
- Client/editor asset folders
- Version stamp / protocol v10 compatibility note (client and server must match)
- No `.fcc` importer, no MariaDB redistributable

## Out of scope

- Store / code-signing (unless later required)
- Dockerizing MariaDB
