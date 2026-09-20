# STATUS — Maintenance mode + launcher / auto-update scaffolding

| Champ | Valeur |
| --- | --- |
| **Chantier** | Mode maintenance serveur + message login client + stub VERSION / compare |
| **Propriétaire** | Netsun |
| **Statut** | MVP scaffolding — **pas de merge** |
| **Base** | `main` @ `8a51f5c` (merge PR #29 movement fluidity) |
| **Branche** | `cursor/maintenance-launcher-mvp-44a6` |
| **PR** | Draft — voir GitHub (à lier après ouverture) |
| **Tip** | *(pin après CI du tip exact)* |
| **CI** | *(pin après SUCCESS `build-and-test` + `postgres-integration`)* |
| **Protocole** | `FrogWireProtocol.Version` **reste 11** — pas de bump, pas de nouvel opcode |

---

## Livré

1. **Drapeau serveur** — `Maintenance:Enabled` (`appsettings.json`) + env `FROG_MAINTENANCE=1` + fichier optionnel `FROG_MAINTENANCE_FILE` (même idée que `FROG_SHUTDOWN_FILE`). `MaintenanceService` (plus un stub TODO). Override in-process `SetEnabledOverride` pour tests / ops.
2. **Refus login** — `LoginResult` / `RegisterResult` / `ReconnectResult` forme courte existante, message `Serveur en maintenance. Reessayez plus tard.` (`Frog.Core.Distribution.MaintenanceMessages`). Sessions déjà connectées **non** coupées (pas de drain).
3. **Bypass opérateur** — si `Maintenance:AllowOperators=true` (défaut) et le compte est déjà dans `IOperatorDirectory` (grant hors TCP / OpsCli). Pas de nouveau packet admin.
4. **Client** — `PlayerFacingMessages.FromServerOrNetwork` mappe le signal « maintenance » vers un libellé clair (update / réessayer). Réutilise le status login existant. **Aucun** edit LoginShell / SHA / chrome DA v2.
5. **Launcher stub** — `ClientVersionManifest` + `docs/progress/maintenance-launcher/VERSION` + `scripts/check-client-version.sh` (compare deux fichiers VERSION). **Pas** d’installateur, pas d’HTTP réel.

Chemin ops : overlay `Maintenance:Enabled=true` **ou** `FROG_MAINTENANCE=1` **ou** écrire `1` dans le fichier nommé par `FROG_MAINTENANCE_FILE`. Grant GM via `Frog.OpsCli operator grant` pour le bypass.

---

## Hors scope (volontaire)

- Installateur, code-signing, CDN, téléchargement de builds.
- Drain / kick des sessions déjà en jeu, deadline d’arrêt.
- Bump `FrogWireProtocol.Version`, opcodes, trailer Hello.
- UI chrome, social, weather, audio, movement.

---

## Tests

- `Frog.Tests/MaintenanceModeTests.cs` — config / fichier / override, TCP reject login+register, bypass opérateur, mapping message client, VERSION + script, protocole 11, ce STATUS.
- `Frog.Tests/ClientVersionManifestTests.cs` — parse / compare (current, update, minClient, protocole).

CI : à pinner après le run du tip exact.
