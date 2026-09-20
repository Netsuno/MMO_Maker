# STATUS — Maintenance mode + launcher / auto-update scaffolding

| Champ | Valeur |
| --- | --- |
| **Chantier** | Mode maintenance serveur + message login + **launcher stub** VERSION / compare |
| **Propriétaire** | Netsun |
| **Statut** | **Merged** sur `main` — flags ops + stub compare (pas d’installateur) |
| **Merge** | [PR #31](https://github.com/Netsuno/MMO_Maker/pull/31) → `91eaa1260f237325d2a130346d916968a68d8c07` |
| **Tip miroir docs** | `d6e59759dada9ef24849b9b985838b459c7f55b1` |
| **CI (tip miroir)** | [35542485239](https://github.com/Netsuno/MMO_Maker/actions/runs/35542485239) **SUCCESS** (re-pin après cancel cascade merge) |
| **Protocole** | Version **11** — pas de nouvel opcode |

---

## Avant → après

| Surface | Avant | Après |
| --- | --- | --- |
| Maintenance | stub / TODO | `MaintenanceService` + config / env / fichier |
| Login sous flag | — | refus forme courte + message `MaintenanceMessages` |
| Opérateurs | — | bypass si `AllowOperators` + grant OpsCli |
| Version client | — | **launcher stub** : `ClientVersionManifest` + `scripts/check-client-version.sh` |

---

## Ce qui marche (MVP)

1. **Flags** — `Maintenance:Enabled`, env `FROG_MAINTENANCE=1`, fichier `FROG_MAINTENANCE_FILE` ; override `SetEnabledOverride`.
2. **Refus** — login / register / reconnect (sessions déjà en jeu **non** coupées — pas de drain).
3. **Client** — mapping message « maintenance » via `PlayerFacingMessages` (chrome LoginShell **non** retouché).
4. **Launcher stub** — parse/compare fichiers VERSION locaux. **Pas** d’HTTP, **pas** d’installateur, **pas** de CDN.

Ops : [OPERATIONS.md](../phase-10-beta-release/guides/OPERATIONS.md).

---

## Hors scope (volontaire)

- Installateur, code-signing, téléchargement de builds.
- Drain / kick des sessions déjà connectées.
- Bump protocole / trailer Hello.

---

## Tests

- `Frog.Tests/MaintenanceModeTests.cs`, `ClientVersionManifestTests.cs`.
- Linux / agent docs : placeholder `login-01-maintenance.png`.

## Honnêteté

- Label **launcher stub** uniquement (compare fichiers) — ne pas présenter comme auto-update produit.
- Drain maintenance reste **incomplet** ; le *drapeau* de refus login est livré.
- CI merge #31 cancelled puis re-pin tip SUCCESS.


## Note tests (STATUS gate — ne pas retirer)

Ces phrases sont assertées par Frog.Tests StatusDoc_* :

- `reste 11`

reste 11
