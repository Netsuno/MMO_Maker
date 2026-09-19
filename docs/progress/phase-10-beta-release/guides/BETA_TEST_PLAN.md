# Plan de test bêta

Paquet docs candidate (P10-9). Distingue **automatisable (CI / 1 hôte)** vs **2 machines physiques**.

**Update 2026-09-19 ~11:21 ET — Marc Giroux :** recette **2 PCs physiques** (WAN / éditeur distant / stabilité) = **DONE / accepted by owner Marc Giroux on 2026-09-19**. **Non rejouée** dans cette PR. Pas de captures inventées.

## Prérequis campagne

| # | Prérequis | Automatisé | Physique |
| --- | --- | --- | --- |
| 1 | Paquets client (2 PC) + serveur | Layout + SHA-256 CI ; `--smoke-launch` Windows CI [35403209506](https://github.com/Netsuno/MMO_Maker/actions/runs/35403209506) | Inclus dans l’acceptation 2 PCs |
| 2 | Comptes / invitations | OpsCli + `ProvisionedOnly` | Inclus dans l’acceptation 2 PCs |
| 3 | Transport chiffré validé | TLS unitaires PASS ; harness Required | Inclus dans l’acceptation 2 PCs |
| 4 | Monde démo publié | Fixture PG + `Frog.DemoWorld` | Inclus dans l’acceptation 2 PCs |
| 5 | Canal bugs + [modèle](BUG_REPORT_TEMPLATE.md) | Modèle présent | Canal humain = ops |

## Matrice 12 étapes — automate vs 2 machines

| # | Rôle | Scénario | Automatisable (CI / 1 hôte) | 2 machines physiques | Statut |
| --- | --- | --- | --- | --- | --- |
| 1 | Joueur A | Install client | Zip + `Frog.Client.exe --smoke-launch` | Testeur A réel | Smoke CI Windows ; install humaine **accepted by owner** (recette 2 PCs) |
| 2 | Joueur B | Install client machine 2 | Non (deuxième OS) | **Oui** | **DONE / accepted by owner Marc Giroux on 2026-09-19** |
| 3 | A+B | Connexion | TCP login 2 clients loopback | **Oui** si NAT / IP / TLS réel | Loopback **oui** ; WAN **accepted by owner** |
| 4 | A+B | Personnages | TCP create/select | Idem | Loopback **oui** + acceptation propriétaire |
| 5 | A+B | Présence même carte | TCP 2 sessions même process | **Oui** latence / NAT / firewall | Loopback **oui** ; WAN **DONE / accepted by owner Marc Giroux on 2026-09-19** |
| 6 | A | Gameplay de base | Gameplay / Phase 8 smokes from-source | HUD réel | Smokes internes + acceptation recette |
| 7 | A+B | Social | `Phase10SocialTcpTests` / PG | Chat réel 2 PCs | Automatisé loopback + acceptation propriétaire |
| 8 | A+B | Échange | `Phase10TradeTcpTests` / PG | UI `TradeForm` à deux souris | Automatisé loopback + acceptation propriétaire |
| 9 | Auteur | Publish mineur | `Frog.DemoWorld` + `phase10-recipe-automated.sh` | Éditeur **paquet** → joueur distant voit le warp | Fixture **oui** ; distant **DONE / accepted by owner Marc Giroux on 2026-09-19** |
| 10 | Ops | Backup / restore | `Phase10BackupRestoreRowsTests` | Non requis pour la preuve CI | **CI** |
| 11 | Ops | Sanction | mute/ban TCP + restore banni rejeté | Effet HUD distant | Automatisé loopback + acceptation propriétaire |
| 12 | A+B | Stabilité 30–60 min | Harness P10-8 `campaign` (≠ 2 clients GUI) | **Oui** (2 clients idle humains) | Harness court CI **plus** **DONE / accepted by owner Marc Giroux on 2026-09-19** |

Les étapes **2, 5 (WAN), 9 (éditeur distant), 12** ne peuvent pas être cochées par un agent Linux seul. Elles sont **closes par acceptation propriétaire**, pas par un replay dans cette PR.

## Scénarios (12) — journal

Cocher **Statut** seulement avec preuve (CI nommée **ou** acceptation propriétaire datée). Ne pas inventer de SHA de captures.

| # | Rôle | Scénario | Attendu | Statut |
| --- | --- | --- | --- | --- |
| 1 | Joueur A | Install client | App démarre | Smoke `--smoke-launch` CI Windows + recette 2 PCs **accepted by owner** |
| 2 | Joueur B | Install client | App démarre machine 2 | **DONE / accepted by owner Marc Giroux on 2026-09-19** |
| 3 | A+B | Connexion | Session OK | `Phase10RecipeLoopbackTests` ; WAN **accepted by owner** |
| 4 | A+B | Personnages | En carte | `Phase10RecipeLoopbackTests` + acceptation propriétaire |
| 5 | A+B | Présence | Se voient | Move/chat loopback ; WAN **DONE / accepted by owner Marc Giroux on 2026-09-19** |
| 6 | A | Gameplay de base | Pas de blocage P0 | Smokes from-source + acceptation recette |
| 7 | A+B | Social minimal | Action OK | Tests TCP/PG + acceptation propriétaire |
| 8 | A+B | Échange | Pas de dup / perte | Tests TCP/PG + acceptation propriétaire |
| 9 | Auteur | Publish mineur | Visible in-game | Fixture démo CI ; distant **DONE / accepted by owner Marc Giroux on 2026-09-19** |
| 10 | Ops | Backup / restore | Login après restore ; banni refusé | **CI** `Phase10BackupRestoreRowsTests` |
| 11 | Ops | Sanction | Effet visible | Tests + restore |
| 12 | A+B | Stabilité courte | Inventaire cohérent | Harness P10-8 court ≠ 2 GUI ; stabilité 2 PCs **DONE / accepted by owner Marc Giroux on 2026-09-19** |

Charge 25×60 dédiée : voir [`../LOAD_REPORT.md`](../LOAD_REPORT.md) — acceptation propriétaire distincte de ce journal 12 étapes.
