# Phase 10 — STATUS (P10-9 docs candidate)

Paquet documentation **candidate** pour relecture Netsun. Ce fichier n’annonce pas une sortie.
Aucun claim marketing de bêta distribuable. La phrase de gate n’est **pas** écrite ici
(réservée à l’Orchestrator après CI SUCCESS du tip docs exact).

**Acceptation / skip propriétaire 2026-09-19 — Netsun :**

1. Campagne P10-8 **25 joueurs × 60 min** réelle (machine dédiée) — **Accepted / skipped by owner agreement — Netsun (2026-09-19); not re-run in this PR.**
2. Recette P10-4 **2 PCs physiques** (WAN / éditeur distant / stabilité) — **Accepted / skipped by owner agreement — Netsun (2026-09-19); not re-run in this PR.**

Ces deux preuves **physiques n’ont pas été rejouées dans cette PR**. Aucun chiffre de latence, TPS ou CPU n’est inventé pour le run 60 min. Aucun job CI 60 min n’existe.

| Item | Valeur |
| --- | --- |
| Branche | `cursor/phase10-beta-release` (unique ; PR Draft [#8](https://github.com/Netsuno/MMO_Maker/pull/8)) |
| Base / tip `main` | `f74b34cca09dda819fe26747d48ee16d27007dfd` (merge PR #7 Phase 9) |
| Produit Phase 9 accepté | `cab57b94c20f86af2cc61738bdf3307ed9626ef4` |
| CI `main` post-fusion | https://github.com/Netsuno/MMO_Maker/actions/runs/35386572613 **SUCCESS** |
| Tip docs précédent | `a489379f91859a66b07380b2dfb0b68c979770ba` |
| CI du tip `a489379` | https://github.com/Netsuno/MMO_Maker/actions/runs/35450339601 **SUCCESS** (`build-and-test` + `postgres-integration`) |
| Produit vert antérieur | `814b8ba5380a952225b0f087bb2c9f0ecb0b912c` · CI https://github.com/Netsuno/MMO_Maker/actions/runs/35449733364 **SUCCESS** (playtest zip + hosted P10-8 **5 s**) |
| Mandat | [`MANDATE.md`](MANDATE.md) (texte 2026-09-18) + update propriétaire 2026-09-19 |
| Protocole runtime | **v11** — [`SOCIAL_PROTOCOL_FREEZE.md`](SOCIAL_PROTOCOL_FREEZE.md) opcodes 80–86 |
| Gate Phase 10 | **non écrite** — docs candidate seulement |

## Classes de preuve (ne pas mélanger)

| Classe | Quoi | Preuve |
| --- | --- | --- |
| Automatisée (dépôt / CI) | Harness hosted packaged+PG **~5 s** (`--profile ci`) ; in-memory **~45 s** ; loopback recette ; zip Hello / READY headless ; EXE `--smoke-launch` Windows | SHA/CI ci-dessus + [`LOAD_REPORT.md`](LOAD_REPORT.md) + [`P10-3-PACKAGED-PLAYTEST.md`](P10-3-PACKAGED-PLAYTEST.md) |
| Physique **acceptée propriétaire** | 25×60 min machine dédiée ; recette 2 PCs (WAN / éditeur distant / stabilité) | **Accepted / skipped by owner agreement — Netsun (2026-09-19); not re-run in this PR.** — **pas** rejouée ici, **pas** un run CI 60 min |
| Skip PC Windows local (hors bloqueurs gate) | Menu Playtest WinForms manuel (éditeur → spawn client) ; HUD 1366×768 / 1920×1080 | **Accepted / skipped by owner agreement — Netsun (2026-09-19); not re-run in this PR.** CI Windows prouve `--smoke-launch` seulement |

## Lots

| Lot | Statut |
| --- | --- |
| P10-0 Audit + plan | **FAIT** |
| P10-1 Groupes / guildes / relations | **DONE** tip `dca2185` |
| P10-2 Échanges directs | **LIVRÉ** — opcodes 84–86, TX PG, replay, block sur invites |
| P10-3 Client / éditeur externes | **P10-3a + zip Hello CI** ; `--smoke-launch` Windows CI. Menu Playtest WinForms manuel : **Accepted / skipped by owner agreement — Netsun (2026-09-19); not re-run in this PR.** (hors bloqueurs gate) |
| P10-4 Monde démo + recette | **FAIT** — fixture 3 cartes **livrée** ; loopback CI **oui** ; recette 2 PCs physiques **Accepted / skipped by owner agreement — Netsun (2026-09-19); not re-run in this PR.** |
| P10-5 Sécurité externe | **PASS sécu** A–E (TLS Windows unitaires verts) |
| P10-6 Paquets autonomes | Layout+SHA Linux **et** `--smoke-launch` Windows **CI 35403209506 SUCCESS**. Wine Linux ≠ pass. Jeu GUI local : skip P10-3 / P10-4 ci-dessus. |
| P10-7 Exploitation / restore | **INCOMPLET (non bloqueur gate)** — dump/restore **lignes** sanctions/guildes/amis/trades + serveur publié **CI** ; chiffrement/rétention/durée 30 min **non** |
| P10-8 Charge 25 joueurs | **Accepted / skipped by owner agreement — Netsun (2026-09-19); not re-run in this PR.** pour le palier 25×60 dédié. Harness court CI **5 s / 45 s** seulement (voir [`LOAD_REPORT.md`](LOAD_REPORT.md)) |
| P10-9 Validation / candidate | **EN COURS (ce lot)** — docs candidate poussées ; phrase gate **interdite ici** |

## Bloqueurs gate

**Aucun** des deux items physiques (25×60 dédié, recette 2 PCs) n’est plus un bloqueur ouvert.

Restent uniquement le **processus docs**, pas des preuves produit à rejouer :

| Item | État |
| --- | --- |
| CI SUCCESS du **tip docs exact** (ce commit, après push) | Ouvert jusqu’à lecture fraîche — mandat §6 : ne pas auto-pinner son propre SHA/CI |
| Phrase de gate | **Non écrite** — Orchestrator uniquement, après CI du tip exact |

Menu Playtest WinForms manuel (PC Windows local, pas le smoke CI) : **Accepted / skipped by owner agreement — Netsun (2026-09-19); not re-run in this PR.** Hors bloqueurs gate.

## File (déjà landed, ne pas rejouer)

| Lot | Tip | Statut |
| --- | --- | --- |
| P10-5 A TLS SslStream | `ea116afae9ee1a84c8d80e08ae9f6f0bf2e7af3b` | **PASS** TLS Windows unitaires verts |
| P10-2 Échanges 84–86 | `bd8462dba2c00e560ccde61ef30e411d0fd8ee94` | **livré** (block invites inclus) |
| P10-3a Settings / aide / rebind | `4ea44de675642180fc5a39989bd22b9f78eb40f6` | **livré** (fix CI `ab1bec5`) |
| P10-5 B Rate-limit | `6e73e22141447461c52878e0580dcc72b9841bb5` | **livré** |
| P10-5 C ClosedBeta + OpsCli | `726e037b4e5cd4943c1e076f732385f4d76cc76b` | **livré** |
| P10-5 D PG least-privilege | `8f06cde0cec35fefb3c27817b1e7090562633649` | **livré** |
| P10-5 E LoadHarness TLS | `b58a02a03d269ac1e89cc812638e90badb7b9634` | **livré** |
| Deadlock éditeur | `3e1d04d947c8435c38fdfbbfb1d2336106ef3f52` | **corrigé** |
| Harness P10-8 + zip playtest | `15e7a10` / `5ca346e` / `7d1ef9f` | **livré** (campagne courte) |

## Éditeur — deadlock ouverture (sync-over-async)

Cause : `MapEventsPostgreSqlService.LoadPlacementsForMap` (et les autres `Load*` / `Try*` sync) faisaient `_gate.ExecuteAsync(...).ConfigureAwait(false).GetAwaiter().GetResult()` depuis le thread UI WinForms. Correctif : `RunOffUiSyncContext` = `Task.Run(work).GetResult()`. Tests : `Phase10EditorSyncOverAsyncTests` + `Phase10EditorSyncOverAsyncSmokeTests`.

## P10-8 — PROUVÉ (automatisé) vs ACCEPTÉ (propriétaire)

Voir [`LOAD_REPORT.md`](LOAD_REPORT.md).

- **Automatisé :** scénario `campaign` (25 sessions, TLS Required), hosted packaged+PG **5 000 ms** (`mandateDurationMet=false`), in-memory **45 000 ms**, test CI ~2,5 s.
- **Accepté propriétaire :** 25×60 min machine dédiée — **Accepted / skipped by owner agreement — Netsun (2026-09-19); not re-run in this PR.** Pas de métriques inventées. Pas de job CI 3600 s.

## P10-4 — PROUVÉ (automatisé) vs ACCEPTÉ (propriétaire)

- **Automatisé :** fixture 3 cartes ; `Phase10RecipeLoopbackTests` / `run-p10-4-recipe-loopback.sh` (loopback 1 hôte).
- **Accepté propriétaire :** étapes 2 / 5-WAN / 9-distant / 12 sur **2 machines physiques** — **Accepted / skipped by owner agreement — Netsun (2026-09-19); not re-run in this PR.** Pas rejoué dans cette PR.

## P10-3 playtest zip — PROUVÉ vs HORS GATE

Voir [`P10-3-PACKAGED-PLAYTEST.md`](P10-3-PACKAGED-PLAYTEST.md). **PROUVÉ CI :** zip serveur hors dépôt → Hello (Linux) ; layouts frères + Hello (Windows) ; `--smoke-launch`. Menu Playtest WinForms manuel : **Accepted / skipped by owner agreement — Netsun (2026-09-19); not re-run in this PR.**

## P10-6 / P10-7 — rappel court

P10-6 : layout + SHA hors dépôt (Linux) + `--smoke-launch` Windows [35403209506](https://github.com/Netsuno/MMO_Maker/actions/runs/35403209506). Wine ≠ pass.

P10-7 : [`RESTORE_REPORT.md`](RESTORE_REPORT.md) — lignes sanctions/social/trade + serveur publié **CI**. Chiffrement dumps / rétention 7 / 30 min : **non** (non listé comme bloqueur gate par l’update 2026-09-19).

## Interdits (toujours)

Pas de merge. Pas de distribution. Pas de Phase 11. Pas de claim marketing de sortie. Pas de `PHASE 10 GATE REACHED` dans ce lot docs.

## Verdict docs

P10-0…P10-2, P10-3a, P10-4 fixture+loopback, P10-5, P10-6 CI, P10-7 CI, deadlock éditeur, harness P10-8 borné, playtest zip : **dans le dépôt**. P10-4 2 PCs et P10-8 25×60 dédié : **Accepted / skipped by owner agreement — Netsun (2026-09-19); not re-run in this PR.** Ce lot livre le paquet docs candidate. L’Orchestrator tranche la gate après CI du tip exact.
