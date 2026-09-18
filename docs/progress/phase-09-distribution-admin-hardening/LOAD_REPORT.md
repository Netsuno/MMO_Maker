# Phase 9 — LOAD_REPORT

**Status:** P9-5 **DONE** (measured on this agent, not guessed).  
**Harness code SHA:** `0452504ee89627686dc6bf534c7103f0e606bd83` (`0452504`). The tip after this report commit will be newer; do not invent a CI URL.  
**No CI URL** is claimed for this run (`ci.yml` still only fires on `main` / PRs to `main`). Draft PR #7 exists; this file does not invent a GitHub Actions URL.

UDP / AOI was **not** built. Phase 8 E2E / screenshot SHA gates were **not** touched.

## How to run the harness

In-memory self-host (no PostgreSQL, loopback TCP):

```bash
./scripts/run-load-harness.sh --scenario mixed --sessions 25
# or:
dotnet run --project tools/Frog.LoadHarness -- --scenario mixed --sessions 25 --json-out artifacts/load/load-report.json
```

Scenarios: `connect` (Hello only), `chat`, `move`, `mixed` (connect + register/login/select + chat burst + move burst + oversize frame + login rate-limit probe).

Attach to a packaged or from-source listener (P9-4 path):

```bash
./scripts/run-load-harness.sh --host 127.0.0.1 --port 6000 --sessions 10 --scenario connect
```

CI-sized proof (always, no PG): `dotnet test Frog.Tests/Frog.Tests.csproj --filter FullyQualifiedName~.Phase9OpsMetricsTests`  
PG-sized proof (needs `FROG_POSTGRES_TEST_CONNECTION_STRING`): `FullyQualifiedName~.PostgresLoadObservabilityTests`

## Host that produced the numbers below

| Item | Value |
| --- | --- |
| Kernel | Linux 6.12.94+ x86_64 (`uname -a`) |
| CPU | 4 logical processors |
| RAM | 15 GiB |
| SDK | 8.0.424 (`global.json`) |
| PostgreSQL | 16.15 (Ubuntu), local `frog_test` role |
| Mode | harness **self-host in-memory** unless noted |

`serverOps.connectionsAccepted` is **N + 1** on `connect` (one extra TCP probe in `WaitForAcceptAsync` before workers) and **N + 3** on `mixed` (probe + oversize probe + login-rate probe).

## Measured (this run)

### Pre-auth TCP (Hello)

| Sessions requested | Hello OK | Connect fail | Elapsed | Server accepted | Rejects | PG errors |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 25 | 25 | 0 | 2504 ms | 26 | 0 | 0 |
| 50 | 50 | 0 | 2499 ms | 51 | 0 | 0 |
| 100 | 100 | 0 | 2501 ms | 101 | 0 | 0 |
| 200 | 200 | 0 | 2538 ms | 201 | 0 | 0 |

Hold was 2 s. Process CPU estimate ~3%. **200 concurrent TCP Hellos held** on this host.

### Authenticated mixed (register + login + select + chat 12 + move 80)

| Sessions | Select OK | Chat sent / rate-limited | Move sent / rate-limited | Oversize dropped | Elapsed | Server `rate_limit_hits` | PG errors | Process CPU est. |
| ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: | ---: |
| 4 (unit test) | 4 | (cap proven) | (cap proven) | 1 | ~9 s | 137 (chat 16, move 120, login 1) | 0 | — |
| 25 | 25 | 300 / 100 | 2000 / 750 | 1 | 14.2 s | 851 (chat 100, move 750, login 1) | 0 | 14.7% |
| 50 | 50 | 600 / 200 | 4000 / 1500 | 1 | 17.2 s | 1701 (chat 200, move 1500, login 1) | 0 | 22.3% |
| 100 | 100 | 1200 / 400 | 8000 / 3000 | 1 | 23.3 s | 3401 (chat 400, move 3000, login 1) | 0 | 30.8% |

Chat math matches the cap: 12 burst − 8 allowed / 10 s = **4 rejects / session**.  
Move math matches the cap: 80 burst − 50 / rolling second = **30 rejects / session** (burst completed well under 1 s).

Oversize probe (`length = 1 MiB + 1`, no payload): connection dropped every time; `connectionsRejected = 1`, log `TCP frame rejected … reason=oversize_frame`.

Login probe: 9 failed logins on one TCP endpoint → 8 `invalid_credentials` then `rate_limited`; `rateLimitHitsLogin = 1`.

Working set at 100 mixed: ~164 MiB (harness + in-memory host in one process).

### PostgreSQL

| Signal | Result |
| --- | --- |
| `PostgresLoadObservabilityTests` (4 authed mixed **attached** to a PG-backed `FrogServerHostFactory`) | Hello/register/login/select 4/4; chat rate-limit ≥ 4; oversize drop; **`postgresErrors = 0`** |
| Full `Frog.Persistence.IntegrationTests` this run | **180 PASS / 0 fail / 0 skip** (~3 min), includes `PackagedServerPostgreSqlProcessTests` (P9-4 packaged process + PG login/shop) and P9-1 moderation PG tests |
| 100-session mixed against PG | **Not run.** PBKDF2-SHA256 600k per register/login dominates wall time; this agent spent the PG budget on the full 180-test suite instead of a 100-client PG storm. |

## BASELINE_AUDIT §10 — certified vs revised

| Signal | Proposed (P9-0, not measured) | This run | Verdict |
| --- | --- | --- | --- |
| Concurrent authenticated sessions | 25 / 50 / 100 | **100 mixed in-memory, 0 failures** | **Certified 100** (in-memory TCP on a 4-core / 15 GiB Linux host) |
| Concurrent TCP connections (pre-auth) | 2× sessions | **200 Hello** | **Certified 200** (above 2×100) |
| Movement + position-sync | ≤ 50 / s / session | 80 burst → 30 rejects / session | **Enforced and visible** (`ops_metrics` + `Movement rate limited`) |
| Chat accepted | 8 / 10 s / session | 12 burst → 4 rejects / session | **Enforced and visible** (`Chat rate limited`) |
| Frame rejects > 1 MiB | 100% drop | 1/1 drop | **Certified** |
| Host CPU | < 70% at certified N | ~31% process estimate at 100 mixed | **Met** on this host (process-level, not whole-box) |
| Idle disconnect ~300 s | proposed | **Not measured** (would need ≥5 min) | **Revised: not certified** |
| Economy + quest TPS 10 / s | proposed | **Not in this harness** | **Revised: not certified** |
| Interact 5 sustained / burst 20 | proposed | **Not in this harness** | **Revised: not certified** |
| PG connections ≤ 20 | proposed | Pool not dumped; 4 authed mixed + 180 PG tests showed **0** `postgres_errors` | **Revised:** error **visibility** certified; pool size **not** certified |
| Restart reconnect N in 60 s | proposed | **Not measured** | **Revised: not certified** |
| PG 100 authed | implied by 100 | 4 authed mixed measured | **Revised certified PG floor: 4 concurrent authed** on a seeded host. Stretch 25–100 remains an operator attach (`--host/--port`) after a published world exists. |

## Ops signals (minimal, proven)

| Counter / log | Where | Proof |
| --- | --- | --- |
| `connections_accepted` | `GameServerService` after TCP accept | `ops_metrics` EventId 5030; matches N+probes |
| `connections_rejected` + `TCP frame rejected … reason=` | oversize / invalid length in `ClientSession` | mixed runs `connectionsRejected=1`, `oversize_frame` |
| `rate_limit_chat` / `rate_limit_movement` / `rate_limit_login` | `PacketDispatcher` / `AuthService` | counts match client Error packets |
| `postgres_errors` + `PostgreSQL error connection=` | dispatch catch if type name contains `Npgsql` / `PostgresException` | 0 under load; unit test classifies fake `*Npgsql*` / `*PostgresException*` types |
| JSON file (optional) | `FROG_OPS_METRICS_PATH` | `OpsMetricsSnapshotHostedService` |

There is still **no HTTP `/metrics`**. Console + optional file is the operator surface.

## Sanity (this run)

| Suite | Result |
| --- | --- |
| `Frog.Tests` Release | **436 PASS / 0 fail / 0 skip** (~42 s) |
| `Frog.Persistence.IntegrationTests` Release | **180 PASS / 0 fail / 0 skip** (~3 min) |
| Phase 8 screenshot SHA scripts | **not run / not changed** (Windows CI gate) |

## Residual risks

- Login rate-limit key is still `RemoteEndPoint` (IP:port), so a NAT of distinct source ports does not share a window (known P9-2 residual). The probe proves the counter, not IP-wide enforcement.
- Chat Global fan-out is O(sessions²) under this harness; 100 mixed was fine here, not an AOI substitute.
- Authenticated ramp is CPU-bound on PBKDF2 600k — that, not TCP accept, is the cost of “100 logins”.
- Packaged-server attach was not a separate 100-session storm; P9-4 `PackagedServerPostgreSqlProcessTests` still passed in the 180 PG tests (process + PG login/shop).
- No TLS, no metrics HTTP, no player-drain on stop (existing).
- P9-6 still owns Phase 8 Windows smokes ×3 and a real CI URL once a run exists.

## Out of scope (kept empty)

- UDP / AOI
- Mute/kick/ban product (P9-1, already on this branch)
- Phase 8 E2E replacement
- Phase 10
