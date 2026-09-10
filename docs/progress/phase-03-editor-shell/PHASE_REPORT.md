# Rapport de fin de phase 03 — Shell éditeur (corrections gate)

## Identification

- Date : 2026-08-22
- Branche : `cursor/phase0-baseline-audit-02c7`
- Commit d’implémentation Phase 3 (immuable) : `3fc6530`
- Plage revue : `3fc6530..HEAD` (corrections smoke Windows + MapId)
- OS local : Ubuntu 24.04 / SDK 8.0.424 / PostgreSQL 16.15

## Verdict proposé

- **READY FOR REVIEW** — corrections gate appliquées ; smoke Windows **PASS en CI**.

## Corrections gate

| Exigence | Livrable |
| --- | --- |
| Smoke UI Windows | `Frog.Editor.WindowsSmokeTests` + job CI Windows |
| Repository mémoire | `FROG_EDITOR_FORCE_IN_MEMORY=1` / `EditorTestHooks` |
| Identité MapId | Migration `ModernMapIdentity`, ports Application, docs |
| Pas de LegacyId actif | Retrait `legacy_id`, `LoadByLegacyIdAsync`, etc. |

## Livré et vérifié

| Preuve | Résultat |
| --- | --- |
| Build Release | OK |
| Tests unitaires | 110 / 110 |
| Tests PostgreSQL | 7 / 7 |
| Smoke Windows CI | PASS — run 32575250906 |

## Décision requise

Accepter Phase 3 et autoriser Phase 4, ou demander des précisions sur le smoke Windows.

**Phase 4 non commencée.**
