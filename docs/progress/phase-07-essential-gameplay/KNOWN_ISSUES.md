# Phase 7 — Known Issues (CHANGES REQUESTED)

## Review rejection (active remediation)

1. E2E was not the required 17-step PostgreSQL network gate.
2. E2E used in-memory repositories and mid-scenario service injection.
3. Production DI registered hardcoded `Phase7PublishedContent` instead of PG published catalogs.
4. Shop/bank not fully atomic; bank gold was in-memory.
5. Incomplete PG integration/concurrency/restart coverage for player repos.
6. Client lacked typed Phase 7 decoding and usable gameplay UI.
7. Editor Windows smoke was incorrectly presented as gameplay-client proof.
8. Docs marked unverified requirements DONE; SHA/CI identity was stale.
9. Trailing whitespace / CRLF in `Frog.Application.csproj` (fixing).

## Remediation workstreams

- P7-FIX-1 PostgreSQL SoT + published catalogs
- P7-FIX-2 Atomic economy + bank gold persistence
- P7-FIX-3 PG integration + true 17-step E2E
- P7-FIX-4 Functional game client + gameplay smoke ×3
- P7-FIX-5 Documentation integrity

Phase 8 not started.
