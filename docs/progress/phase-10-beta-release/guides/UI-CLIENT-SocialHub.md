# Frog.Client — UI Social (dock + overlay Amis / Groupe / Guilde)

← [Guides](README.md) · [MainShell](UI-CLIENT-MainShell.md) · [Référence Client](../../../reference/client/README.md) · [ClientSocialRoster](../../../reference/core/README.md)

Tip miroir : `d6e59759`. #27 Social (merge) · #32 économie scaffolding · #33 instances scaffolding. Propriétaire : **Netsun**.

Panneau HUD social branché sur opcodes **80–83** (`SendSocialAsync` → `SocialRequest`). Slash `/friend` `/party` `/guild` restent disponibles ; ce guide documente **chaque bouton / panneau** visible.

> Onglets **Courrier / HdV / Coffre** (#32) et **Instance** (#33) : **échafaudage** sur tip `d6e59759`. Query-only / listes vides / Enter-Leave in-memory. **Pas** d’économie ni de donjon jouable livrés.

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
| **Courrier** / **HdV** / **Coffre** | Listes Query-only vides (#32) | [Échafaudage économie](#échafaudage--courrier--hdv--coffre-32) |
| **Instance** | Catalogue + Entrer/Quitter in-memory (#33) | [Échafaudage Instance](#échafaudage--instance-33) |

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


---

## Échafaudage — Courrier / HdV / Coffre (#32)

**Statut :** scaffolding **Query-only**. Pas d’achat, pas d’envoi de courrier, pas de dépôt/retrait coffre. Pas de PostgreSQL.

| Onglet | Kind | EmptyHint typique | Bouton |
| --- | --- | --- | --- |
| **Courrier** | `Mail=2` | « Boîte de courrier vide. » | **Actualiser** → Query |
| **HdV** | `Auction=1` | « Aucune enchère — hôtel des ventes vide (MVP). » | **Actualiser** |
| **Coffre** | `GuildBank=3` | sans guilde : « Pas de guilde — coffre indisponible. » ; avec guilde : « Coffre de guilde : emplacements vides. » (8 slots vides) | **Actualiser** |

Flux : **Actualiser** → `SendEconomyHubAsync(kind, Query, requestId, [])` (opcode **87**) → result **88** / snapshot **89** → `ClientEconomyHub.ApplySnapshot` / `ApplyResult` → `EconomyHubSurface`.

API : [sendeconomyhubasync](../../../reference/client/sendeconomyhubasync.md) · [clienteconomyhub](../../../reference/core/clienteconomyhub.md) · [economyhubwire](../../../reference/core/economyhubwire.md) · serveur [economyhubservice-executeasync](../../../reference/server/economyhubservice-executeasync.md).

<!-- CAPTURE: assets/economy-01-courrier-vide.png -->
*Capture à venir (placeholder) : onglet Courrier — hint boîte vide + Actualiser.*

<!-- CAPTURE: assets/economy-02-hdv-vide.png -->
*Capture à venir (placeholder) : onglet HdV — hint aucune enchère.*

<!-- CAPTURE: assets/economy-03-coffre-vide.png -->
*Capture à venir (placeholder) : onglet Coffre — slots vides ou pas de guilde.*

---

## Échafaudage — Instance (#33)

**Statut :** scaffolding in-memory. Catalogue fixe ; **pas** de carte `map_instance` PG ; procgen = stub seedé.

| Contrôle | Rôle | API |
| --- | --- | --- |
| Liste | Définitions + runs (si présents) | `ClientInstanceHub` |
| **Actualiser** | Query Dungeon + Raid | `SendInstanceHubAsync` action Query (90) |
| **Entrer** | Enter sur définition sélectionnée (gate groupe ; raid min 2) | action Enter + `definitionId` |
| **Quitter** | Leave → overworld (hook session) | action Leave |

Catalogue MVP :

| Nom | Kind | Min party |
| --- | --- | --- |
| **Ruines du Marais** | Donjon | 1 |
| **Crypte du Roi** | Raid | 2 |

EmptyHint si catalogue/run vide côté client : « Aucun donjon — catalogue vide (MVP). » (le serveur Query renvoie en principe les 2 définitions).

Opcodes **90–92**. API : [sendinstancehubasync](../../../reference/client/sendinstancehubasync.md) · [clientinstancehub](../../../reference/core/clientinstancehub.md) · [instancehubwire](../../../reference/core/instancehubwire.md) · [dungeoncatalog](../../../reference/core/dungeoncatalog.md) · [proceduraldungeongenerator](../../../reference/core/proceduraldungeongenerator.md) · serveur [instancehubservice-execute](../../../reference/server/instancehubservice-execute.md).

<!-- CAPTURE: assets/instance-01-onglet.png -->
*Capture à venir (placeholder) : onglet Instance — catalogue Ruines / Crypte + Entrer / Quitter / Actualiser.*


## Slash (secondaire)

Toujours actifs dans le chat : `/friend`, `/party`, `/guild`, `/block` (et `/trade` hors ce guide). Préférer les boutons HUD pour la recette visuelle.
