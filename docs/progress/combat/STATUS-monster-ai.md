# STATUS — IA monstre / PNJ (combat)

| Champ | Valeur |
| --- | --- |
| **Chantier** | Aggro, poursuite, attaque des monstres déjà spawnés (au-dessus du combat + DoT) |
| **Propriétaire** | Netsun |
| **Statut** | MVP in-memory |
| **Base** | `main` @ `cb1e5d5` (éditeur modèles #79) |
| **Branche** | `cursor/monster-npc-combat-ai-b499` |
| **Protocole** | `FrogWireProtocol.Version` **reste 11**. Paquet **9** : trailer kind optionnel (monstre / mannequin). Coup : paquet **18** + **50** CombatState + **63** DeathNotify. Pas d'opcode nouveau. Gel 80–92. |

Tuiles TileAsset restent **48×48**. `WorldMetrics.DefaultTileSizePixels` reste 32 pour le monde feuille. L'aggro compte en tuiles de contenu 48.

## Règles

1. **Aggro** — 5 × 48 px = **240**. Un joueur vivant, sur la même carte. Une seule cible.
2. **Poursuite** — **48 px / tic** (tic **400 ms**, `Combat:MonsterAiTickMs`). Le processus `Frog.Server` active le tic. `Combat:MonsterAiEnabled=false` le coupe ; `true` le force (tests TCP). Murs via la collision carte si elle est chargée ; sinon clamp au rectangle.
3. **Attaque** — mêlée **56 px** ou distance **96 px** (`CombatFormulas`). Même formule et même mutation HP que le coup joueur→joueur (`MeleeDamage`, verrou perso). Recharge **800 ms**.
4. **Lâcher** — mort du monstre ou de la cible, changement de carte, laisse **8 × 48 = 384 px**, ou **3500 ms** hors du rayon d'aggro. Le mannequin déjà posé suit les mêmes règles ; un étourdissement bloque son coup.
5. **Fil** — `PositionUpdate` inchangé pour un joueur. Monstre / mannequin : 1 octet de kind. Les clients qui ignorent l'octet lisent encore la position.

Pas de table PostgreSQL, pas d'arbre de comportement, pas de bump Hello.
