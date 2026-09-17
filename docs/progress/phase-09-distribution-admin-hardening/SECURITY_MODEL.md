# Phase 9 — SECURITY_MODEL

**Status:** stub (P9-0). Design required in P9-2 before P9-1 ships public admin packets.

## Trust boundaries (observed today)

| Actor | What they can do now | Gap |
| --- | --- | --- |
| Anonymous TCP peer | Open a connection, send Hello-era frames, attempt login/register | Rate-limited (in memory); no TLS |
| Authenticated player | Full Phase 7/8 gameplay + chat | No mute/kick/ban; no GM role |
| Editor author with PG connection string | Publish maps and Phase 8 content | Same DB as production — world admin by possession of the string |
| Process host | Kill the server | Only real “ban” today |

## Already in place (keep)

- PBKDF2-SHA256 600k (`Frog.Core/Security/PasswordHasher.cs`)
- Timing-safe reject for unknown accounts
- Reconnect token hashed at rest (SHA-256), 12 h lifetime
- `WorldFlagsPatchRequest` rejected when PostgreSQL production is on
- Chat / login / movement rate limits (see BASELINE_AUDIT §8)
- Event commands scoped to session or character — not “run arbitrary SQL” ([`COMMAND_CATALOG.md`](../phase-08-quests-events-advanced-creation/COMMAND_CATALOG.md))
- `appsettings.Local.json` gitignored

## P9-2 must decide

- TBD: where the operator bit lives (`auth.accounts` column vs `auth.operators` table)
- TBD: mute vs kick vs ban state machine and expiry
- TBD: whether admin commands are new `PacketId`s or an authenticated out-of-band channel (console)
- TBD: TLS / bind-address policy for a hosted world
- TBD: rotation of reconnect tokens on ban
- TBD: editor publish credentials vs player DB role (least privilege)

## Explicitly out of scope

- Arbitrary author scripting runtime (Phase 8 leftover, not a Phase 9 gate)
- Guild/group permission trees (P9-S deferred)
