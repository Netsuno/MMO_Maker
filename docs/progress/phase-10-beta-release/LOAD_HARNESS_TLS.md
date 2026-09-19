# LoadHarness TLS (P10-5 lot E) — usage P10-8

Le générateur `tools/Frog.LoadHarness` parle **SslStream** avec `Mode=Required` + `TargetHost`. Validation **stricte** (`TlsCertificateValidator`). **Aucun** AcceptAll.

## Modes

| Mode | Usage |
| --- | --- |
| `Off` (défaut) | smokes Phase 9 in-memory, clair |
| `Required` | **profil P10-8** (hébergé + TLS) |

Pas de mode optionnel, pas de repli clair silencieux.

## CA de test

`--tls-ca-path` pointe vers un PEM **confiné** (répertoire temp, jamais Git). Le client ancre cette CA (`CustomRootTrust`). Sans `--tls-ca-path`, ancre **système** (certificat public hébergé) — toujours une vraie chaîne, jamais « tout accepter ».

## P10-8 (charge)

```bash
# Preuve cloud / CI-sized (défaut 90 s)
./scripts/run-p10-8-load-campaign.sh

# Mandat 60 min — machine dédiée seulement
./scripts/run-p10-8-load-campaign.sh --hold-ms 3600000

# Attacher un serveur déjà lancé
./scripts/run-load-harness.sh \
  --host VOTRE_HOTE --port 6000 \
  --tls-mode Required \
  --tls-target-host VOTRE_HOTE \
  --tls-ca-path /chemin/confine/ca.pem \
  --sessions 25 --scenario campaign --hold-ms 3600000
```

Self-host TLS : le script campaign émet une CA confinée (`--emit-test-certs`). `--tls-mode Required --tls-target-host localhost --tls-cert-path leaf.pem --tls-key-path leaf.key --tls-ca-path ca.pem`.

Hosted packaged + PostgreSQL (serveur hors dépôt) :

```bash
./scripts/phase10-hosted-load-campaign.sh --profile ci         # hold 5 s (CI)
./scripts/phase10-hosted-load-campaign.sh --profile cloud      # hold 45 s
./scripts/phase10-hosted-load-campaign.sh --profile dedicated  # hold 3600 s
```

Tests : `Phase10LoadHarnessTlsTests` (Hello + CA inconnue + **campaign 25×TLS** + scan AcceptAll). Rapport : [`LOAD_REPORT.md`](LOAD_REPORT.md). Le palier **60 min** n’est pas clos.
