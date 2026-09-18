# Phase 10 — STATUS

**Lot courant :** P10-1 (groupes / guildes / amis / blocage). **Pas READY.**

| Item | Valeur |
| --- | --- |
| Branche | `cursor/phase10-beta-release` (unique ; ne pas en ouvrir une seconde) |
| Base / tip `main` | `f74b34cca09dda819fe26747d48ee16d27007dfd` (merge PR #7 Phase 9) |
| Produit Phase 9 accepté | `cab57b94c20f86af2cc61738bdf3307ed9626ef4` |
| CI `main` post-fusion | https://github.com/Netsuno/MMO_Maker/actions/runs/35386572613 **SUCCESS** |
| Mandat | [`MANDATE.md`](MANDATE.md) (texte complet, 2026-09-18) |
| Protocole runtime (cette branche) | **v11** — [`SOCIAL_PROTOCOL_FREEZE.md`](SOCIAL_PROTOCOL_FREEZE.md) opcodes 80–83 |
| Gate Phase 10 | **pas atteinte** — P10-2…P10-9 absents |

## Lots

| Lot | Statut |
| --- | --- |
| P10-0 Audit + plan | **FAIT** |
| P10-1 Groupes / guildes / relations | **LIVRÉ (code + tests)** — pas une gate |
| P10-2 Échanges directs | **ABSENT** |
| P10-3 Client / éditeur externes | **INCOMPLET** |
| P10-4 Monde démo + recette | **ABSENT** |
| P10-5 Sécurité externe (TLS, invitations, NAT) | **INCOMPLET** |
| P10-6 Paquets autonomes | **INCOMPLET** |
| P10-7 Exploitation / restore | **INCOMPLET** (guildes/amis/blocs désormais dans le schéma ; restore de ces lignes **non** recertifié backup) |
| P10-8 Charge 25 joueurs | **INCOMPLET** |
| P10-9 Validation / candidate | **ABSENT** |

## P10-1 — ce qui est livré

- Groupes temporaires (invite 60 s, max 5, chef, chat Party, quit/kick/transfer/dissolve, dissolution au redémarrage processus).
- Guildes persistées PostgreSQL (`player.guilds` / `guild_members` / `guild_invites`) : création, rôles chef/officier/membre, capacité 50, MOTD, chat Guild, transfert avant départ.
- Amis + blocage persistants (`player.friendships`, `player.character_blocks`) ; le blocage coupe whisper et invites sociales.
- `PacketDispatcher.Social.cs` unique rédacteur social ; mute serveur appliqué aux canaux Party/Guild via `HandleChatSend`.
- Stubs `Guild.cs` / `GuildService.cs` **non** remplis.
- Tests : `Phase10SocialWireTests`, `Phase10SocialLogicTests`, `Phase10SocialTcpTests`, `Phase10SocialPostgresTests`.

## Interdits (toujours)

Pas de merge. Pas de distribution. Pas de Phase 11. Pas de READY bêta. P10-2 trade non commencé (opcodes 84–86 réservés).

## Verdict

**P10-1 seulement.** La bêta n’est **pas** prête.
