# Phase 10 — LOAD_REPORT (P10-8)

**Pas READY.** Le mandat 25 joueurs × **60 min** TLS+PG+monde publié n’est **pas** clos. Ce fichier borne honnêtement ce qui est automatisé et ce qui reste pour une machine dédiée.

## Harness

| Item | Valeur |
| --- | --- |
| Outil | `tools/Frog.LoadHarness` scénario `campaign` |
| Script | [`scripts/run-p10-8-load-campaign.sh`](../../../scripts/run-p10-8-load-campaign.sh) |
| Transport | `TlsMode=Required` + CA confinée (`--emit-test-certs`) — **pas** AcceptAll |
| Actions décodées | Hello, register/login/select, HeartbeatAck, PositionUpdate, InteractResult, MeleeAttackResult, ChatMessage |
| Métriques | RTT p50/p95/p99 heartbeat/move/interact, actions/s, échantillons CPU%/RSS |
| Mandat durée | `--mandate-hold-ms 3600000` (comparaison rapport seulement) |

```bash
# Preuve cloud (~90 s) — défaut du script
./scripts/run-p10-8-load-campaign.sh

# Mandat 60 min — machine dédiée (pas ce run cloud)
./scripts/run-p10-8-load-campaign.sh --hold-ms 3600000
```

CI rapide (in-memory TLS, 25 sessions, ~2,5 s) : `Phase10LoadHarnessTlsTests.Campaign_TlsRequired_TwentyFiveSessions_RecordsMetrics`.

## Environnement de cette campagne

Publié **avant** les mesures (mandat P10-8).

| Item | Valeur |
| --- | --- |
| OS | Linux 6.12.94+ x86_64 (agent cloud) |
| CPU | 4 processeurs logiques |
| RAM | voir échantillons `workingSetBytes` du JSON |
| Disque | SSD agent |
| SDK | 8.0.425 (`global.json` 8.0.424 rollForward latestFeature) |
| Mode | **self-host in-memory** + TLS Required (générateur et serveur = **même processus**) |
| PostgreSQL | **non** ( palier PG 25×60 = machine dédiée ) |
| Monde publié | **non** (in-memory Phase 7 seed) |
| Localisation charge | loopback 127.0.0.1 |

## Mesure exécutée ici

### Run cloud 2026-09-19 (cet agent)

| Item | Valeur |
| --- | --- |
| SHA harness (ce lot) | tip de la branche au commit P10-8 |
| Commande | `./scripts/run-p10-8-load-campaign.sh --hold-ms 45000` |
| Elapsed total | 53 554 ms (auth + 45 000 ms hold) |
| Sessions | 25 Hello OK, 25 CharacterSelect OK, TLS Required / localhost |
| Hold vs mandat | **45 000 ms / 3 600 000 ms** — `mandateDurationMet=false` |
| Actions décodées / s | 29,44 (heartbeat 428, interact 447, melee 450) |
| Heartbeat RTT | n=428 p50 0,56 ms p95 2,5 ms p99 9,25 ms |
| Interact RTT | n=447 p50 0,51 ms p95 3,21 ms p99 43,9 ms (max 2006 ms, 3 timeout) |
| Move → PositionUpdate | 0 pendant ce run (spawns empilés / collisions) ; le harness compte aussi `Error` décodé comme réponse |
| CPU process (fin) | ~0,5–4,2 % / 4 logical ; RSS ~110 MiB (générateur+serveur **même process**) |
| JSON | `/tmp/p10-8-campaign.json` (hors git, `artifacts/` ignoré) |

| Signal | Mandat | Atteint ici | Verdict |
| --- | --- | --- | --- |
| 25 sessions authentifiées | 25 | 25 (CI + campagne script) | **partiel** (in-memory, pas PG) |
| Durée 60 min | 3 600 000 ms | **&lt; 60 min** (défaut script 90 s ; CI 2,5 s) | **non clos** |
| p95 ≤ 250 ms / p99 ≤ 1 s | bout-en-bout | mesuré sur heartbeat/move/interact loopback | **indicatif loopback ≠ WAN** |
| Économie 10 mut/s × 5 min | — | non (campaign n’enchaîne pas shop/banque/trade) | **absent** |
| Interact 5/s × 60 s + rafale 20 | — | interact cadencé, pas le palier isolé | **absent** |
| Idle 300 s | — | non mesuré | **absent** |
| Reconnect 25 &lt; 60 s | — | non mesuré | **absent** |
| Pool PG ≤ 20 | — | N/A self-host mémoire | **absent** |
| CPU &lt; 80 % palier | — | échantillons process (générateur+serveur) | **indicatif** |
| Job CI ~90 min dédié | — | absent de `ci.yml` (volontaire : ne pas casser le job 35/70 min) | **reste machine dédiée** |

## Ce qui reste pour une machine dédiée

1. `--hold-ms 3600000` sur hôte 4 vCPU / 8 Gio (ou décrit).
2. Attacher le harness (`--host` / `--port`) au **serveur publié** + PostgreSQL + monde démo + TLS.
3. Distinguer CPU/RAM serveur vs générateur (deux processus).
4. Palier économie / interact / idle 300 s / restart-reconnect 25.
5. Job CI optionnel ~90 min, même SHA que la candidate — **pas** branché ici pour garder CI verte et bornée.

Ne pas lire ce rapport comme une certification 25×60.
