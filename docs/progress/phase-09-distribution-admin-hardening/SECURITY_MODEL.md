# Phase 9 — SECURITY_MODEL

**Status:** P9-2 (source of truth for operator privilege and Phase 9 security gates) plus P9-1 sanction commands implemented against this document.

---

## 1. Actors

| Actor | How they show up | What they can do today | What they must not do |
| --- | --- | --- | --- |
| Anonymous TCP peer | Any host that can reach `Server:BindAddress:Port` (`Frog.Server/Network/ServerSocket.cs`) | Open a connection, send `Hello` / `LoginRequest` / `RegisterRequest` / `ReconnectRequest` (`Frog.Core/Enums/PacketId.cs`) | Authenticate without credentials; brute-force without the login window (`Frog.Server/Security/LoginRateLimiter.cs`) |
| Authenticated player | Account in `auth.accounts` (`Frog.Persistence.PostgreSql/Entities/Auth/AccountEntity.cs`) after `AuthService.TryAuthenticateAsync` | Phase 7/8 gameplay + chat Global/Map/Whisper | Moderate others; patch `worldFlags` in production; forge stats (`CharacterStatsUpdateRequest` already rejected outside playtest / in-memory fallback) |
| Operator (GM) | Row in `auth.operators` (`IOperatorDirectory`) | `ModerateRequest` mute/unmute/kick/ban/unban **after** `IsOperatorAsync(session.AccountId)` | Exist merely because they registered first; grant themselves via a client packet |
| Editor author | Machine holding the PostgreSQL connection string (editor `appsettings.Local.json`, gitignored) | Publish maps and Phase 8 content to the same database the server reads | Be treated as a distinct DB role today — possession of the string **is** world-admin |
| Process host | OS user that can kill / restart `Frog.Server` | The only “ban” that exists before P9-1 | Substitute for an ACL |

Historical stubs (`Frog.Server/Models/Role.cs`, `Permission.cs`, `Frog.Core/Enums/AccessRightEnum.cs`, `Frog.Server/Services/AdminCommandService.cs`, `SecurityService.cs`) are **not** product code (ADR-0003). Do not revive them.

---

## 2. Assets

| Asset | Location | Notes |
| --- | --- | --- |
| Account password hashes | `auth.accounts.password_hash` | PBKDF2-SHA256 600k, `$frog-v1$` (`Frog.Core/Security/PasswordHasher.cs`) |
| Reconnect tokens | Emitted at login; **SHA-256 at rest** in `auth.auth_sessions.token_hash` (`PostgresAuthSessionRepository`) | 32 random bytes, 12 h lifetime; bearer if stolen |
| Session mapping | In-process `ConnectionManager` | Username-scoped; reconnect displaces |
| Published world + catalogs | schemas `world`, `content` | Editor publish writes here |
| Player inventory / economy / quests | schema `player` | Server-authoritative |
| Operator grants | schema `auth.operators` (this phase) | Out-of-band grant only |
| Sanctions | `ops.account_sanctions` + `ops.moderation_events` (P9-1, migration `20260918001424_OpsAccountSanctions`) | |
| Process memory | Login/chat rate-limit windows | Lost on restart |

---

## 3. Trust boundaries

```text
[untrusted TCP client] --cleartext FrogWire v10--> [Frog.Server bind]
                         |
                         +-- authn: AuthService + IAccountRepository
                         +-- authz (gameplay): session / character ownership
                         +-- authz (moderation, P9-1): IOperatorDirectory
                         |
                         v
                   [PostgreSQL 16]
                         ^
                         |
[editor / ops workstation] -- connection string (same role today) --+
```

Boundaries:

1. **Wire vs process.** Clients are never trusted. `PacketDispatcher` is the only opcode switch (`Frog.Server/Network/PacketDispatcher.cs`). Frames > 1 MiB are dropped (`ClientSession`).
2. **Authn vs gameplay.** A valid password does not imply character ownership; character packets check `ICharacterBootstrap.IsCharacterOwned`.
3. **Player vs operator.** Register/login never writes `auth.operators`. Default is “not an operator”.
4. **Editor vs server.** There is no separate publisher role. The connection string is a world-admin credential. Treat it like a root password (P9-4 packaging must not commit it).
5. **Loopback vs public bind.** Default `127.0.0.1:6000` (`Frog.Server/appsettings.json`). Non-loopback requires `Server:AllowNonLoopbackBind=true`. There is **no TLS** on `TcpListener`.

---

## 4. Authentication (players)

Authoritative path:

- Username / password rules: `Frog.Application/Identity/AccountInputRules.cs` (username 3–32 `[A-Za-z0-9_-]`; register password 8–128; login allows shorter for legacy verify only).
- Hash: `PasswordHasher.HashPassword` → `$frog-v1$pbkdf2-sha256$600000$<salt>$<key>`. Verify uses `CryptographicOperations.FixedTimeEquals`. Unknown accounts call `VerifyOrTimingSafeReject` (burns a full hash).
- Persistence: `PostgresAccountRepository` (production) / `InMemoryAccountRepository` (tests / playtest). In-memory seeds a `demo` account — **not** an operator.
- Rate limit: `LoginRateLimiter` (8 failures / 60 s / key). Key is `ClientSession.RemoteEndPoint` for login; reconnect uses prefix `reconnect:` + endpoint (`AuthService`). In-memory; restart clears the window.
- Reconnect: opaque token, hashed at rest, `IAuthSessionRepository.RevokeAllForAccountAsync` already exists for P9-1 bans.
- Playtest reserved username is gated by `PlaytestAuthTokenGate`, not by `auth.operators`.

MariaDB account tables are **frozen** (ADR-0002). Do not add operator bits there.

---

## 5. Authorization — permission source of truth (decision)

### Decision

**Operators live in table `auth.operators` (PostgreSQL + EF), not as a column on `auth.accounts`.**

| Option | Verdict |
| --- | --- |
| `auth.accounts.is_operator` boolean | Rejected. Register/login would carry a privilege bit on the hottest identity row; easier to leak on `AccountRecord`; no grant audit. |
| MariaDB `accounts` / VB6 `AccessRightEnum` | Rejected (ADR-0002 / ADR-0003). |
| `Frog.Server/Models/Role.cs` stubs | Rejected. Historical. |
| **`auth.operators` 1:1 with `auth.accounts`** | **Accepted.** Explicit grant, soft-revoke, audit columns, unused by the login path. |

Port: `Frog.Application/Identity/IOperatorDirectory.cs`
Implementations: `PostgresOperatorDirectory` (production), `InMemoryOperatorDirectory` (playtest / unit tests).
Entity: `Frog.Persistence.PostgreSql/Entities/Auth/OperatorEntity.cs`
Migration: `20260917223000_AuthOperators`.

```text
auth.operators
  account_id     uuid PK FK auth.accounts(id) ON DELETE CASCADE
  granted_at_utc timestamptz NOT NULL
  granted_by     varchar(64) NOT NULL   -- "sql" / ops username / "bootstrap"
  note           varchar(256) NULL
  revoked_at_utc timestamptz NULL       -- NULL = active
```

**IsOperator** = row exists AND `revoked_at_utc IS NULL`.

Grant is **out of band** (SQL or a future ops tool). There is no grant/revoke `PacketId`. First operator:

```sql
INSERT INTO auth.operators (account_id, granted_at_utc, granted_by, note)
SELECT id, now(), 'sql', 'initial operator'
FROM auth.accounts
WHERE username = 'your-gm-username';
```

Do **not** auto-promote the first registered account.

P9-1 **must** call `IOperatorDirectory.IsOperatorAsync(session.AccountId)` before any mute/kick/ban. A client-supplied “I am GM” flag is forbidden. Unprivileged accounts (no row) cannot moderate — this is already true (no admin opcodes) and is the negative gate P9-1 must keep.

### P9-1 sanction schema (implemented)

Mute / ban **state** is operational, not identity. Put it in `ops`, not on `auth.accounts`.

```text
ops.account_sanctions
  id              uuid PK
  account_id      uuid NOT NULL FK auth.accounts(id)
  kind            text NOT NULL CHECK (kind IN ('mute', 'ban'))
  reason          text NOT NULL
  actor_account_id uuid NOT NULL FK auth.accounts(id)  -- operator
  created_at_utc  timestamptz NOT NULL
  expires_at_utc  timestamptz NULL                     -- NULL = until reversed
  revoked_at_utc  timestamptz NULL

  -- at most one active mute and one active ban per account
  UNIQUE (account_id, kind) WHERE revoked_at_utc IS NULL

ops.moderation_events
  id, at_utc, actor_account_id, target_account_id, action ('mute'|'unmute'|'kick'|'ban'|'unban'),
  reason, details_json
```

| Action | Persist? | Enforcement (P9-1) |
| --- | --- | --- |
| Mute | `ops.account_sanctions` kind=`mute` | Reject `ChatSend`; other gameplay stays |
| Kick | event only (optional short reconnect deny) | Close the TCP session immediately |
| Ban | `ops.account_sanctions` kind=`ban` | Reject login + reconnect; `RevokeAllForAccountAsync`; drop live session |

P9-1 may add new `PacketId`s **or** an authenticated console channel. If packets: they are server-checked against `auth.operators`, never against the client. Guild/group/trade permission trees are P9-S **DEFERRED**.

---

## 6. Secret handling

| File | Role | Production? |
| --- | --- | --- |
| `Frog.Server/appsettings.json` | Committed defaults: bind `127.0.0.1:6000`, `PostgreSql.enabled=false`, password `NOT_A_PRODUCTION_SECRET` | No |
| `Frog.Server/appsettings.Local.json` | Gitignored overlay (`.gitignore` `**/appsettings.Local.json`) | Yes — real DSN lives here or in env |
| `Frog.Server/appsettings.Local.json.example` | Template (`VOTRE_MOT_DE_PASSE`) | No |
| `Frog.Editor/appsettings.Local.json.example` | Editor overlay template | No |
| Env `FROG_POSTGRES_CONNECTION_STRING` | Server fallback if config CS empty (`FrogServerHostFactory`) | Yes |
| `docker-compose.yml` | `frog` / `frog_dev_only` — local volume only | No |

Gate (`PlaceholderSecretPolicy`): if the bind is **not** loopback, the host **refuses to start** when an **enabled** backend DSN contains a known placeholder (`NOT_A_PRODUCTION_SECRET`, `changeme`, `CHANGE_ME`, `VOTRE_MOT_DE_PASSE`, `frog_dev_only`, `frog_test_local_only`). A **disabled** backend (typical: `MariaDb.enabled=false` while PostgreSQL is the active store) does **not** participate in that check — leftover Compose / `appsettings.json` placeholders on the unused path must not block start. Loopback + Compose passwords remain valid for local docker. Production still requires a real secret on every **enabled** backend before a public bind.

Do not commit `appsettings.Local.json`. Do not reuse Compose passwords on a hosted world.

---

## 7. Bind / TLS stance (Phase 9)

| Rule | Status |
| --- | --- |
| Default bind `127.0.0.1:6000` | Keep |
| Parseable IP required | `ServerOptions.Validate` |
| Non-loopback requires `Server:AllowNonLoopbackBind=true` | Implemented this phase |
| TLS on `TcpListener` | **Not in Phase 9.** Residual risk: clear-text if publicly bound |
| Recommended hosted layout | Loopback (or private NIC) + SSH tunnel / TLS-terminating reverse proxy; firewall to the proxy only |
| UDP / AOI | Not a Phase 9 item |

---

## 8. Leftover / dangerous packets

Opcode list: `Frog.Core/Enums/PacketId.cs` (1–79 + `Error=255`). P9-1 added `ModerateRequest` (78) / `ModerateResult` (79).

| Packet | Risk | Phase 9 stance |
| --- | --- | --- |
| `WorldFlagsPatchRequest` (34) | Client-authored JSON merge into character `worldFlags` (legacy MariaDB `character_world_flag`) | **Rejected** when PostgreSQL is enabled **or** when the host is a production composition (not playtest, not `AllowInMemoryFallback`). Policy: `WorldFlagsPatchPolicy`. Message: `WorldFlagsPatch desactive en production PostgreSQL (Phase 8).` Opcode kept for wire compatibility. |
| `CharacterStatsUpdateRequest` (27) | Client-forged STR…LUCK | Already rejected outside playtest / in-memory fallback (P7-G1). Keep. |
| `ReconnectRequest` (36) | Stolen bearer token = session | Hashed at rest; 12 h; P9-1 must revoke on ban |
| `ChatSend` (15) | Spam / harassment | `ChatRateLimiter` 8 / 10 s / session (`GameplayLimits`). Active mute (`ops.account_sanctions` kind=`mute`) rejects the send. |
| `ModerateRequest` (78) | Unprivileged impersonation of a GM | Server calls `IOperatorDirectory.IsOperatorAsync(session.AccountId)`. Unprivileged → `ModerateResult` false. No grant/revoke opcode. |
| `RegisterRequest` (6) | Account spam | Same login rate limiter keying (endpoint). Residual: NAT shares a window |
| Event commands (`COMMAND_CATALOG.md`) | Not SQL; session- or character-scoped | Not operator commands. Do not overload them for mute/kick/ban |
| Unknown opcode | Logged + `Error` | Keep |
| Historical MariaDB stores | Frozen | Do not extend |

Playtest / `AllowInMemoryFallback=true` may still apply a WorldFlags merge (Phase 8 leftover). That path is not a hosted-world configuration.

---

## 9. Rate limits (verified, not tightened)

| Gate | Cap | File | Tests |
| --- | --- | --- | --- |
| Login / register failures | 8 / 60 s / key | `LoginRateLimiter` | `Phase7AuthTests.LoginRateLimiter_BlocksAfterFailures` |
| Reconnect failures | same limiter, `reconnect:` prefix | `AuthService` | existing reconnect TCP tests |
| Chat | 8 messages / 10 s / session | `ChatRateLimiter` | `Phase9SecurityGateTests.ChatRateLimiter_BlocksAfterWindowCap_AndResetClears` |
| Movement | 50 / rolling second | `MovementPacketRateGate` | existing |
| Frame size | 1 MiB | `ClientSession` | `TcpFramingProtocolTests` |

No cap change in P9-2: existing values match `BASELINE_AUDIT` §8. Residual: in-memory windows reset on process restart; login key is the remote endpoint string (NAT / IPv6 privacy).

---

## 10. Residual risks (not closed by P9-2)

- Clear-text TCP if `AllowNonLoopbackBind` is used without an external TLS terminator.
- Editor and server share one PostgreSQL role — no least-privilege publisher vs runtime split (defer to later ops work, not P9-1).
- Login rate limiter is process-local.
- Reconnect token is a bearer secret for 12 h.
- MariaDB can still be enabled (`MariaDb:enabled`) — frozen, not deleted.
- Mute/kick/ban shipped in P9-1; unprivileged accounts still cannot moderate. Grant remains out-of-band.
- In-memory `demo`/`demo` bootstrap account exists for tests/playtest only; production requires PostgreSQL (`FrogServerHostFactory` throws without PG or `AllowInMemoryFallback`).
- CI on this branch runs only when a PR targets `main` — do not invent run URLs.

---

## 11. Explicitly out of scope

- Arbitrary author scripting runtime (Phase 8 leftover).
- Guild / group / trade permission trees (P9-S **DEFERRED**).
- TLS implementation, UDP, AOI, sharding.
- Auto-granting the first account.
- New MariaDB schema.
