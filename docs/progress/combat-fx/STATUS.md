# STATUS — Feedback visuel combat (hit / miss / crit)

| Champ | Valeur |
| --- | --- |
| **Chantier** | Feedback mêlée style MMO 2D vu de dessus (nombres nets + flash sprite), paquet 18 |
| **Propriétaire** | Netsun |
| **Statut** | Client only — **pas de merge** |
| **Protocole** | `FrogWireProtocol.Version` **reste 11**. Pas de nouvel opcode. Bit `DamageFlagCrit = 4` dans l'octet de flags déjà présent du trailer `DamageEvent`. |

## Livré

1. **Hit** — nombre `-N` jaune, contour noir 1 px, pixels entiers, au-dessus du sprite local. Flash blanc opaque du sprite (~120 ms), pas un glow ni un recolor du slot.
2. **Raté** — texte gris « Raté » pour un coup hors portée ou pas en face. Pas de flash. Recharge, mort et cible invalide ne spamment pas un float.
3. **Critique** — si le bit de flags est posé : même nombre en rouge, un cran plus grand. Les formules restent déterministes (pas de RNG, pas de rééquilibrage) : le serveur laisse le bit à 0 tant qu'un coup critique n'est pas calculé.
4. **Coût** — police UI en tracé 1 bit, contour 4 directions, un rectangle blanc. Pas de moteur de particules. Le bitmap carte n'est redessiné que pendant la durée du float (~800 ms).

## Hors scope

- Nouvel opcode, bump de protocole, sorts, IA, persistance des coups.
- Ancrage monde sur la cible quand le client n'a pas sa position (le float suit le sprite local qui a reçu le résultat).
