# STATUS — Fiche perso (emplacements paperdoll)

| Champ | Valeur |
| --- | --- |
| **Chantier** | Panneau Fiche perso : aperçu paperdoll local + emplacements lisibles |
| **Propriétaire** | Netsun |
| **Statut** | Onglet Fiche à côté d'Inventaire, touche C, bouton Perso |
| **Protocole** | **v11** inchangé — pas de paquet, pas de champ fil, pas de format de carte |

Affichage client seulement. Les couches restent **body → tunic → armor → head → hat → weapon**.
Corps et tête : Eldiran CC0 (toujours visibles). Tunique, armure, casque, arme : mêmes overlays procéduraux que le monde et le portrait du statut.
Pas de nouvel asset. La main gauche reste réservée, sans emplacement.

## Emplacements

| Couche | Libellé | Détail | Source |
| --- | --- | --- | --- |
| Body | Corps | de base | Eldiran, toujours porté |
| Tunic | Tunique | portée / — | local (`LocalTunicItemId`), clic = même bascule que le panneau équipement |
| Armor | Armure | nom d'objet / — | `EquippedArmorItemId` |
| Head | Tête | de base | Eldiran, toujours portée |
| Hat | Casque | porté / — | local (`LocalHeadwearItemId`) |
| Weapon | Arme | nom d'objet / — | `EquippedWeaponItemId` |

L'aperçu est le composite sud idle (`PlayerWorldAssets.FrameFor`, nearest). Chaque emplacement occupé montre la cellule sud idle de sa couche (`LayerIcon`).

## Ouverture

- Bouton **Perso** du menu : ouvre ou ferme la fiche. Depuis Inventaire, Perso bascule sur la fiche sans fermer la fenêtre.
- **C** (`Keys.C`) en jeu, hors saisie texte et hors touche de déplacement : même bascule. Pas d'envoi réseau.
- Titre de fenêtre : **Fiche perso**. Fermeture aussi par la croix et Échap.

## Hors scope

- Refonte inventaire / économie
- Bump protocole, équipement des autres joueurs
- Nouveaux sprites
