# Change summary — Phase 02

## Commits de la phase

- `3604bd7` docs(product): Phase 2 — MMO Maker scope, no FRoG compatibility
- `73e5dcb` docs(progress): fill Phase 2 gate commit hashes

## Diff (départ `094ba3f` → final)

Ajouts principaux :

- `docs/decisions/ADR-0003-frog-inspiration-no-compatibility.md`
- `docs/decisions/ADR-0004-editor-wpf-shell-temporary.md`
- `docs/MARIADB_DOMAIN_MATRIX.md`
- `docs/BACKLOG.md`
- `Frog.Legacy/README.md`
- `docs/progress/phase-02-clarification/*`

Modifiés :

- `README.md` — repositionnement MMO Maker / PostgreSQL / pas de compat FRoG
- `docs/STATUS.md`, `ARCHITECTURE.md`, `TESTING.md`, `LEGACY_FORMATS.md` (bannière différé)
- `Frog.Legacy/Frog.Legacy.csproj` — Description experimental/deferred

## Projets / packages / migrations

- Aucun projet ajouté ou retiré.
- Aucun package NuGet ajouté.
- Aucune migration PostgreSQL.
- Aucun message réseau modifié.
- Aucun écran UI ajouté (docs seulement).

## Compatibilité retirée du chemin critique

- Import `.fcc` / LegacyImporter / parité VB6 retirés du backlog actif et de STATUS.

## Retour arrière

- Revenir au commit pré-phase (`094ba3f`) restaure l’ancienne formulation documentaire ; aucun schéma DB à reverting.
