# STATUS — Auction / mail / guild bank MVP scaffolding

| Champ | Valeur |
| --- | --- |
| **Chantier** | Hôtel des ventes, courrier, coffre de guilde — **échafaudage** Query-only |
| **Propriétaire** | Netsun |
| **Statut** | **Merged** sur `main` — scaffolding only (pas d’économie joueur livrée) |
| **Merge** | [PR #32](https://github.com/Netsuno/MMO_Maker/pull/32) → `c428c26673a8567f766c26e9afba2e7fd028417e` |
| **Tip miroir docs** | `d6e59759dada9ef24849b9b985838b459c7f55b1` (main tip courant) |
| **Tip feature** | `ec24f0462bf3bc0e4afc9b736cfe3ba686a35fe9` (CI green avant merge) |
| **CI (feature tip)** | [35540509057](https://github.com/Netsuno/MMO_Maker/actions/runs/35540509057) **SUCCESS** |
| **Protocole** | `FrogWireProtocol.Version` **11** — opcodes additifs **87–89** (`EconomyHubRequest` / `Result` / `Snapshot`). Gel social 80–86 inchangé. |

Chrome DA v2 + overlay Social. Menu ring **reste 5 icônes**. Dock chat **Amis / Groupe / Guilde** inchangé. Ouverture : sous-onglets **Courrier / HdV / Coffre** dans `SocialHubPanel`.

> **Honnêteté :** pas d’achat / enchère / envoi de courrier / dépôt coffre. Listes **vides** (coffre : 8 slots vides si guilde). **Pas de PostgreSQL.** Ne pas documenter comme gameplay économie livré.

---

## Avant → après

| Surface | Avant | Après (#32) |
| --- | --- | --- |
| Opcodes 87–89 | — | `EconomyHubWire` multiplex Auction=1 / Mail=2 / GuildBank=3, action **Query=1** seulement |
| Serveur | — | `EconomyHubService.ExecuteAsync` in-memory → snapshots vides |
| Client | — | `ClientEconomyHub` + `EconomyHubSurface` (Actualiser) |
| UI | Amis / Groupe / Guilde | + onglets Courrier / HdV / Coffre (états vides explicites) |

---

## Ce qui marche (scaffolding)

1. **Wire** — `Frog.Core/Protocol/EconomyHubWire.cs` : Build/TryParse Request/Result/Snapshot.
2. **Serveur** — Query HdV / courrier → listes vides ; Query coffre : pas de guilde → 0 slots ; membre → 8 emplacements vides.
3. **Client** — `SendEconomyHubAsync` → opcode 87 ; snapshots 89 → `ClientEconomyHub.ApplySnapshot`.
4. **Persistance** — **in-memory stubs**. Pas de tables `auction_*` / `mail_*` / `guild_bank_*`.

Guide UI : [UI-CLIENT-SocialHub.md](../phase-10-beta-release/guides/UI-CLIENT-SocialHub.md) (section échafaudage économie).

---

## Hors scope (volontaire)

- Acheter / enchérir / poster, envoyer un courrier, déposer / retirer du coffre.
- Migration PostgreSQL, journal `economy_request_ids`, pièces jointes inventaire.
- Bump protocole, trailer Hello, opcodes 80–86.
- Nouveau chrome / 6ᵉ icône menu ring.

---

## Tests (feature tip)

- `EconomyHubWireTests` / `EconomyHubLogicTests` / `EconomyHubTcpTests`
- Linux feature tip : filtre EconomyHub **11 passed** (rapport STATUS produit)

Linux / agent docs : pas de capture WinForms. Placeholders `economy-01..03` seulement.
