# Phase 10 — STATUS

**Lot courant :** P10-0 (audit + plan). **Pas READY.**

| Item | Valeur |
| --- | --- |
| Branche | `cursor/phase10-beta-release` (unique ; ne pas en ouvrir une seconde) |
| Base / tip `main` | `f74b34cca09dda819fe26747d48ee16d27007dfd` (merge PR #7 Phase 9) |
| Produit Phase 9 accepté | `cab57b94c20f86af2cc61738bdf3307ed9626ef4` |
| CI `main` post-fusion | https://github.com/Netsuno/MMO_Maker/actions/runs/35386572613 **SUCCESS** |
| CI produit Phase 9 | https://github.com/Netsuno/MMO_Maker/actions/runs/35384819869 **SUCCESS** |
| Mandat | [`MANDATE.md`](MANDATE.md) (texte complet, 2026-09-18) |
| Protocole actuel (héritage Phase 9) | `FrogWireProtocol.Version = 10` |
| Protocole figé pour Phase 10 | **v11** — voir [`SOCIAL_PROTOCOL_FREEZE.md`](SOCIAL_PROTOCOL_FREEZE.md) (pas encore implémenté) |
| Gate Phase 10 | **pas atteinte** — aucun lot P10-1…P10-9 livré |

## Décision de départ (vérifiée le 2026-09-18)

- PR #7 fusionnée ; aucune branche `cursor/phase10-beta-release` n’existait avant ce lot.
- Aucun dossier `docs/progress/phase-10-beta-release/` sur `main`.
- Aucun mandat Phase 10 plus récent dans le dépôt, les issues GitHub ou les PR.
- CI post-merge **SUCCESS** (plus « en cours » comme lors de la rédaction initiale du mandat).

## Lots

| Lot | Statut |
| --- | --- |
| P10-0 Audit + plan | **EN COURS** (ce dossier) |
| P10-1 Groupes / guildes / relations | **ABSENT** — pas commencé |
| P10-2 Échanges directs | **ABSENT** — pas commencé |
| P10-3 Client / éditeur externes | **INCOMPLET** (socle Phases 7–9 ; UX bêta absente) |
| P10-4 Monde démo + recette | **ABSENT** (seeds de tests ≠ monde démo) |
| P10-5 Sécurité externe (TLS, invitations, NAT) | **INCOMPLET** (modération Phase 9 ; TLS clair) |
| P10-6 Paquets autonomes | **INCOMPLET** (layouts scripts ; lancement client/éditeur non prouvé ; pas self-contained) |
| P10-7 Exploitation / restore | **INCOMPLET** (schéma restauré ; sanctions/guildes/échanges non certifiés) |
| P10-8 Charge 25 joueurs | **INCOMPLET** (mesures in-memory Phase 9 ; palier 25×60 min absent) |
| P10-9 Validation / candidate | **ABSENT** |

## Interdits (P10-0)

Pas d’implémentation produit P10-1…P10-9. Pas de merge. Pas de distribution. Pas de Phase 11. Pas de MariaDB nouvelle. Pas d’import `.fcc`. PostgreSQL reste la source de vérité.

## Verdict

**P10-0 seulement.** La bêta n’est **pas** prête. Les squelettes `Guild*.cs` et les boutons UI existants ne sont pas des fonctionnalités livrées.
