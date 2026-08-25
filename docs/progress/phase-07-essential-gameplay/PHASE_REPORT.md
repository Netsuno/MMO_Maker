# Phase 7 — Essential Gameplay

## Status

| Tranche | Status |
| --- | --- |
| 7.1 Authentication and sessions | IN PROGRESS (first commit) |
| 7.2 Characters | NOT STARTED |
| 7.3 Inventory, equipment, ground items | NOT STARTED |
| 7.4 Combat and essential spells | NOT STARTED |
| 7.5 Chat | NOT STARTED |
| 7.6 Shops and bank | NOT STARTED |
| 7.7 Progression, death and respawn | NOT STARTED |
| Phase 7 E2E gate | NOT STARTED |

## Baselines

- Starting implementation SHA (accepted Phase 6): `99b782f8f205c0161c0bba8838d041714e39947e`
- Branch: `cursor/phase0-baseline-audit-02c7`
- PR: #2

## 7.1 delivered (this tranche)

- Application identity ports (`IAccountRepository`, `IAuthSessionRepository`)
- PBKDF2-SHA256 password hashing (`PasswordHasher`)
- PostgreSQL `auth.accounts` + `auth.auth_sessions` migration
- Server auth service, rate limiting, session issue/validate/revoke
- Login returns opaque token; `ReconnectRequest`/`ReconnectResult` protocol
- Architecture-safe PG backend via `IServerAuthBackend` + runtime load
- Unit tests (`Phase7AuthTests`) and PostgreSQL integration test

## Phase 8

Not started.
