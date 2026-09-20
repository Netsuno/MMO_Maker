# STATUS — Dungeons / instances / raids MVP scaffolding

| Champ | Valeur |
| --- | --- |
| **Chantier** | Donjons, instances, raids, génération procédurale stub — scaffolding MVP après auction / mail / guild bank |
| **Propriétaire** | Netsun |
| **Statut** | MVP in-memory + onglet Social Instance — **pas de merge** |
| **Base** | `main` @ `c428c26` (merge PR #32 auction / mail / guild bank) |
| **Branche** | `cursor/dungeons-instances-mvp-a9bc` |
| **PR** | Draft [#33](https://github.com/Netsuno/MMO_Maker/pull/33) vers `main` — **pas de merge** |
| **Tip** | `7f87d507b834237f9231130fe4533d53c0873643` |
| **CI** | [35541768014](https://github.com/Netsuno/MMO_Maker/actions/runs/35541768014) **SUCCESS** (`build-and-test` + `postgres-integration`) |
| **Protocole** | `FrogWireProtocol.Version` **reste 11** — opcodes additifs **90–92** (`InstanceHubRequest` / `Result` / `Snapshot`). Gel social 80–86 et économie 87–89 inchangés. |

Chrome DA v2 + overlay Social existant. Menu ring **reste 5 icônes**. Dock chat **Amis / Groupe / Guilde** inchangé. Ouverture : onglet **Instance** sous Social (Entrer / Quitter / Actualiser).

---

## Livré

1. **Modèles** — `DungeonDefinition`, `InstanceId`, catalogue fixe (`Ruines du Marais` donjon / `Crypte du Roi` raid). Hooks enter/leave sur `Session` (sauvegarde overworld + `CurrentMapId` template).
2. **Wire** — `Frog.Core/Protocol/InstanceHubWire.cs` : multiplex `kind` Dungeon=1 / Raid=2, actions `Query=1` / `Enter=2` / `Leave=3`. Entrées unifiées (définition, run, occupants, seed, titre).
3. **Serveur** — `InstanceHubService` in-memory. Create/destroy run, gate groupe (membre requis ; raid min 2 ; chef crée, membres rejoignent), leave → overworld. `PartyRoster` partagé avec le social Phase 10. Pas de migration PostgreSQL.
4. **Procgen** — stub seedé `ProceduralDungeonGenerator` (2–4 salles template). Pas un moteur procédural.
5. **Client** — `ClientInstanceHub` (Linux sans WinForms) + `InstanceHubSurface` dans `SocialHubPanel` (tab or, boutons contraste). `FrogGameClient.SendInstanceHubAsync` / snapshots 92.
6. **Persistance** — **in-memory stubs**. Pas de tables `map_instance` / journal de runs.

---

## Hors scope (volontaire)

- Moteur procgen (salles/couloirs/loot/rencontres), cartes instance dédiées publiées, sharding.
- Persistence PostgreSQL `map_instance`, reprise après restart, idempotence `request_id`.
- Bump `FrogWireProtocol.Version`, trailer Hello, opcodes 80–89.
- Weather, movement (hors hook session CurrentMapId), audio, maintenance, auction/mail/coffre, login shell, panes Phase 8 exact-sha (`DialoguePanel` / `QuestJournalPanel` / `EnvironmentPanel`).
- Nouveau chrome overlay / 6ᵉ icône menu ring / boutons dock extra (garde le smoke Social à 3 boutons).

---

## TODO (prochaine passe)

- Persistence PostgreSQL : `map_instance` / run ledger + seed + occupants.
- Cartes template dédiées + `TryTeleportToTile` réel (blobs publiés) au lieu du hook session.
- Loot / rencontres / lockout / reset + idempotence `request_id`.
- Smoke Windows dédié `OpenInstancePanelForTest` (Linux this run = gates source seulement).

---

## Tests

- `Frog.Tests/InstanceHubWireTests.cs` — round-trip 90–92, protocole 11, hints, procgen déterministe, câblage shell/hub/client, ce STATUS.
- `Frog.Tests/InstanceHubLogicTests.cs` — Query sans perso / action inconnue / catalogue / gate groupe / enter-leave overworld / join membre / raid min 2.
- `Frog.Tests/InstanceHubTcpTests.cs` — TCP in-memory Query + gate + create/join/leave.

Linux this run: `dotnet test Frog.Tests` **713 passed** (filtre InstanceHub **16 passed**).

CI **green** on `7f87d50` : [build-and-test](https://github.com/Netsuno/MMO_Maker/actions/runs/35541768014/job/106160604472) + [postgres-integration](https://github.com/Netsuno/MMO_Maker/actions/runs/35541768014/job/106160604334). Windows editor / gameplay / Phase 8 smokes included.

Linux / cet agent : pas de capture WinForms HUD. Revue pixel = Windows 1280×720 DPI 125 %.
