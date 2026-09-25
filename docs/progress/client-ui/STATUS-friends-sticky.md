# STATUS — Liste Amis épinglée

| Champ | Valeur |
| --- | --- |
| **Chantier** | Liste Amis visible pendant le jeu, sans toucher au focus du chat |
| **Propriétaire** | Netsun |
| **Statut** | MVP test privé |
| **Protocole** | `FrogWireProtocol.Version` **11** — paquets sociaux **80–86** inchangés |
| **Tuiles** | TileAsset **48×48** |

Ouvrir **Amis** épingle la liste à gauche, au-dessus du chat. Elle reste pendant le déplacement et les clics sur la carte. **Détacher** la laisse se fermer au clic carte ou à Échap (hors saisie). **Épingler** la recolle. **Fermer** la range.

Un clic sur un ami remplit le champ nom et passe sur **Chuchoter**. Le message part par le chat déjà livré (`/w` ou Entrée si la saisie est ouverte). Le clic ne prend pas le focus : Entrée, Échap et le verrou de déplacement restent ceux du chat.

Liste vide : « Aucun ami. » Tous hors ligne : « Aucun ami en ligne. » et la ligne « hors ligne ». Les demandes en attente restent dans le panneau Amis complet.

Hors scope : refonte sociale, opcodes nouveaux, bump Hello, IA de combat, éditeur, boutique, art DA ambre.
