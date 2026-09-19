# Phase 10 — KNOWN_ISSUES

Inventaire **honnête**. Un fichier ou un bouton ≠ fonction livrée.
Gravité : **P0** = perte/duplication, compromission, monde inutilisable ; **P1** = installation / accès / parcours essentiel bloqué.

**Update propriétaire 2026-09-19 — Netsun :** P10-8 25×60 dédié, P10-4 recette 2 PCs physiques, et le playtest GUI WinForms **manuel** sont **Accepted / skipped by owner agreement — Netsun (2026-09-19); not re-run in this PR.** Ils **ne sont plus** des bloqueurs gate ouverts.

---

## Bloqueurs gate

**Aucun bloqueur produit ouvert** pour les deux items physiques ci-dessus.

Restent uniquement le **processus docs** (pas des preuves à rejouer ici) :

| Item | Gravité | État |
| --- | --- | --- |
| CI SUCCESS du tip **docs exact** (commit P10-9, après push) | Processus | Ouvert jusqu’à lecture fraîche. Mandat §6 : ne pas inventer l’URL CI de ce commit. |
| Phrase de gate | Processus | **Non écrite** — Orchestrator uniquement, après CI du tip exact. |

**Hors bloqueurs gate :** menu Playtest WinForms manuel (PC Windows local). CI Windows prouve `--smoke-launch` seulement. **Accepted / skipped by owner agreement — Netsun (2026-09-19); not re-run in this PR.** Wine n’est jamais un pass.

---

## Accepté propriétaire 2026-09-19 (plus des bloqueurs)

| Item | Preuve automatisée (dépôt) | Preuve physique | Statut |
| --- | --- | --- | --- |
| P10-8 25 joueurs × 60 min machine dédiée | Harness `campaign` hosted packaged+PG **5 s** (CI [35449733364](https://github.com/Netsuno/MMO_Maker/actions/runs/35449733364) / tip [35450339601](https://github.com/Netsuno/MMO_Maker/actions/runs/35450339601)) ; in-memory **45 s**. **Pas** de job CI 3600 s. **Pas** de latence/TPS/CPU inventés pour le run dédié. | **Accepted / skipped by owner agreement — Netsun (2026-09-19); not re-run in this PR.** — non rejouée dans cette PR | Clos par acceptation |
| P10-4 recette 2 PCs (WAN / éditeur distant / stabilité) | Fixture 3 cartes + `Phase10RecipeLoopbackTests` (1 hôte) | **Accepted / skipped by owner agreement — Netsun (2026-09-19); not re-run in this PR.** — non rejouée dans cette PR | Clos par acceptation |

---

## A. Limites Phase 9 acceptées — état Phase 10

Source historique : [`../phase-09-distribution-admin-hardening/KNOWN_ISSUES.md`](../phase-09-distribution-admin-hardening/KNOWN_ISSUES.md).

| Sujet | État réel | Lot |
| --- | --- | --- |
| **TLS** | Lots A+E : `SslStream` in-process + LoadHarness `Mode=Required` (pas AcceptAll). Proxy externe, mTLS, DPAPI : absents. | P10-5 A+E **livrés** |
| **Packaging client/éditeur** | Layout EXE hors dépôt Linux + `--smoke-launch` Windows CI. Serveur Linux **prouvé**. Menu Playtest manuel = skip Netsun. | P10-6 |
| **LOAD 25×60** | Harness court CI + **Accepted / skipped by owner agreement — Netsun (2026-09-19); not re-run in this PR.** pour le dédié. Palier économie 10 mut/s / interact isolé / idle 300 s / restart-reconnect 25 : **non mesurés en CI** (pas des bloqueurs gate après l’update). | P10-8 |
| **Restore avec sanctions** | `Phase10BackupRestoreRowsTests` CI. Chiffrement/rétention 7 **non**. | P10-7 |
| **Rate-limit login** | IP normalisée + username. [`AUTH_RATE_LIMIT.md`](AUTH_RATE_LIMIT.md). | P10-5 B **livré** |
| **Inscriptions** | Défaut local `Open`. Bêta : `ProvisionedOnly`. | P10-5 C **livré** |
| **P9-S social** | P10-1 DONE `dca2185` + P10-2 livré. Stubs `Guild.cs` / `GuildService.cs` toujours morts. | P10-1 + P10-2 |

Autres résidus (non bloqueurs gate) :

- `PRD_MMO_Maker_CSharp.md` v2.1 cité, **absent** du dépôt.
- `docs/DATA_MODEL.md` périmé vs `FrogDbContext`.
- ~100 fichiers `// TODO: Implémenter` folklore (client `Models/*`, `MaintenanceService.cs`, …).
- CI `concurrency.cancel-in-progress: true` (annulations ≠ preuves vertes).
- Annotation Node 20 / actions v4 (dépréciation).

---

## B. Items encore incomplets (non bloqueurs gate après 2026-09-19)

| Item | Preuve | Lot |
| --- | --- | --- |
| Menu Playtest WinForms éditeur → client (manuel, PC Windows local) | Hello zip + READY headless + `--smoke-launch` **CI** ; menu GUI **pas** CI | P10-3 — **Accepted / skipped by owner agreement — Netsun (2026-09-19); not re-run in this PR.** Hors bloqueurs gate |
| Résolutions 1366×768 / 1920×1080 + DPI 100/150 | Smokes non dimensionnés ainsi (PC Windows local) | P10-3 — **Accepted / skipped by owner agreement — Netsun (2026-09-19); not re-run in this PR.** |
| Jetons reconnect protégés OS (DPAPI) | `_storedAuthToken` champ UI | P10-5 — **absent**, hors update |
| Mode maintenance / drain | `MaintenanceService.cs` stub | P10-7 — **absent**, hors update |
| Chiffrement dumps / rétention 7 / durée restore 30 min | Runbook Phase 9 seulement | P10-7 — **incomplet**, hors update |
| InviteOnly avec jetons | Jalon sans jetons | P10-5 C |
| Guides captures PNG | Placeholders `guides/assets/` | P10-9 — légendes présentes, binaires absents |

---

## C. Squelettes / UI (inventaire, pas des P0)

- `Frog.Server/Models/Guild.cs`, `Frog.Server/Services/GuildService.cs` — stubs. Ne pas les remplir.
- Client : slash `/party` `/guild` `/friend` `/block` `/trade` + canaux Party/Guild. Pas de panneau Ami/Groupe/Guilde dédié.
- Menu « Publier vers MariaDB… (héritage) » encore visible.
- `Phase8JsonEditorPanel` non branché (fichier mort).
- Compose `POSTGRES_USER=frog` = superuser **démo locale**. Hébergé : `frog_runtime` ([`POSTGRES_ROLES.md`](POSTGRES_ROLES.md)).

---

## D. Acquis à ne pas casser

- Transactions économie Phase 7 + identités de requête Phase 8.
- C2/C2b : `SessionTeardown` ; ban vs login/reconnect (`cab57b9`).
- Dispose éditeur / `EditorMainFormCloseCoordinator`.
- Suites Phase 9 acceptées : Frog.Tests **454** / PG **181** / éditeur **87×3** / gameplay **6×3** / Phase 8 **24×3** + manifeste 12 SHA-256 (CI 35384819869 / 35386572613).
- Deadlock ouverture carte corrigé (`3e1d04d`).
- PostgreSQL SoT ; MariaDB héritage gelé.

---

## E. Ce que P10-9 docs candidate ne clôt pas

Ce lot **n’écrit pas** la phrase de gate et **n’invente pas** un CI 60 min. Il aligne le statut exact : preuves automatisées vs acceptation propriétaire. Pas de merge. Pas de diffusion. Pas de Phase 11.
