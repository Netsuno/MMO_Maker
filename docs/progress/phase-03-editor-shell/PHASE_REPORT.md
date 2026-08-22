# Rapport de fin de phase 03 — Shell éditeur (corrections gate)

## Identification

- Date : 2026-08-22
- Branche : `cursor/phase0-baseline-audit-02c7`
- Commit d’implémentation Phase 3 (immuable) : `3fc6530`
- Plage revue : `3fc6530..HEAD` (corrections smoke Windows + MapId)
- OS local : Ubuntu 24.04 / SDK 8.0.424 / PostgreSQL 16.15

## Verdict proposé

- **READY FOR REVIEW** — corrections gate appliquées ; smoke Windows **automatisé en CI** (non exécuté sur agent Linux).

## Corrections gate

| Exigence | Livrable |
| --- | --- |
| Smoke UI Windows | `Frog.Editor.WindowsSmokeTests` + job CI Windows |
| Repository mémoire | `FROG_EDITOR_FORCE_IN_MEMORY=1` / `EditorTestHooks` |
| Identité MapId | Migration `ModernMapIdentity`, ports Application, docs |
| Pas de LegacyId actif | Retrait `legacy_id`, `LoadByLegacyIdAsync`, etc. |

## Livré et vérifié (Linux)

- Build Release vert
- 110 tests unitaires
- 7 tests PostgreSQL (migration forward appliquée)

## Non vérifié localement

- Smoke Windows (CI uniquement — voir `TEST_RESULTS.md` pour lien run)

## Décision requise

Accepter Phase 3 et autoriser Phase 4, ou exiger confirmation du run CI Windows vert.
