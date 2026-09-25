# STATUS — Fiche perso (emplacements paperdoll)

| Champ | Valeur |
| --- | --- |
| **Chantier** | Panneau Fiche perso : aperçu paperdoll local + emplacements lisibles |
| **Propriétaire** | Netsun |
| **Statut** | Onglet Fiche à côté d'Inventaire, touche C, bouton Perso, sac autoritaire |
| **Protocole** | **v11** inchangé — `EquipRequest` / `UnequipRequest` existants, pas de champ fil, pas de format de carte |

Les couches restent **body → tunic → armor → head → hat → weapon**.
Corps et tête : Eldiran CC0 (toujours visibles). Tunique, armure, casque, arme : mêmes overlays procéduraux que le monde et le portrait du statut.
Pas de nouvel asset. La main gauche reste réservée, sans emplacement.
Arme et armure : le sac de la fiche envoie l'index d'inventaire (`EquipRequest`). Un clic sur l'emplacement occupé envoie `UnequipRequest`, sauf si le sac a un objet du même type sélectionné (échange). Le snapshot met à jour le paperdoll local, le portrait du statut et le panneau équipement. La persistance est celle du dépôt d'équipement déjà en place.
Tunique et casque : aperçu local, pas sur le fil. Les autres joueurs restent corps + tête (leur équipement n'est pas dans `PositionUpdate`).

## Emplacements

| Couche | Libellé | Détail | Source |
| --- | --- | --- | --- |
| Body | Corps | de base | Eldiran, toujours porté |
| Tunic | Tunique | portée / — | local (`LocalTunicItemId`), clic = même bascule que le panneau équipement |
| Armor | Armure | nom d'objet / — | `EquippedArmorItemId`, clic = déséquiper ou équiper une armure du sac |
| Head | Tête | de base | Eldiran, toujours portée |
| Hat | Casque | porté / — | local (`LocalHeadwearItemId`) |
| Weapon | Arme | nom d'objet / — | `EquippedWeaponItemId`, clic = déséquiper ou équiper une arme du sac |

L'aperçu est le composite sud idle (`PlayerWorldAssets.FrameFor`, nearest). Chaque emplacement occupé montre la cellule sud idle de sa couche (`LayerIcon`).

## Ouverture

- Bouton **Perso** du menu : ouvre ou ferme la fiche. Depuis Inventaire, Perso bascule sur la fiche sans fermer la fenêtre.
- **C** (`Keys.C`) en jeu, hors saisie texte et hors touche de déplacement : même bascule. Pas d'envoi réseau.
- **Sac** : liste du snapshot. **Équiper** (ou double-clic) envoie `EquipRequest` pour l'index sélectionné. Le serveur refuse un type non équipable.
- Titre de fenêtre : **Fiche perso**. Fermeture aussi par la croix et Échap.

## Hors scope

- Refonte inventaire / économie
- Bump protocole, équipement des autres joueurs
- Nouveaux sprites
