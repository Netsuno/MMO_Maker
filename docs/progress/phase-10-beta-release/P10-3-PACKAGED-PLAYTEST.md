# P10-3 — Playtest depuis zip livré

Preuve de process / Hello depuis archives extraites **hors dépôt**. Ce n’est **pas** une partie WinForms 30–60 min.

Recette 2 PCs physiques : **Accepted / skipped by owner agreement — Netsun (2026-09-19); not re-run in this PR.** (voir [`guides/BETA_TEST_PLAN.md`](guides/BETA_TEST_PLAN.md)). Menu Playtest WinForms manuel : **Accepted / skipped by owner agreement — Netsun (2026-09-19); not re-run in this PR.** (`--smoke-launch` Windows CI reste une preuve automatisée distincte).

## Linux (cet agent / CI ubuntu)

WinForms / WPF **impossible** à lancer de façon prouvée : `Frog.Client.exe` / `Frog.Editor.exe` sont des apphost Windows. Wine n’est **jamais** un pass ([`packaged-winforms-layout-proof.sh`](../../../scripts/packaged-winforms-layout-proof.sh) `--skip-wine`).

**Prouvé (script)** : [`scripts/packaged-playtest-e2e.sh`](../../../scripts/packaged-playtest-e2e.sh)

1. Publie `server-linux-x64` (ou `--skip-publish` après `packaged-server-smoke`).
2. Copie le zip hors de l’arbre git, extrait.
3. `chmod +x Frog.Server` (zip Python perd souvent le bit exécutable).
4. Démarre `Frog.Server` du zip (`AllowInMemoryFallback` local au dossier extrait).
5. TCP Hello opcode 1.
6. Arrêt (fichier shutdown + SIGTERM).

**Prouvé (test)** : `Phase10PackagedPlaytestFromZipTests` — serveur publié hors dépôt + client headless, marqueur READY spawn exact.

Menu Playtest WinForms manuel (éditeur → spawn client) : **Accepted / skipped by owner agreement — Netsun (2026-09-19); not re-run in this PR.** Wine n’est jamais un pass.

CI : job `postgres-integration`, étapes Hello zip + READY headless — [35449733364](https://github.com/Netsuno/MMO_Maker/actions/runs/35449733364) SUCCESS sur `814b8ba` ; tip `a489379` [35450339601](https://github.com/Netsuno/MMO_Maker/actions/runs/35450339601) SUCCESS.

## Windows (CI windows-latest)

[`scripts/packaged-playtest-e2e.ps1`](../../../scripts/packaged-playtest-e2e.ps1)

1. Publie `client-win-x64` + `editor-win-x64` + `server-win-x64`.
2. Extrait hors dépôt, aplatit en layouts **frères** (`../client-win-x64`, `../server-win-x64`) — les mêmes candidats que `EditorFrogClientLauncher` / `EditorFrogServerLauncher`.
3. Démarre `Frog.Server.exe` du zip, TCP Hello.
4. `--smoke-launch` client/éditeur reste P10-6 (`packaged-winforms-smoke.ps1`) : shell visible puis quit, **pas** le menu Playtest.

CI : job `build-and-test`, étape `Packaged playtest from zip (sibling layouts + server Hello)`.

## Skip PC local (hors bloqueurs gate)

| Item | Pourquoi |
| --- | --- |
| Menu Playtest WinForms (éditeur → spawn client) | Exige GUI Windows + workspace PG ; CI prouve `--smoke-launch` seulement. **Accepted / skipped by owner agreement — Netsun (2026-09-19); not re-run in this PR.** |
| Playtest 2 machines / IP publique | **Accepted / skipped by owner agreement — Netsun (2026-09-19); not re-run in this PR.** (P10-4) |
| HUD 1366×768 / 1920×1080 | PC Windows local. **Accepted / skipped by owner agreement — Netsun (2026-09-19); not re-run in this PR.** |
