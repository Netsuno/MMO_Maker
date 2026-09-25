# STATUS — Focus du chat et chuchotement

| Champ | Valeur |
| --- | --- |
| **Chantier** | Saisie chat verrouillée, chuchotement à un joueur |
| **Propriétaire** | Netsun |
| **Statut** | MVP test privé |
| **Protocole** | `FrogWireProtocol.Version` **11** — `ChatSend` / `ChatMessage` / `Error` inchangés. Opcodes sociaux 80–86 gelés. |
| **Tuiles** | TileAsset **48×48** |

Entrée donne le focus au chat, ou envoie si la saisie est déjà ouverte. Pendant la saisie, les touches de jeu ne déplacent pas l'avatar et les flèches haut/bas ne rendent pas le focus au monde. Échap ou un clic sur la carte rend le déplacement.

Le chuchotement réutilise le canal Whisper déjà livré (`/w Nom message`, ou le champ nom). La ligne s'affiche dans le dock chat existant. Le serveur répond « Joueur hors ligne. » pour un compte absent ou déconnecté ; le client le dit en français, ainsi que « Vous êtes bloqué. » Le chat public reste en place.

Hors scope : refonte du panneau chat, panneau Amis collant, nouveau opcode, IA de combat, éditeur, boutique, bump Hello, art DA ambre.
