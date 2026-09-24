# STATUS — Feedback visuel combat (hit / miss / crit)

| Champ | Valeur |
| --- | --- |
| **Chantier** | Floats mêlée sur le client, alignés sur le combat MVP (paquet 18) |
| **Propriétaire** | Netsun |
| **Statut** | Client only — **pas de merge** |
| **Protocole** | `FrogWireProtocol.Version` **reste 11**. Pas de nouvel opcode. Bit `DamageFlagCrit = 4` dans l'octet de flags déjà présent du trailer `DamageEvent`. |

## Livré

1. **Hit** — nombre flottant `-N` (or) au-dessus du sprite local, plus le flash court du slot mêlée déjà en place.
2. **Raté** — texte gris « Raté » pour un coup hors portée ou pas en face. Recharge, mort et cible invalide ne spamment pas un float.
3. **Critique** — si le bit de flags est posé : même nombre, plus grand, or clair, flash de slot plus clair. Les formules restent déterministes (pas de RNG, pas de rééquilibrage) : le serveur laisse le bit à 0 tant qu'un coup critique n'est pas calculé.
4. **Coût** — police UI, deux `DrawString`, pas de moteur de particules. Le bitmap carte n'est redessiné pour l'animation que pendant la durée du float (~800 ms).

## Hors scope

- Nouvel opcode, bump de protocole, sorts, IA, persistance des coups.
- Ancrage monde sur la cible quand le client n'a pas sa position (le float suit le sprite local qui a reçu le résultat).
