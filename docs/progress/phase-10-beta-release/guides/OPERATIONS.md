# Operations — bêta (brouillon Phase 10)

> **Statut : BROUILLON P10-0 — Phase 10 pas prête. Ne pas diffuser comme procédure livrée.**

## Source actuelle (Phase 9 sur `main`)

Tant que les runbooks Phase 10 ne sont pas complets, utilisez :

| Document | Lien |
| --- | --- |
| OPERATIONS_RUNBOOK | [`../../phase-09-distribution-admin-hardening/OPERATIONS_RUNBOOK.md`](../../phase-09-distribution-admin-hardening/OPERATIONS_RUNBOOK.md) |
| BACKUP_RESTORE_RUNBOOK | [`../../phase-09-distribution-admin-hardening/BACKUP_RESTORE_RUNBOOK.md`](../../phase-09-distribution-admin-hardening/BACKUP_RESTORE_RUNBOOK.md) |
| PACKAGING_GUIDE | [`../../phase-09-distribution-admin-hardening/PACKAGING_GUIDE.md`](../../phase-09-distribution-admin-hardening/PACKAGING_GUIDE.md) |
| SECURITY_MODEL | [`../../phase-09-distribution-admin-hardening/SECURITY_MODEL.md`](../../phase-09-distribution-admin-hardening/SECURITY_MODEL.md) |

## Rappels non négociables

- PostgreSQL 16 = source de vérité. MariaDB non requis.
- Ne jamais committer `appsettings.Local.json`.
- Placeholders (`NOT_A_PRODUCTION_SECRET`, etc.) ≠ secrets réels.
- Bind public seulement avec `allowNonLoopbackBind=true` + config réelle.

<!-- CAPTURE: assets/ops-01-server-start.png — démarrage serveur ; masquer DSN -->
*Capture à venir : démarrage serveur (expurger secrets).*

## Écarts Phase 10 (à traiter avant diffusion externe)

Inventaire honnête : [`../KNOWN_ISSUES.md`](../KNOWN_ISSUES.md).

| Sujet | État | Lot |
| --- | --- | --- |
| TLS + certificat validé | Absent (TCP clair) | P10-5 |
| Inscriptions fermées / invitations | Absent | P10-5 |
| Outils opérateur (reset MDP, revoke, grant GM) | Incomplete | P10-5 |
| Client/éditeur packaged prouvés | Incomplete | P10-6 |
| Restore avec lignes mute/ban (+ social/trade) | Incomplete | P10-7 |
| Charge 25 joueurs × 60 min | Absent | P10-8 |

## Dossier assets

Les captures ops iront dans [`assets/`](assets/). Aucune image secrète.

## Wiki

Miroir : [Ops](https://github.com/Netsuno/MMO_Maker/wiki/Ops)
