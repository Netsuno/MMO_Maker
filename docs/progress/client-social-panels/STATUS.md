# STATUS — Client social panels MVP (Amis / Groupe / Guilde)

| Champ | Valeur |
| --- | --- |
| **Chantier** | Panneaux HUD Amis, Groupe, Guilde branchés sur le social Phase 10 déjà sur `main` |
| **Propriétaire** | Netsun |
| **Statut** | **Merged** sur `main` — MVP overlay + dock chat |
| **Merge** | [PR #27](https://github.com/Netsuno/MMO_Maker/pull/27) → `399ece9` |
| **Tip miroir docs** | `d6e59759dada9ef24849b9b985838b459c7f55b1` (main tip courant) |
| **Tip feature** | `9b836559f13385b30242299c81894fd447d6f9f0` (fix open sans PerformClick) |
| **CI (feature tip)** | [35533368787](https://github.com/Netsuno/MMO_Maker/actions/runs/35533368787) **SUCCESS** |
| **Protocole** | `FrogWireProtocol.Version` **11** — opcodes **80–83** inchangés |

Chrome DA v2 (#19) + contraste Kenney (#16). Menu ring **reste 5 icônes**. Ouverture : boutons **Amis / Groupe / Guilde** du dock chat, onglet overlay **Social**.

---

## Avant → après

| Surface | Avant | Après |
| --- | --- | --- |
| Snapshots 82 / events 83 / results 81 | log seulement | `ClientSocialRoster` + `SocialHubPanel` (listes + invitations) |
| Overlay chrome | Chat / Inventaire / Quêtes | + onglet **Social** (sous-onglets Amis / Groupe / Guilde) |
| Dock chat | 5 canaux | + boutons contraste **Amis / Groupe / Guilde** |
| Listes vides | — | Textes vides explicites (pas de crash, pas de faux membres) |
| Actions | slash `/friend` `/party` `/guild` | Mêmes `SendSocialAsync` + extras wire (Guid / UTF-8 / confirm) |

---

## Ce qui marche (MVP)

1. **État** — `Frog.Core/Social/ClientSocialRoster.cs` : snapshots, invitations, présence, MOTD (testable hors WinForms).
2. **UI** — `SocialHubPanel` : listes, MOTD, boutons contraste.
3. **Câblage** — `MainShellForm` : snapshot/event/result → roster → panneau ; actions → `SendSocialAsync`.
4. **Ouverture** — dock chat. Menu ring non étendu.

Guide UI : [UI-CLIENT-SocialHub.md](../phase-10-beta-release/guides/UI-CLIENT-SocialHub.md).

---

## Hors scope (volontaire pour #27)

- Auction house, mail, coffre de guilde, instances — onglets éventuellement présents sur tip post-#32/#33 : **échafaudage**, pas documentés comme gameplay complet dans ce lot.
- Nouveau opcode / bump version pour social.
- Liste blocage dédiée (snapshot `Block` reçu, pas de 4ᵉ onglet social).
- Résolution Guid depuis le nom monde (slash et champ Guid restent le contrat).

---

## Tests

- Linux : `Frog.Tests` social panels — empty states, snapshot amis, invite groupe, builders wire, gates dock/chrome.
- Windows smoke : open from chat dock + apply snapshot.

Linux / agent docs : pas de capture WinForms HUD. Placeholders HTML + *Capture à venir* dans le guide.


## Note tests (STATUS gate)

Historique MVP : **pas de merge** tant que CI rouge ; chrome **HudWindowChrome** (#19).
