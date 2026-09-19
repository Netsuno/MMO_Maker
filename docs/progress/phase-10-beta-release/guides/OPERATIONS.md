# Operations — stack bêta

Démarre, arrête, provisionne, sauvegarde et restaure le serveur de bêta. **Pas** un guide joueur.

Références Phase 9 (toujours valides pour les commandes numérotées) :

- [OPERATIONS_RUNBOOK](../../phase-09-distribution-admin-hardening/OPERATIONS_RUNBOOK.md)
- [BACKUP_RESTORE_RUNBOOK](../../phase-09-distribution-admin-hardening/BACKUP_RESTORE_RUNBOOK.md)
- [PACKAGING_GUIDE](../../phase-09-distribution-admin-hardening/PACKAGING_GUIDE.md)

Phase 10 : [`../POSTGRES_ROLES.md`](../POSTGRES_ROLES.md), [`../CLOSED_BETA.md`](../CLOSED_BETA.md), [`../RESTORE_REPORT.md`](../RESTORE_REPORT.md), [`../LOAD_HARNESS_TLS.md`](../LOAD_HARNESS_TLS.md).

## Prérequis

- PostgreSQL 16
- Paquet `server-linux-x64` (self-contained) **ou** SDK 8 pour from-source
- Overlay **gitignoré** (`appsettings.Local.json`) — jamais committer les secrets
- Profil hébergé : rôle **`frog_runtime`** NOSUPERUSER DML-only (compose `frog` superuser = **démo locale seulement**)

## 1. Démarrer (from-source, dev)

1. Démarrer PostgreSQL (Compose du dépôt = démo, pas le profil hébergé).
2. Copier l’exemple d’overlay → fichier **gitignoré**.
3. Renseigner la connexion via overlay **ou** variable d’environnement du runbook (valeurs hors git).
4. `dotnet run --project Frog.Server/Frog.Server.csproj`

**Résultat :** processus qui écoute ; logs sans secret dans les tickets.

## 2. Démarrer (paquet Linux)

```bash
./scripts/publish-frog.sh --target server-linux-x64
# extraire / copier hors git, puis overlay Local, puis :
./Frog.Server
```

TLS parcours externe : `Server:Tls:Mode=Required` + certificat (PEM/PFX). Pas de repli clair silencieux. Fail-fast si bind non-loopback sans cert. Voir P10-5.

Bêta comptes : `Registration:Mode=ProvisionedOnly` (TCP Register refusé). Défaut local `Open`.

## 3. Comptes opérateur (`tools/Frog.OpsCli`)

| Action | Note |
| --- | --- |
| create | Compte joueur ≠ grant GM |
| reset-password | Sans connaître l’ancien mot de passe |
| session-revoke | Invalide les sessions |
| operator grant\|revoke | Écrit `auth.operators` |
| sanctions | mute / kick / ban |

Invitation ≠ rôle GM. InviteOnly sans jetons = jalon documenté, pas un système de codes.

## 4. Sauvegarde / restauration

Runbook Phase 9 + preuve P10-7 :

```bash
# voir RESTORE_REPORT.md — CI : pg_dump -Fc → restore → Frog.Server publié
# login OK / ban rejeté ; guildes, amis, trades rejouables
```

**Résultat attendu CI :** login possible après restore ; compte banni refusé.

**Incomplet (non bloqueur gate 2026-09-19) :** chiffrement dumps, rétention 7, durée 30 min, drain / `MaintenanceService`.

## 5. Charge (ne pas confondre les profils)

| Profil | Hold | Où |
| --- | --- | --- |
| `ci` | 5 s | CI postgres-integration ([35449733364](https://github.com/Netsuno/MMO_Maker/actions/runs/35449733364) / [35450339601](https://github.com/Netsuno/MMO_Maker/actions/runs/35450339601)) |
| `cloud` | 45 s | Script hosted / in-memory documenté |
| `dedicated` | 3600 s | Machine dédiée — **Accepted / skipped by owner agreement — Netsun (2026-09-19); not re-run in this PR.** ; **pas** un job CI ; **pas** de métriques inventées ici |

```bash
./scripts/phase10-hosted-load-campaign.sh --profile ci
```

Détail : [`../LOAD_REPORT.md`](../LOAD_REPORT.md).

## 6. Testable vs limites

| Sujet | Testable maintenant | Limite honnête |
| --- | --- | --- |
| Serveur Linux publié | Oui (Phase 9 + P10-3 zip Hello) | — |
| Client / éditeur paquet | `--smoke-launch` Windows CI | Menu Playtest WinForms manuel : **Accepted / skipped by owner agreement — Netsun (2026-09-19); not re-run in this PR.** |
| TLS + cert validé | Unitaires + harness Required | Proxy externe / mTLS absents |
| Inscriptions fermées | `ProvisionedOnly` + OpsCli | InviteOnly sans jetons |
| Recette 2 PCs | Loopback CI | **Accepted / skipped by owner agreement — Netsun (2026-09-19); not re-run in this PR.** |
| Charge 25×60 | Harness 5 s / 45 s | Dédié **Accepted / skipped by owner agreement — Netsun (2026-09-19); not re-run in this PR.** ; pas de CI 60 min |
| Restore lignes métier | `Phase10BackupRestoreRowsTests` | Chiffrement / rétention non |

## Secrets

- Nommer les variables / fichiers (`appsettings.Local.json`, DSN Postgres).
- **Jamais** coller les valeurs dans ce guide, le wiki, ou un ticket public.
- Diagnostics client : bouton expurgé — ne pas y coller un jeton.

## Et après ?

- [KNOWN_ISSUES](../KNOWN_ISSUES.md)
- [CANDIDATE_CHECKLIST](../CANDIDATE_CHECKLIST.md)
- Wiki : [Ops](https://github.com/Netsuno/MMO_Maker/wiki/Ops)
