# Phase 10 — Manifeste release (candidate)

Inventaire d’identités pour relecture. **Pas** une déclaration de sortie. **Pas** de phrase de gate.

Date docs : 2026-09-19. Branche `cursor/phase10-beta-release` · PR Draft [#8](https://github.com/Netsuno/MMO_Maker/pull/8).

## Identités connues (avant ce commit docs)

| Item | Valeur |
| --- | --- |
| Base `main` | `f74b34cca09dda819fe26747d48ee16d27007dfd` (PR #7) |
| Produit Phase 9 | `cab57b94c20f86af2cc61738bdf3307ed9626ef4` |
| CI `main` post-fusion | [35386572613](https://github.com/Netsuno/MMO_Maker/actions/runs/35386572613) SUCCESS |
| Tip docs précédent | `a489379f91859a66b07380b2dfb0b68c979770ba` |
| CI tip `a489379` | [35450339601](https://github.com/Netsuno/MMO_Maker/actions/runs/35450339601) SUCCESS |
| Produit vert antérieur | `814b8ba5380a952225b0f087bb2c9f0ecb0b912c` · [35449733364](https://github.com/Netsuno/MMO_Maker/actions/runs/35449733364) SUCCESS |
| EXE smoke Windows | [35403209506](https://github.com/Netsuno/MMO_Maker/actions/runs/35403209506) SUCCESS |
| Tip P10-9 (ce commit) | *à lire après push* — mandat §6 : ne pas auto-pinner son propre SHA/CI |

## Produit

| Item | Valeur |
| --- | --- |
| Badge client | `v10.3.0` |
| Protocole | `FrogWireProtocol.Version = 11` (opcodes 80–86) |
| SDK pin | `global.json` 8.0.424 (`rollForward: latestFeature`) |
| Persist | PostgreSQL 16 · rôle runtime `frog_runtime` (hébergé) |

## Archives (comment les produire — hors git)

`artifacts/` est gitignoré. Ne pas inventer de SHA-256 d’archives absentes du dépôt.

```bash
./scripts/publish-frog.sh --target all --force
# → artifacts/publish/archives/*.zip + SHA256SUMS
# chaque layout : packaging-manifest.json (SDK, RID, protocol v11, git SHA)
```

| Layout | RID | Lancement prouvé |
| --- | --- | --- |
| `server-linux-x64` | linux-x64 | Oui (CI process + zip Hello) |
| `server-win-x64` | win-x64 | Layout produit ; lancement **non** revendiqué |
| `client-win-x64` | win-x64 | `--smoke-launch` Windows CI ; GUI Linux **not proven** |
| `editor-win-x64` | win-x64 | idem |

Licences fixture : [`demo-world/LICENSES.md`](demo-world/LICENSES.md). Licence dépôt : MIT.

## Notes candidate (incompatibilités)

- Clients Phase 9 **v10** refusés au Hello (message version incompatible).
- TLS `Mode=Required` : pas de repli clair.
- Bêta : `Registration:Mode=ProvisionedOnly`.
- Groupes dissous au redémarrage processus.

Changements détaillés : lots P10-0…P10-8 dans [`STATUS.md`](STATUS.md). Contenu créatif final = Marc, pas ce manifeste.

## Acceptations propriétaire 2026-09-19

- P10-8 25×60 dédié — **accepted by owner Marc Giroux on 2026-09-19** (pas de métriques inventées).
- P10-4 recette 2 PCs — **accepted by owner Marc Giroux on 2026-09-19** (pas rejouée ici).
