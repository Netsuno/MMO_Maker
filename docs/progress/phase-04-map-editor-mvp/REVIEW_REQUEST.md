# Demande de revue — Phase 4 Map Editor MVP (gate data safety)

## Contexte

Gate Phase 4 rejeté temporairement pour corrections data safety. Cette itération adresse les 8 points du feedback sans commencer Phase 5.

## Critères gate

| Critère | État |
| --- | --- |
| Mode mémoire : pas de succès « PostgreSQL » trompeur | OK |
| Save/Publish désactivés ou clairement non persistants en démo | OK |
| `FROG_EDITOR_FORCE_IN_MEMORY=1` réservé aux tests | OK |
| Dirty + prompts complets | OK |
| Erreurs PG gérées + état occupé | OK |
| Concurrence atomique + test 2 DbContext | OK |
| Brouillon / publication séparés + migration | OK |
| Warps : limites carte cible + validation | OK |
| Tests édition + smoke chemin commande | OK |
| Phase 5 non commencée | OK |

## Preuves

- [`PHASE_REPORT.md`](PHASE_REPORT.md)
- [`TEST_RESULTS.md`](TEST_RESULTS.md)
- [`CHANGE_SUMMARY.md`](CHANGE_SUMMARY.md)
- [`KNOWN_ISSUES.md`](KNOWN_ISSUES.md)

## Question de gate

**Accepter Phase 4 (data safety)** et autoriser Phase 5 ?

**Phase 5 non commencée.**
