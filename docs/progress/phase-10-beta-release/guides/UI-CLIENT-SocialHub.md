# Frog.Client — UI Social (dock + overlay Amis / Groupe / Guilde)

← [Guides](README.md) · [MainShell](UI-CLIENT-MainShell.md) · [Référence Client](../../../reference/client/README.md) · [ClientSocialRoster](../../../reference/core/README.md)

Tip miroir : `d6e59759`. Feature #27 merge `399ece9` (tip feature `9b83655`). Propriétaire : **Netsun**.

Panneau HUD social branché sur opcodes **80–83** (`SendSocialAsync` → `SocialRequest`). Slash `/friend` `/party` `/guild` restent disponibles ; ce guide documente **chaque bouton / panneau** visible.

> Onglets **Courrier / HdV / Coffre / Instance** peuvent apparaître dans le même `SocialHubPanel` (échafaudage tip post-#32/#33). **Hors périmètre gameplay de ce lot** — ne pas les traiter comme features joueur livrées ici.

---

## Ouverture

| Contrôle | Libellé UI | Où | Fait quoi | API |
| --- | --- | --- | --- | --- |
| Bouton dock | **Amis** | `HudChatDock` (barre canaux) | Ouvre overlay Social, onglet Amis | `SocialPanelRequested(Friend)` → `OpenSocialPanel` |
| Bouton dock | **Groupe** | `HudChatDock` | Ouvre overlay Social, onglet Groupe | idem `Party` |
| Bouton dock | **Guilde** | `HudChatDock` | Ouvre overlay Social, onglet Guilde | idem `Guild` |
| Onglet chrome | **Social** | `MainShellForm` / `HudWindowChrome` | Contient `SocialHubPanel` | — |

Menu ring : **pas** d’icône Social (figé à 5). Aide **F1** rappelle les boutons dock.

<!-- CAPTURE: assets/social-01-dock-amis-groupe-guilde.png -->
*Capture à venir : dock chat — canaux Général/Local/Whisper/Groupe/Guilde + boutons contraste Amis / Groupe / Guilde.*

<!-- CAPTURE: assets/social-02-overlay-onglet-social.png -->
*Capture à venir : overlay chrome — onglet Social sélectionné, sous-onglets Amis / Groupe / Guilde (tabs or).*

---

## Sous-onglets SocialHubPanel

| Onglet | Contenu | Statut doc ce lot |
| --- | --- | --- |
| **Amis** | Liste + invitations + actions ami | Documenté |
| **Groupe** | Membres / invites + actions party | Documenté |
| **Guilde** | Membres / MOTD / invites + actions guild | Documenté |
| **Courrier** / **HdV** / **Coffre** / **Instance** | Surfaces échafaudage | Mention seulement |

Champ commun (bas de chaque surface Amis/Groupe/Guilde) :

| Contrôle | Libellé / placeholder | Rôle |
| --- | --- | --- |
| `_input` | `Guid personnage / nom guilde / MOTD` | Cible Guid (ami/invite) ou texte (créer guilde / MOTD) |
| `_list` | (liste) | Membres + lignes invitation |
| `_empty` | hint `EmptyHint(kind)` | Affiché si liste vide |
| `_motd` | titre kind ou `MOTD : …` | MOTD guilde (ou party si présent) |
| `_status` | `StatusLine` roster | Dernier résultat / snapshot / event |

---

## Boutons — onglet Amis

| Bouton | Action | API |
| --- | --- | --- |
| **Ajouter** | Demande d’ami vers Guid saisi (activé si Guid valide) | → [invite](../../../reference/core/clientsocialroster-invite.md) → [sendsocialasync](../../../reference/client/README.md#sendsocialasync) |
| **Accepter** | Accepte demande entrante / invite sélectionnée | → [accept](../../../reference/core/clientsocialroster-accept.md) |
| **Refuser** | Refuse demande / invite sélectionnée | → [decline](../../../reference/core/clientsocialroster-decline.md) |
| **Retirer** | Retire un ami accepté (ou demande sortante) | → [friendremove](../../../reference/core/clientsocialroster-friendremove.md) |

Rôles affichés : ami / demande envoyée / demande reçue (`FriendRoleIncoming`) + présence.

<!-- CAPTURE: assets/social-03-amis-liste.png -->
*Capture à venir : onglet Amis — liste (ou texte vide), champ Guid, boutons Ajouter / Accepter / Refuser / Retirer.*

---

## Boutons — onglet Groupe

| Bouton | Action | API |
| --- | --- | --- |
| **Inviter** | Invite Guid saisi | → [invite](../../../reference/core/clientsocialroster-invite.md) |
| **Accepter** | Accepte invitation (payload = `party_id`) | → [accept](../../../reference/core/clientsocialroster-accept.md) |
| **Refuser** | Refuse invitation | → [decline](../../../reference/core/clientsocialroster-decline.md) |
| **Quitter** | Quitte le groupe (si `HasParty`) | → [leave](../../../reference/core/clientsocialroster-leave.md) |
| **Expulser** | Kick membre sélectionné (chef) | → [kick](../../../reference/core/clientsocialroster-kick.md) |
| **Chef** | Transfert leadership | → [transferleader](../../../reference/core/clientsocialroster-transferleader.md) |
| **Dissoudre** | Dissout le groupe (confirm wire) | → [disband](../../../reference/core/clientsocialroster-disband.md) |

Un redémarrage serveur **dissout les groupes** (éphémères).

<!-- CAPTURE: assets/social-04-groupe.png -->
*Capture à venir : onglet Groupe — membres ou hint vide, boutons Inviter … Dissoudre.*

---

## Boutons — onglet Guilde

| Bouton | Action | API |
| --- | --- | --- |
| **Créer** | Crée une guilde (nom dans le champ ; pas déjà en guilde) | → [tryguildcreate](../../../reference/core/clientsocialroster-tryguildcreate.md) |
| **Inviter** | Invite Guid (si déjà en guilde) | → [invite](../../../reference/core/clientsocialroster-invite.md) |
| **Accepter** / **Refuser** | Invitation guilde | → accept / decline |
| **Quitter** | Quitte la guilde | → [leave](../../../reference/core/clientsocialroster-leave.md) |
| **Expulser** | Kick membre | → [kick](../../../reference/core/clientsocialroster-kick.md) |
| **Chef** | Transfert chef | → [transferleader](../../../reference/core/clientsocialroster-transferleader.md) |
| **Dissoudre** | Dissout la guilde | → [disband](../../../reference/core/clientsocialroster-disband.md) |
| **MOTD** | Définit le message du jour (texte champ) | → [tryguildsetmotd](../../../reference/core/clientsocialroster-tryguildsetmotd.md) |

Guildes / amis / blocages **survivent** au redémarrage (contrairement aux groupes).

<!-- CAPTURE: assets/social-05-guilde-motd.png -->
*Capture à venir : onglet Guilde — MOTD en tête, liste, champ nom/MOTD, boutons Créer … MOTD.*

---

## Flux données (résumé)

1. Serveur → `SocialSnapshot` (82) / `SocialEvent` (83) / `SocialResult` (81).
2. `MainShellForm` → `ClientSocialRoster.ApplySnapshot` / `ApplyEvent` / `ApplyResult`.
3. `SocialHubPanel.ApplyRoster` → listes + enablement boutons.
4. Clic action → `SocialClientRequest` → `FrogGameClient.SendSocialAsync(kind, action, requestId, extra)`.

Parse Guid : [tryparsetargetguid](../../../reference/core/clientsocialroster-tryparsetargetguid.md). Cible Accept/Decline : [accepttarget](../../../reference/core/clientsocialroster-accepttarget.md).

---

## Slash (secondaire)

Toujours actifs dans le chat : `/friend`, `/party`, `/guild`, `/block` (et `/trade` hors ce guide). Préférer les boutons HUD pour la recette visuelle.
