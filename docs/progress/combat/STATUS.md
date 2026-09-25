# STATUS — Combat MVP + effets de statut

| Champ | Valeur |
| --- | --- |
| **Chantier** | DoT / statuts sur le Combat MVP (mêlée / distance + trailer DamageEvent) |
| **Propriétaire** | Netsun |
| **Statut** | MVP in-memory — poison + étourdissement court — **pas de merge** |
| **Base** | `main` @ `205e290` (Trade polish #75) |
| **Branche** | `cursor/status-effects-dot-mvp-969c` |
| **Protocole** | `FrogWireProtocol.Version` **reste 11** — pas de nouvel opcode. Extension additive des paquets **17 / 18**. Reuse **50** `CombatState` / **63** `DeathNotify` inchangé. Gel social 80–86, économie 87–89, instance 90–92. |

Chrome DA v2 amber inchangé. Tuiles TileAsset / carte v6 / TilePack restent **48×48** ; ce lot ne modifie pas `TileAssetMetrics` ni la grille monde. Menu ring inchangé. Hotbar : mêlée (poison) et distance (étourdissement) sur le mannequin seulement.

---

## Livré (ce lot)

1. **Modèle** — `StatusEffect` / `StatusEffectEvent` (`Frog.Core/Combat`). Kinds : **Poison** (DoT) et **Stun** (court, 0 dégât). Champs : effect id, kind, source, cible, tics restants, puissance.
2. **Règle de pile** — **refresh** (`StatusEffectLimits.StackRule`). Un seul effet par (carte, cible, kind). Réappliquer remet la durée et la puissance ; les dégâts ne s'empilent pas. Poison et étourdissement coexistent.
3. **Serveur** — `CombatMvpService` in-memory. Le drapeau optionnel du paquet 17 pose l'effet sur un coup qui porte contre le **Mannequin** (pas un jet aléatoire). Poison : 4 tics × 3 PV. Étourdissement : 2 tics, le porteur ne lance pas d'attaque (`Étourdi.`). `StatusEffectTickHostedService` tique (`Combat:StatusTickMs`, défaut 1000). Mort ou expiration : clear. Un coup fatal dissipe tout et ne pose pas l'effet. Pas de table PostgreSQL.
4. **Wire** — Octet de kind après le style du paquet 17 (absent = aucun ; kind inconnu ignoré). Trailer `StatusEffectEvent` (54 o) après le trailer `DamageEvent` du paquet 18. Les parseurs qui s'arrêtent au message ou au trailer de dégâts restent valides.
5. **Client** — Icône + teinte sur le sprite, nombre flottant vert pour le tic de poison, infobulles françaises (`Poison — N tic(s), P PV`, `Étourdi — N tic(s)`). Mêlée contre le mannequin envoie Poison ; distance envoie Stun. Les autres cibles ne demandent pas d'effet.

---

## Hors scope (volontaire)

- Grimoire, arbre de sorts, PvP équilibré, IA monstre, persistance PostgreSQL des effets.
- Bump `FrogWireProtocol.Version`, opcodes 80–92, DA skin v2, rotation de clés TilePack, enchères / mail, Trade.
- Le poison ne s'applique pas aux Slimes / joueurs du chemin Phase 7 : seulement le mannequin MVP.

---

## TODO (prochaine passe)

- Persistence PostgreSQL : journal de coups + HP monstre publié + effets repris après restart.
- Spawn monstre carte + aggro. Étendre le DoT au-delà du mannequin.
- Smoke Windows dédié icônes HUD (Linux this run = gates source + tests in-memory).

---

## Tests

- `Frog.Tests/StatusEffectMvpTests.cs` — round-trip 17/18, refresh, tic, expiration, mort, étourdissement, protocole 11.
- `Frog.Tests/CombatMvpTcpTests.cs` — TCP in-memory : apply poison + tic + broadcast. Les coups sans drapeau restent sans effet.
- `Frog.Tests/CombatMvpWireTests.cs` / `CombatMvpLogicTests.cs` — scaffolding mêlée inchangé (DamageEvent, Mannequin, rate-limit).
