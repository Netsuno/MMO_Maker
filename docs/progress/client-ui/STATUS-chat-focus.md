# STATUS — Focus du chat et chuchotement

| Champ | Valeur |
| --- | --- |
| **Chantier** | Saisie chat verrouillée, chuchotement à un joueur |
| **Propriétaire** | Netsun |
| **Statut** | MVP test privé |
| **Protocole** | `FrogWireProtocol.Version` **11** — `ChatSend` / `ChatMessage` / `Error` inchangés. Opcodes sociaux 80–86 gelés. |
| **Tuiles** | TileAsset **48×48** |

Entrée donne le focus au chat. Pendant la saisie, ZQSD / WASD / flèches ne déplacent pas l'avatar. Échap ou un clic sur la carte rend le déplacement.

Chuchoter envoie le canal Whisper déjà présent : nom saisi, ami (ou membre) sélectionné, ou cible monde. `/w Nom message` aussi. Retours : envoyé, hors ligne, inconnu, bloqué. Le chat public (Général / Local) reste en place.

Hors scope : refonte du panneau Amis, IA de combat, éditeur, boutique, bump Hello, art DA ambre.
