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
3. **Attaque** — mêlée **56 px**. La distance **96 px** (`CombatFormulas`, chemin #68) n'est utilisée que si l'appelant passe `rangedCapable: true`. `NpcDefinition` n'a pas ce champ : slime et mannequin restent en mêlée. Même formule et même mutation HP que le coup joueur→joueur (`MeleeDamage`, verrou perso). Recharge **800 ms**.
4. **Laisse** — bulle autour du **spawn** (première position vue), **8 × 48 = 384 px**. Si la cible ou le monstre sort de cette bulle, ou si la cible reste hors aggro **3500 ms**, le combat tombe et le monstre **revient** vers le spawn (48 px / tic). Pas de nouvelle cible avant d'y être. Mort, changement de carte : lâcher aussi ; hors du spawn, retour. Le mannequin déjà posé suit les mêmes règles ; un étourdissement bloque son coup.
5. **Fil** — tic serveur. `PositionUpdate` (paquet 9) inchangé pour un joueur ; monstre / mannequin : 1 octet de kind, y compris le retour au spawn. Le coup (paquet 18) joue déjà l'anim d'attaque du sprite. Pas de champ client en plus.
6. **Poison / étourdissement (#76)** — non appliqués au joueur touché. Ces effets ciblent le monstre ou le mannequin, pas le perso qui subit le coup.

Pas de table PostgreSQL, pas d'arbre de comportement, pas de bump Hello.
