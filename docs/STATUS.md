# STATUS

Journal **actif** du dépôt. Les rapports de phase dans `docs/progress/phase-0N-*/` conservent leur texte d’époque ; un bandeau daté indique l’acceptation ultérieure.

| Phase | Status |
| --- | --- |
| Phase 7 | **ACCEPTED** on main |
| Phase 8 | **ACCEPTED** on main (merge `1cd57ba`, PR #2) |
| Phase 9 | **ACCEPTED** on main (merge `f74b34cca09dda819fe26747d48ee16d27007dfd`, PR #7) — 2026-09-18 |
| Phase 10 | **P10-5 A landed `ea116afa` — CI pending — NOT READY.** File P10-2 / P10-3a / B–E / docs DA déjà sur `cursor/phase10-beta-release`. Pas de gate. |

## Phase 10 (actif)

- Mandat : [`progress/phase-10-beta-release/MANDATE.md`](progress/phase-10-beta-release/MANDATE.md)
- Plan : [`progress/phase-10-beta-release/PHASE_PLAN.md`](progress/phase-10-beta-release/PHASE_PLAN.md)
- Gel social : [`progress/phase-10-beta-release/SOCIAL_PROTOCOL_FREEZE.md`](progress/phase-10-beta-release/SOCIAL_PROTOCOL_FREEZE.md) (**v11** P10-1 opcodes 80–83 tip `dca2185` ; P10-2 opcodes 84–86)
- Base : `f74b34cca09dda819fe26747d48ee16d27007dfd`
- Produit Phase 9 accepté : `cab57b94c20f86af2cc61738bdf3307ed9626ef4`
- CI `main` post-merge : https://github.com/Netsuno/MMO_Maker/actions/runs/35386572613 **SUCCESS**
- CI produit Phase 9 : https://github.com/Netsuno/MMO_Maker/actions/runs/35384819869 **SUCCESS** (Frog.Tests **454** / PG **181** / editor **87×3** / gameplay **6×3** / Phase 8 **24×3** + 12 exact-sha)

P10-5 A : **landed** tip `ea116afa` — CI **pending**, pas READY. File déjà sur la branche : docs référence DA (`ebf3437`) · P10-2 · P10-3a · B rate-limit · C ClosedBeta · D PG · E LoadHarness. P10-1 DONE `dca2185`. P10-4 / P10-6…P10-9 absents ou incomplets. Pas de merge.

Protocole runtime (cette branche) : **v11**. Ne pas fusionner. Ne pas diffuser. Pas de Phase 11. **Pas READY.**

## Phase 9 (historique daté — acceptée)

Acceptation Marc 2026-09-18. Merge PR #7 `f74b34c`. Tip produit `cab57b9`.

Le dossier [`progress/phase-09-distribution-admin-hardening/`](progress/phase-09-distribution-admin-hardening/) contient encore les rapports de re-revue (**NOT READY**, C-fixes, C2 follow-up) rédigés **avant** cette décision. Les lire comme archive, pas comme statut actif.

Résidus **non certifiés** repris en Phase 10 : TLS clair ; lancement packaged client/éditeur ; LOAD idle/économie/interact/restart/pool/25×60 ; restore avec **lignes** mute/ban ; rate-limit IP:port ; P9-S social.

P9-S reste la décision Phase 9 « différé » — le travail est désormais P10-1 / P10-2.

## Phase 8 (historique)

ACCEPTED, merge `1cd57ba`. README d’alignement : PR #6. Protocole v10 (`activationId`).
