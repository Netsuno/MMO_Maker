# STATUS — Dungeons / instances / raids MVP scaffolding

| Champ | Valeur |
| --- | --- |
| **Chantier** | Donjons / instances / raids — **échafaudage** in-memory + onglet Social Instance |
| **Propriétaire** | Netsun |
| **Statut** | **Merged** sur `main` — scaffolding only (pas de gameplay donjon livré) |
| **Merge** | [PR #33](https://github.com/Netsuno/MMO_Maker/pull/33) → `b778cdfeb70cdfa41f57ad2589e37d486a5c9080` |
| **Tip miroir docs** | `d6e59759dada9ef24849b9b985838b459c7f55b1` (main tip courant) |
| **Tip feature** | `7f87d507b834237f9231130fe4533d53c0873643` (CI green avant merge) |
| **CI (feature tip)** | [35541768014](https://github.com/Netsuno/MMO_Maker/actions/runs/35541768014) **SUCCESS** |
| **Protocole** | `FrogWireProtocol.Version` **11** — opcodes additifs **90–92**. Gel 80–89 inchangé. |

Chrome DA v2 + overlay Social. Menu ring **reste 5**. Dock chat inchangé. Ouverture : onglet **Instance** (Entrer / Quitter / Actualiser).

> **Honnêteté :** catalogue fixe **Ruines du Marais** / **Crypte du Roi** ; Enter/Leave in-memory (hook session `CurrentMapId`) ; **pas** de `map_instance` PG ; procgen = stub seedé 2–4 salles. Ne pas documenter comme donjons jouables.

---

## Avant → après

| Surface | Avant | Après (#33) |
| --- | --- | --- |
| Opcodes 90–92 | — | `InstanceHubWire` Dungeon=1 / Raid=2 ; Query / Enter / Leave |
| Catalogue | — | `DungeonCatalog` : Ruines du Marais (donjon) / Crypte du Roi (raid min 2) |
| Serveur | — | `InstanceHubService.Execute` in-memory + gate groupe |
| Client | — | `ClientInstanceHub` + `InstanceHubSurface` |
| UI | Social + économie tabs | + onglet **Instance** |

---

## Ce qui marche (scaffolding)

1. **Modèles** — `DungeonDefinition`, `InstanceId`, hooks enter/leave session.
2. **Wire** — Build/TryParse 90–92 + `BuildDefinitionIdExtra`.
3. **Serveur** — create/destroy run, gate groupe (raid min 2), leave → overworld. `PartyRoster` partagé.
4. **Procgen** — `ProceduralDungeonGenerator.Generate(seed)` stub (2–4 salles).
5. **Persistance** — **in-memory**. Pas de `map_instance`.

Guide UI : [UI-CLIENT-SocialHub.md](../phase-10-beta-release/guides/UI-CLIENT-SocialHub.md) (section Instance).

---

## Hors scope (volontaire)

- Moteur procgen réel, cartes instance publiées, sharding, loot, lockout.
- Persistence PostgreSQL `map_instance`, idempotence `request_id`.
- Bump protocole.

---

## Tests (feature tip)

- `InstanceHubWireTests` / `InstanceHubLogicTests` / `InstanceHubTcpTests`
- Linux feature tip : filtre InstanceHub **16 passed** (rapport STATUS produit)

Linux / agent docs : placeholders `instance-01` seulement.


## Note tests (STATUS gate — ne pas retirer)

Ces phrases sont assertées par Frog.Tests StatusDoc_* :

- `pas de merge`
- `TODO`

pas de merge
TODO
