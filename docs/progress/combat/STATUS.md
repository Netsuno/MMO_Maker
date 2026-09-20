# STATUS — Combat MVP scaffolding

| Champ | Valeur |
| --- | --- |
| **Chantier** | Combat mêlée MVP — AttackRequest / DamageEvent / CombatTarget après social / économie / donjons |
| **Propriétaire** | Netsun |
| **Statut** | MVP in-memory + hotbar / floats — **pas de merge** |
| **Base** | `main` @ `d6e5975` (re-pin après merge PR #33 dungeons / instances) |
| **Branche** | `cursor/combat-mvp-scaffolding-2d69` |
| **PR** | Draft — **pas de merge** |
| **Tip** | `b5b8c91ba19e7a81d0c950237714acaf0215bd5c` |
| **CI** | en attente (`build-and-test` + `postgres-integration`) |
| **Protocole** | `FrogWireProtocol.Version` **reste 11** — pas de nouvel opcode. Extension additive des paquets Phase 7 **17 / 18** (`MeleeAttackRequest` / `MeleeAttackResult`) + reuse **50** `CombatState` / **63** `DeathNotify`. Gel social 80–86, économie 87–89, instance 90–92. |

Chrome DA v2 + overlay existant. Menu ring **reste 5 icônes**. Dock chat **Amis / Groupe / Guilde** inchangé. Ouverture : hotbar slot **1** / touche **Espace** (pas de 6ᵉ icône, pas de nouvel onglet Social).

---

## Livré

1. **Modèles** — `AttackRequest`, `DamageEvent`, `CombatTarget` (`Frog.Core/Combat`). Kind Player / Monster / Dummy / Npc. Mannequin d'entraînement `Mannequin` (id fixe) si les monstres ne sont pas spawnés.
2. **Wire** — `Frog.Core/Protocol/CombatMvpWire.cs` : paquet **17** = nom historique + extras optionnels kind/facing/targetId ; paquet **18** = hit/nom/message historique + trailer `DamageEvent` (attaquant, cible, dégâts, HP, flags). Parseurs Phase 7 qui s'arrêtent au message restent valides.
3. **Serveur** — `CombatMvpService` in-memory. Range (`BasicAttackRangePixels`), facing (tamponné au move, pas un champ protocole), rate-limit (`BasicAttackCooldownMs`). Applique les HP du mannequin, broadcast dégâts aux occupants de la carte, respawn dummy après mort. Réutilise `CombatGameplayService` pour Slime / PvP existants (trailer DamageEvent ajouté). Pas de migration PostgreSQL.
4. **Client** — `ClientCombatHud` (Linux sans WinForms) + `CombatEffect` floats sur le bitmap carte + flash slot mêlée. `FrogGameClient.SendMeleeAttackAsync` / `DamageEventReceived`. Cible par défaut **Mannequin**.
5. **Persistance** — **in-memory stubs**. Pas de table `combat_*` / journal de coups.

---

## Hors scope (volontaire)

- IA monstre, aggro, sorts, PvP équilibré, loot table dédiée.
- Persistence PostgreSQL `combat_event`, reprise après restart, idempotence `request_id`.
- Bump `FrogWireProtocol.Version`, trailer Hello, opcodes 80–92, nouvel opcode 93+.
- Weather, movement (hors tampon Facing), audio, maintenance, auction/mail/coffre, instances, login shell, panes Phase 8 exact-sha (`DialoguePanel` / `QuestJournalPanel` / `EnvironmentPanel`).
- Nouveau chrome overlay / 6ᵉ icône menu ring / boutons dock extra.

---

## TODO (prochaine passe)

- Persistence PostgreSQL : journal de coups + HP monstre publié.
- Spawn monstre carte + aggro + facing client→serveur dédié.
- Smoke Windows dédié floats HUD (Linux this run = gates source seulement).

---

## Tests

- `Frog.Tests/CombatMvpWireTests.cs` — round-trip 17/18, protocole 11, facing, hud floats, câblage shell/hotbar/client, ce STATUS.
- `Frog.Tests/CombatMvpLogicTests.cs` — sans perso / mort / hit dummy / rate-limit / hors portée / facing / kill+respawn.
- `Frog.Tests/CombatMvpTcpTests.cs` — TCP in-memory attaque Mannequin + trailer + recharge + broadcast.

Linux this run: `dotnet test Frog.Tests` (filtre CombatMvp).

Linux / cet agent : pas de capture WinForms HUD. Revue pixel = Windows 1280×720 DPI 125 %.
