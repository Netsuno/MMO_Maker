# Phase 10 — Checklist candidate (P10-9)

Grille de relecture Marc. **Pas** un claim de sortie. La phrase de gate n’appartient **pas** à ce fichier.

Légende preuve : **CI/SHA** = automatisé dans le dépôt · **owner 2026-09-19** = accepté par Marc Giroux, non rejoué ici · **ouvert** = encore vrai.

## Mandat §7 (huit critères) — état honnête

| # | Critère | Preuve | État |
| --- | --- | --- | --- |
| 1 | Testeur installe/joue avec le client livré ; auteur modifie/publie avec l’éditeur livré | `--smoke-launch` Windows CI [35403209506](https://github.com/Netsuno/MMO_Maker/actions/runs/35403209506) ; chemins paquet ; recette 2 PCs **owner 2026-09-19** | Présent (CI + owner). GUI Linux **hors gate** |
| 2 | Parcours PG + paquets + deux machines | Loopback CI + fixture ; 2 PCs WAN / éditeur distant **owner 2026-09-19** | Présent (CI + owner) |
| 3 | Groupes, guildes, relations, échanges | P10-1 `dca2185` + P10-2 `bd8462d` + suites TCP/PG | **présent** |
| 4 | Connexion chiffrée, droits serveur, pas de secret dans les paquets | P10-5 A–E PASS ; scan certs prod absents ; overlay Local gitignoré | **présent** (jeton client en RAM / DPAPI **absent**, hors update) |
| 5 | 25 joueurs + stabilité | Harness CI **5 s / 45 s** ([LOAD_REPORT.md](LOAD_REPORT.md)) **plus** 25×60 dédié **owner 2026-09-19**. **Pas** de job CI 60 min, **pas** de latence dédiée inventée | Présent (CI courte + owner) |
| 6 | Restauration données réelles de test, redémarrage | [RESTORE_REPORT.md](RESTORE_REPORT.md) CI (lignes + serveur publié). Chiffrement/rétention **non** | **présent CI** ; limites ops nommées, non bloqueurs update |
| 7 | Aucun P0/P1 connu sur parcours obligatoire | [KNOWN_ISSUES.md](KNOWN_ISSUES.md) — 25×60 et 2 PCs **retirés** des bloqueurs | Bloqueurs physiques **clos par owner**. Processus : CI tip docs + phrase gate |
| 8 | Artefacts identités cohérentes, CI tip final vert, PR évaluable | Pins `a489379` / `814b8ba` ci-dessus. CI du **tip docs P10-9** : lecture après push (mandat §6) | **incomplet processus** — ce commit ne s’auto-certifie pas |

## Lots P10-0…P10-9

| Lot | Checklist | État |
| --- | --- | --- |
| P10-0 | Dossier, matrice, gel v11 | **présent** |
| P10-1 | Social 80–83 | **présent** |
| P10-2 | Trade 84–86 | **présent** |
| P10-3 | Client/éditeur externes | **incomplet / hors gate** Linux GUI ; 3a + zip Hello **présent** |
| P10-4 | Fixture + recette 2 PCs | Fixture/loopback **CI** ; 2 PCs **owner 2026-09-19** |
| P10-5 | TLS, rate-limit, closed beta, rôles, harness TLS | **présent** (PASS sécu) |
| P10-6 | Paquets self-contained + SHA | Layout + smoke Windows **CI** ; installer/MAJ **absent** (différé mandat) |
| P10-7 | Restore lignes | **présent CI** ; drain/chiffrement **incomplet** (non bloqueur update) |
| P10-8 | Charge 25×60 | Harness court **CI** ; dédié **owner 2026-09-19** |
| P10-9 | Docs candidate | **ce paquet** — phrase gate **absente** |

## Interdit dans ce lot

- Écrire la phrase de gate.
- Déclarer une sortie marketing.
- Inventer un CI 60 min ou des p95/p99/CPU du run dédié.
- Merger, diffuser, ouvrir Phase 11.
