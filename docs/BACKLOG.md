# Backlog actif — MMO Maker

Aligné sur `PRD_MMO_Maker_CSharp.md` v2.1 et ADR-0003.

## Hors backlog (différé / non bloquant)

- Import / export `.fcc` FRoG
- `Frog.LegacyImporter`
- Parité VB6 / golden masters FRoG
- Compatibilité protocole VB6
- Nouvelles fonctionnalités MariaDB

## Phase 2 — Clarification

- [x] ADR absence de compatibilité FRoG
- [x] Docs actives sans import `.fcc` critique
- [x] `Frog.Legacy` marqué expérimental
- [x] Matrice MariaDB + ADR WPF
- [x] PostgreSQL confirmé en CI

## Phase 3 — Shell éditeur

- [x] Wireframe / responsabilités (`docs/EDITOR_WORKSPACE.md`)
- [x] Shell : menu, toolbar, arbre monde, canvas, tilesets, couches, propriétés, status
- [x] Catalogue cartes via `IMapRepository` / `MapWorkspaceSession` (`MapId`)
- [x] Smoke test Windows automatisé (`Frog.Editor.WindowsSmokeTests` + CI)
- [x] Migration identité moderne (`ModernMapIdentity`)
- [x] Confirmation CI Windows verte (post-push)

## Phase 5 — Playtest

- [x] Editor-triggered playtest (server + client)
- [x] READY validation / token security / lifecycle
- [x] Accepted at `1944d73b6fffa84799d288da555f1005b82f2698`

## Phase 6 — Essential content editors

- [x] Tilesets
- [x] NPCs and monsters
- [x] Items
- [x] Spells and skills
- [x] Classes
- [x] Shops
- [x] Resources and spawns

## Phase 7 — Gameplay essentiel

- [x] Acceptée sur `main` (inventaire, équipement, boutique, banque, mêlée, mort/respawn)

## Phase 8 — Quêtes / événements

- [x] Acceptée sur `main` (merge `1cd57ba`)

## Phase 9 — Distribution / admin / hardening

- [x] Acceptée et fusionnée 2026-09-18 — PR #7 / `f74b34c` / tip produit `cab57b9`
- [x] Mute / kick / ban persistés, `SessionTeardown`, C2/C2b
- [x] `auth.operators`, gates bind / secrets / WorldFlags
- [x] Backup/restore **schéma** + login (lignes sanctions **non** certifiées)
- [x] Packaging serveur Linux prouvé ; client/éditeur publiés **non** lancés en preuve
- [x] Charge in-memory mesurée ; palier 25×60 **non** certifié
- [x] P9-S (guildes / groupes / trades) **différé** en Phase 9 → repris Phase 10

## Phase 10 — Bêta fermée externe (actif)

Mandat : [`progress/phase-10-beta-release/MANDATE.md`](progress/phase-10-beta-release/MANDATE.md).

- [x] P10-0 audit + plan + gel protocole
- [x] P10-1 groupes / guildes / amis / blocage (code + tests ; **pas** gate bêta)
- [ ] P10-2 échanges directs
- [ ] P10-3 client / éditeur utilisables hors dépôt
- [ ] P10-4 monde démo + recette 12 étapes
- [ ] P10-5 TLS, invitations, rate-limit IP+compte, outil opérateur
- [ ] P10-6 paquets self-contained + SHA-256
- [ ] P10-7 restore lignes métier + drain
- [ ] P10-8 25 joueurs × 60 min
- [ ] P10-9 candidate / gate (GO Marc)

Différé explicite (ne pas cocher ici) : coffre de guilde, HdV, mail objets, raids, UDP/AOI, clients non-Windows, import `.fcc`, boutique réelle.
