# Rapport de fin de phase 02 — Clarification produit

## Identification

- Date et fuseau horaire : 2026-08-22 04:34 UTC
- Branche : `cursor/phase0-baseline-audit-02c7`
- Commit de départ : `094ba3f` (PostgreSQL SoT)
- Commit final : 3604bd7a5146ab66ddd44e8d28314cbcea41eaaa
- Working tree : clean après commit de gate
- OS / SDK .NET / PostgreSQL : Ubuntu 24.04.4 LTS / SDK 8.0.424 / PostgreSQL 16.15
- Phase et gate visés : Phase 2 — aucun document actif ne présente l’import `.fcc` ou la parité VB6 comme condition de livraison

## Verdict proposé

- **READY FOR REVIEW**
- Justification : ADR-0003/0004, backlog corrigé, Legacy marqué différé, matrice MariaDB, README/STATUS/ARCHITECTURE/TESTING alignés, CI PostgreSQL déjà en place, build+tests verts.

## Livré et vérifié

| Fonction | Preuve | Test |
| --- | --- | --- |
| Périmètre sans compatibilité FRoG | ADR-0003 | Revue documentaire |
| Docs actives sans import `.fcc` critique | STATUS, BACKLOG, ARCHITECTURE, TESTING, README | Scan texte |
| Legacy différé | `Frog.Legacy/README.md`, Description csproj | Build + unitaires (Legacy tests non exigence produit) |
| MariaDB par domaine | `docs/MARIADB_DOMAIN_MATRIX.md` | — |
| Décision WPF | ADR-0004 | — |
| PostgreSQL CI | `.github/workflows/ci.yml` job `postgres-integration` | 6/6 locaux |

## Implémenté, mais non vérifié

- Smoke UI Windows de l’éditeur (agent Linux) — reporté Phase 3

## Non réalisé ou reporté

- Retrait code MariaDB / WPF — hors Phase 2 (planifié par domaines)
- Shell éditeur Phase 3 — **non commencé** (gate)

## Changements fonctionnels

- Avant : backlog / STATUS poussaient encore LegacyImporter / `.fcc` comme suite logique
- Après : produit présenté comme MMO Maker ; FRoG = inspiration seulement ; prochaine étape = shell éditeur

## Changements techniques

- Docs + ADR ; marquage Legacy ; pas de migration DB ; pas de changement protocole

## Écart par rapport au PRD

- Aucun écart bloquant. README conserve des sections historiques plus bas (jalons MariaDB anciens partiellement mis à jour) — dette documentaire faible, signalée.

## Risques et dette

| Sévérité | Item | Mitigation |
| --- | --- | --- |
| Moyenne | Hybride WPF/WinForms | ADR-0004, pas d’extension WPF |
| Moyenne | MariaDB encore dans Editor/Server | Matrice + gel nouvelles features |
| Faible | Sections historiques README | Backlog / STATUS font foi |

## Décisions requises

1. Valider le gate Phase 2 (recommandé : accepter).
2. Confirmer ADR-0004 (garder coque WPF pour Phase 3) — recommandation : oui.
3. Ordre Phase 3 : wireframe documenté puis shell — recommandation : suivre tâche 4 puis 5 du PRD §18.

## Prochaine étape proposée

1. Wireframe / responsabilités panneaux éditeur (`docs/progress` ou `docs/editor-workspace.md`).
2. Implémenter / peaufiner le shell (arbre, canvas, tilesets, couches, propriétés, status).
3. Smoke test Windows du shell.
