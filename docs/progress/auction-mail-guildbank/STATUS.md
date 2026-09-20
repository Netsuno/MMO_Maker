# STATUS — Auction / mail / guild bank MVP scaffolding

| Champ | Valeur |
| --- | --- |
| **Chantier** | Hôtel des ventes, courrier, coffre de guilde — scaffolding MVP après maintenance |
| **Propriétaire** | Netsun |
| **Statut** | MVP in-memory + onglets Social — **pas de merge** |
| **Base** | `main` @ `91eaa12` (merge PR #31 maintenance) |
| **Branche** | `cursor/auction-mail-guildbank-mvp-61f1` |
| **PR** | Draft [#32](https://github.com/Netsuno/MMO_Maker/pull/32) vers `main` — **pas de merge** |
| **Tip** | `ec24f0462bf3bc0e4afc9b736cfe3ba686a35fe9` |
| **CI** | [35540509057](https://github.com/Netsuno/MMO_Maker/actions/runs/35540509057) **SUCCESS** (`build-and-test` + `postgres-integration`) |
| **Protocole** | `FrogWireProtocol.Version` **reste 11** — opcodes additifs **87–89** (`EconomyHubRequest` / `Result` / `Snapshot`). Gel social 80–86 inchangé. |

Chrome DA v2 + overlay Social existant. Menu ring **reste 5 icônes**. Dock chat **Amis / Groupe / Guilde** inchangé (smoke `#27`). Ouverture : onglets **Courrier / HdV / Coffre** sous Social.

---

## Livré

1. **Wire** — `Frog.Core/Protocol/EconomyHubWire.cs` : multiplex `kind` Auction=1 / Mail=2 / GuildBank=3, action MVP `Query=1`. Entrées unifiées (id, related, qty, flags/prix, titre).
2. **Serveur** — `EconomyHubService` in-memory. Query HdV / courrier → listes vides. Query coffre : pas de guilde → 0 slots ; membre (via `ISocialStore` Phase 10) → 8 emplacements vides. Pas de migration PostgreSQL.
3. **Client** — `ClientEconomyHub` (Linux sans WinForms) + `EconomyHubSurface` dans `SocialHubPanel` (tabs or, bouton Actualiser contraste). `FrogGameClient.SendEconomyHubAsync` / snapshots 89.
4. **Persistance** — **in-memory stubs**. Pas de tables `player.auction_*` / `mail_*` / `guild_bank_*`.

---

## Hors scope (volontaire)

- Acheter / enchérir / poster une vente, envoyer un courrier, déposer / retirer du coffre.
- Migration PostgreSQL, journal `economy_request_ids`, pièces jointes inventaire.
- Bump `FrogWireProtocol.Version`, trailer Hello, opcodes 80–86.
- Weather, movement, audio, maintenance, login shell, panes Phase 8 exact-sha (`DialoguePanel` / `QuestJournalPanel` / `EnvironmentPanel`).
- Nouveau chrome overlay / 6ᵉ icône menu ring / boutons dock extra (garde le smoke Social à 3 boutons).

---

## TODO (prochaine passe)

- Persistence PostgreSQL : listings HdV, messages courrier + pièces jointes, slots coffre + permissions chef/officier.
- Actions mutantes (list / bid / buy, send / claim, deposit / withdraw) + idempotence `request_id`.
- Smoke Windows dédié `OpenEconomyPanelForTest` (Linux this run = gates source seulement).

---

## Tests

- `Frog.Tests/EconomyHubWireTests.cs` — round-trip 87–89, protocole 11, hints vides, câblage shell/hub/client, ce STATUS.
- `Frog.Tests/EconomyHubLogicTests.cs` — Query sans perso / action inconnue / listes vides / coffre 8 slots si guilde.
- `Frog.Tests/EconomyHubTcpTests.cs` — TCP in-memory Query + create guild + coffre.

Linux this run: `dotnet test Frog.Tests` **697 passed** (filtre EconomyHub **11 passed**).

CI **green** on `ec24f04` : [build-and-test](https://github.com/Netsuno/MMO_Maker/actions/runs/35540509057/job/106157221884) + [postgres-integration](https://github.com/Netsuno/MMO_Maker/actions/runs/35540509057/job/106157221791). Windows editor / gameplay / Phase 8 smokes included.

Linux / cet agent : pas de capture WinForms HUD. Revue pixel = Windows 1280×720 DPI 125 %.
