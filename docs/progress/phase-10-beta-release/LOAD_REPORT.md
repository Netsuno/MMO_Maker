# Phase 10 — LOAD_REPORT (P10-8)

Deux classes de preuve, à ne **pas** fusionner :

1. **Automatisée (dépôt / CI)** — campagne hébergée courte (~5 s) et in-memory (~45 s). Chiffres ci-dessous = ces runs seulement.
2. **Physique / machine dédiée** — 25 joueurs × 60 min : **Accepted / skipped by owner agreement — Netsun (2026-09-19); not re-run in this PR.** **Aucun** chiffre de latence, TPS ou CPU n’est inventé pour ce run. **Aucun** job CI 60 min n’existe.

Paquet docs candidate. Pas un claim marketing de sortie.

## Acceptation / skip propriétaire (2026-09-19 — Netsun)

| Item | Valeur |
| --- | --- |
| Palier mandat | 25 joueurs simultanés authentifiés × **60 min** |
| Environnement | Machine dédiée (annonce opérateur / propriétaire) |
| Statut | **Accepted / skipped by owner agreement — Netsun (2026-09-19); not re-run in this PR.** |
| Rejoué ici | **Non** |
| Métriques 60 min (p50/p95/p99, actions/s, CPU%, RSS) | **Non publiées dans ce fichier** — les inventer serait un mensonge |
| Job CI `--hold-ms 3600000` | **Absent** (`ci.yml` reste `--profile ci` = 5 s) |

Le script dédié reste disponible pour reproduction **hors CI** :

```bash
./scripts/phase10-hosted-load-campaign.sh --profile dedicated
# équivalent : --hold-ms 3600000
```

## Harness (automatisé)

| Item | Valeur |
| --- | --- |
| Outil | `tools/Frog.LoadHarness` scénario `campaign` |
| Script in-memory | [`scripts/run-p10-8-load-campaign.sh`](../../../scripts/run-p10-8-load-campaign.sh) |
| Script hosted packaged+PG | [`scripts/phase10-hosted-load-campaign.sh`](../../../scripts/phase10-hosted-load-campaign.sh) |
| Transport | `TlsMode=Required` + CA confinée (`--emit-test-certs`) — **pas** AcceptAll |
| Actions décodées | Hello, register/login/select, HeartbeatAck, PositionUpdate, InteractResult, MeleeAttackResult, ChatMessage |
| Métriques (runs courts seulement) | RTT p50/p95/p99 heartbeat/move/interact, actions/s, échantillons CPU%/RSS |
| Mandat durée (comparaison rapport) | `--mandate-hold-ms 3600000` |

```bash
# Preuve cloud in-memory (~45 s hold) — défaut historique du script
./scripts/run-p10-8-load-campaign.sh

# Hosted packaged+PG profil CI (~5 s) — c’est ce que CI exécute
./scripts/phase10-hosted-load-campaign.sh --profile ci
```

CI rapide (in-memory TLS, 25 sessions, ~2,5 s) : `Phase10LoadHarnessTlsTests.Campaign_TlsRequired_TwentyFiveSessions_RecordsMetrics`.

Profils hosted : `ci` 5 s · `cloud` 45 s · `dedicated` 3600 s. Seuls `ci` (et le run cloud in-memory documenté) ont des **chiffres** ci-dessous.

## Run hosted packaged+PG 2026-09-19 (automatisé, cet agent / CI)

| Item | Valeur |
| --- | --- |
| Commande | `./scripts/phase10-hosted-load-campaign.sh --profile ci` |
| Backend | `server-linux-x64` publié hors dépôt + PostgreSQL jetable + TLS Required (CA confinée) |
| Elapsed total | 19 491 ms (auth 25 + hold 5 000 ms) |
| Sessions | 25 Hello OK, 25 Login OK, 25 CharacterSelect OK |
| Hold vs mandat | **5 000 ms / 3 600 000 ms** — `mandateDurationMet=false` |
| Actions/s | 69,4 (heartbeat 137/123 ack, interact 137/112, melee 137/112) |
| Heartbeat RTT | n=123 p50 0,58 ms p95 389 ms p99 1 395 ms (contention démarrage / PBKDF2) |
| Interact RTT | n=112 p50 0,35 ms p95 2,4 ms p99 2,53 ms |
| Move → PositionUpdate | 137 sent / 0 recv (même limite spawns que l’in-memory) |
| Packaged server VmRSS peak | 222 784 kB |
| Harness CPU / RSS | ~0,9 % / ~70 Mio (processus **séparé** du serveur) |
| CI | [35449733364](https://github.com/Netsuno/MMO_Maker/actions/runs/35449733364) SUCCESS sur `814b8ba` ; tip `a489379` [35450339601](https://github.com/Netsuno/MMO_Maker/actions/runs/35450339601) SUCCESS |

Ces lignes **ne** décrivent **pas** le palier 60 min (**Accepted / skipped by owner agreement — Netsun (2026-09-19); not re-run in this PR.**).

## Run cloud in-memory 2026-09-19 (automatisé, lot P10-8)

| Item | Valeur |
| --- | --- |
| Commande | `./scripts/run-p10-8-load-campaign.sh --hold-ms 45000` |
| Mode | self-host in-memory + TLS Required (générateur et serveur = **même processus**) |
| PostgreSQL / monde publié | **non** |
| Elapsed total | 53 554 ms (auth + 45 000 ms hold) |
| Sessions | 25 Hello OK, 25 CharacterSelect OK, TLS Required / localhost |
| Hold vs mandat | **45 000 ms / 3 600 000 ms** — `mandateDurationMet=false` |
| Actions décodées / s | 29,44 (heartbeat 428, interact 447, melee 450) |
| Heartbeat RTT | n=428 p50 0,56 ms p95 2,5 ms p99 9,25 ms |
| Interact RTT | n=447 p50 0,51 ms p95 3,21 ms p99 43,9 ms (max 2006 ms, 3 timeout) |
| Move → PositionUpdate | 0 pendant ce run (spawns empilés / collisions) |
| CPU process (fin) | ~0,5–4,2 % / 4 logical ; RSS ~110 MiB (même process) |
| JSON | `/tmp/p10-8-campaign.json` (hors git) |

## Synthèse mandat vs preuve

| Signal | Mandat | Automatisé ici | Physique |
| --- | --- | --- | --- |
| 25 sessions authentifiées | 25 | 25 (CI + scripts) | Inclus dans l’acceptation propriétaire |
| Durée 60 min | 3 600 000 ms | **5 000 ms** hosted CI / **45 000 ms** in-memory (`mandateDurationMet=false`) | **Accepted / skipped by owner agreement — Netsun (2026-09-19); not re-run in this PR.** |
| p95 ≤ 250 ms / p99 ≤ 1 s | bout-en-bout | mesuré **loopback court seulement** | **non inventé** pour le dédié |
| Économie 10 mut/s × 5 min | — | non (campaign n’enchaîne pas shop/banque/trade) | non documenté ici |
| Interact 5/s × 60 s + rafale 20 | — | cadencé court, pas le palier isolé | non documenté ici |
| Idle 300 s | — | non mesuré en CI | non documenté ici |
| Reconnect 25 &lt; 60 s | — | non mesuré en CI | non documenté ici |
| Pool PG ≤ 20 | — | N/A self-host ; hosted court sans dump pool | non documenté ici |
| CPU &lt; 80 % | — | échantillons **courts** seulement | **non inventé** pour le dédié |
| Job CI ~90 min dédié | — | **absent** de `ci.yml` (volontaire) | acceptation ≠ job CI |

## Ce que ce rapport ne fait pas

- Ne pas lire les tableaux 5 s / 45 s comme une certification 25×60.
- Ne pas inventer un run CI 3600 s.
- Ne pas retirer l’acceptation propriétaire : le palier 25×60 n’est **plus** un bloqueur gate ouvert ([`KNOWN_ISSUES.md`](KNOWN_ISSUES.md)).
