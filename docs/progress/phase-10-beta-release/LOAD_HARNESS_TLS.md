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

## P10-8 (charge 25×60, pas encore exécutée)

```bash
./scripts/run-load-harness.sh \
  --host VOTRE_HOTE --port 6000 \
  --tls-mode Required \
  --tls-target-host VOTRE_HOTE \
  --tls-ca-path /chemin/confine/ca.pem \
  --sessions 25 --scenario mixed --hold-ms 3600000
```

Self-host TLS (preuve locale) : `--tls-mode Required --tls-target-host localhost --tls-cert-path leaf.pem --tls-key-path leaf.key --tls-ca-path ca.pem`.

Tests : `Phase10LoadHarnessTlsTests` (Hello + CA inconnue + scan AcceptAll). Le palier 25×60 min + PG + monde publié reste **P10-8**.
