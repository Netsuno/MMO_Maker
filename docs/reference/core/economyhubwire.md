# EconomyHubWire (opcodes 87–89)

← [Core](README.md) · Tip : `d6e59759` · #32 scaffolding Query-only

Codec binaire multiplex HdV / courrier / coffre. `FrogWireProtocol.Version` **reste 11**.

## Méthodes clés

| Fonction | Signature | Une ligne |
| --- | --- | --- |
| isknownkind | `isknownkind(kind)` | Auction / Mail / GuildBank |
| isknownaction | `isknownaction(action)` | **Query seulement** |
| buildrequest | `buildrequest(kind, action, requestId, extra)` | Corps C→S |
| tryparserequest | `tryparserequest(payload, …)` | Parse request |
| buildresult / tryparseresult | result wire | Opcode 88 |
| buildsnapshot / tryparsesnapshot | snapshot + entrées | Opcode 89 |

Entrée unifiée `EconomyHubEntryWire(EntryId, RelatedId, Quantity, PriceOrFlags, Title)`.

**Honnêteté :** pas de mutations filaires dans ce MVP.
