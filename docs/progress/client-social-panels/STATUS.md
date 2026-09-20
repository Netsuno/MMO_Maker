# STATUS — Client social panels MVP (Amis / Groupe / Guilde)

| Champ | Valeur |
| --- | --- |
| **Chantier** | Panneaux HUD Amis, Groupe, Guilde branchés sur le social Phase 10 déjà sur `main` |
| **Propriétaire** | Netsun |
| **Statut** | MVP overlay + dock chat — **pas de merge** |
| **Base** | `main` @ `bc60186` (merge PR #26 prefabs) |
| **Branche** | `cursor/client-social-panels-mvp-4176` |
| **PR** | Draft [#27](https://github.com/Netsuno/MMO_Maker/pull/27) vers `main` — **pas de merge** |
| **Tip** | `fcfb4b4e4a145df79531ed88dfed04c65358ca77` |
| **CI** | *(pin after green)* |
| **Protocole** | `FrogWireProtocol.Version` **reste 11** — opcodes **80–83** inchangés |

Chrome DA v2 (#19) + contraste Kenney (#16). Menu ring **reste 5 icônes** (step 4 figé). Ouverture : boutons Amis / Groupe / Guilde du dock chat, onglet overlay **Social**.

---

## Avant → après

| Surface | Avant | Après |
| --- | --- | --- |
| Snapshots 82 / events 83 / results 81 | `AppendLog` seulement | `ClientSocialRoster` + `SocialHubPanel` (listes + invitations) |
| Overlay `HudWindowChrome` | Chat / Inventaire / Quêtes | + onglet **Social** (sous-onglets Amis / Groupe / Guilde, tabs or) |
| Dock chat | 5 canaux | + boutons contraste **Amis / Groupe / Guilde** (`SocialPanelRequested`) |
| Listes vides | — | Textes vides explicites (pas de crash, pas de faux membres) |
| Actions | slash `/friend` `/party` `/guild` | Mêmes `SendSocialAsync` + extras `SocialWire` (Guid / UTF-8 / confirm) |

Rôles amis (déjà poussés par le serveur) : **1** accepté, **2** sortant, **3** entrant. Invitations groupe/guilde : `SocialEventType.InviteReceived` (`SubjectId` = party/guild id).

---

## Ce qui marche (MVP)

1. **État** — `Frog.Core/Social/ClientSocialRoster.cs` : snapshots par `SocialKind`, invitations en attente, présence, MOTD. Testable Linux sans WinForms.
2. **UI** — `SocialHubPanel` dans le chrome 360 px : listes, MOTD, boutons contraste `#0C1018` / filet `#C9A227` / texte `#F2F4F8`.
3. **Câblage** — `MainShellForm` : `OnSocialSnapshot` / `OnSocialEvent` / `OnSocialResult` → roster → panneau ; actions → `FrogGameClient.SendSocialAsync`.
4. **Ouverture** — dock chat (entrée sociale existante). Menu ring non étendu.

---

## Hors scope (volontaire)

- Auction house, mail, coffre de guilde.
- Nouveau opcode / bump `FrogWireProtocol.Version`.
- Liste blocage dédiée (snapshot `Block` déjà reçu, pas de 4ᵉ onglet).
- Résolution Guid depuis le nom monde (slash et champ Guid restent le contrat actuel).

---

## Tests

- Linux : `Frog.Tests/ClientSocialPanelsTests.cs` — empty states, snapshot amis, invite groupe + Accept=`party_id`, builders `SocialWire`, gates source shell/dock/chrome, STATUS.
- Windows smoke : `ClientHudOverlaySmokeTests.SocialPanels_OpenFromChatDock_AndApplySnapshot`.

Linux / cet agent : pas de capture WinForms HUD. Revue pixel = Windows 1280×720 DPI 125 %.
