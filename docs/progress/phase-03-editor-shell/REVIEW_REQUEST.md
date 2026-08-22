# Demande de revue — Phase 3 (Shell éditeur)

## Demande

Merci de valider le **gate Phase 3** avant tout démarrage de la Phase 4 (Map Editor MVP).

## Critères PRD

| Critère | État |
| --- | --- |
| Workspace documenté | OK — `docs/EDITOR_WORKSPACE.md` |
| Shell (arbre / canvas / panneaux / status) | OK — coque WPF existante + toolbar |
| Catalogue via Application | OK — `MapWorkspaceSession` + `ListSummaries` |
| Carte démo ouverte | OK (logique session + seed) ; **UI Windows non observée** |
| Pas de DB dans Forms | OK — tests architecture |
| UI réactive | **NON VÉRIFIÉ** (Linux) |

## Preuves

- [`PHASE_REPORT.md`](PHASE_REPORT.md)
- [`TEST_RESULTS.md`](TEST_RESULTS.md) — 109 + 7 verts
- [`CHANGE_SUMMARY.md`](CHANGE_SUMMARY.md)
- [`KNOWN_ISSUES.md`](KNOWN_ISSUES.md)

## Question de gate

1. **Accepter** Phase 3 avec réserve smoke Windows et autoriser Phase 4 ?
2. **Bloquer** jusqu’à un smoke Windows manuel / CI Windows UI ?

**Phase 4 non commencée.**
