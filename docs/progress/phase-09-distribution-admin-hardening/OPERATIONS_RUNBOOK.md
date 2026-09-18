# Phase 9 — OPERATIONS_RUNBOOK

**Status:** stub (P9-0). Not an operational procedure yet.

## What exists today

- Start/stop: [`BASELINE_AUDIT.md`](BASELINE_AUDIT.md) §6
- Config: `Frog.Server/appsettings.json` + gitignored `appsettings.Local.json`
- Dev PostgreSQL: `docker compose up -d postgres`
- Health primitive: `Frog.Persistence.PostgreSql/PostgresDatabaseHealth.cs` (used in tests; no HTTP probe)

## Secrets / bind (P9-2)

- Copy `Frog.Server/appsettings.Local.json.example` → gitignored `appsettings.Local.json`.
- Or set `FROG_POSTGRES_CONNECTION_STRING`.
- Keep `Server:bindAddress` on `127.0.0.1` unless `Server:allowNonLoopbackBind=true` (clear-text TCP; put TLS in front).
- Host refuses to start if the bind is public **and** the DSN still contains a known placeholder (`NOT_A_PRODUCTION_SECRET`, `changeme`, `frog_dev_only`, …). See `SECURITY_MODEL.md` §6–7.
- First GM (out of band; no TCP grant):

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

Mute persists in `ops.account_sanctions` (`kind=mute`) and rejects `ChatSend` only. Kick closes the live TCP session and writes `ops.moderation_events` (not a lasting ban). Ban persists (`kind=ban`), revokes reconnect tokens, drops the live session, and rejects login/reconnect.

## TBD (P9-4)

- How to apply migrations (today: automatic `Database.Migrate()` on server/editor start)
- How to drain players before stop
- Where logs go besides console
- What to do when `PostgresDatabaseHealth` reports pending migrations

## Do not

- Enable `MariaDb:enabled` for a new world
- Point production at Compose passwords (`frog_dev_only` / `NOT_A_PRODUCTION_SECRET`)
