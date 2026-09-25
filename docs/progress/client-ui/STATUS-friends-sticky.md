# STATUS — Liste Amis épinglée

| Champ | Valeur |
| --- | --- |
| **Chantier** | Liste Amis visible pendant le jeu, sans toucher au focus du chat |
| **Propriétaire** | Netsun |
| **Statut** | MVP test privé |
| **Protocole** | `FrogWireProtocol.Version` **11** — paquets sociaux **80–86** inchangés |
| **Tuiles** | TileAsset **48×48** |

Ouvrir **Amis** épingle la liste à gauche, au-dessus du chat. Elle reste au changement de carte, pendant le déplacement, et pendant la saisie du chat (Entrée / Échap). **Détacher** la laisse se fermer au clic carte ou à Échap hors saisie. **Épingler** la recolle. **Fermer** la range.

Un clic sur un ami (● en ligne, ○ hors ligne) passe par le chuchotement déjà livré : champ nom + canal **Chuchoter**. Le message part par `/w` ou par Entrée si la saisie est ouverte. Le clic ne prend pas le focus.

Liste vide : « Aucun ami. » Tous hors ligne : « Aucun ami en ligne. » et la ligne « hors ligne ». Les demandes en attente restent dans le panneau Amis complet.

Hors scope : refonte sociale, opcodes nouveaux, bump Hello, IA de combat, éditeur, boutique, art DA ambre.
