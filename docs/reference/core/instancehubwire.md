# InstanceHubWire (opcodes 90–92)

← [Core](README.md) · Tip : `d6e59759` · #33 scaffolding

Codec binaire donjon / raid. Version protocole **11**.

## Méthodes clés

| Fonction | Signature | Une ligne |
| --- | --- | --- |
| isknownkind / isknownaction | guards | Dungeon/Raid ; Query/Enter/Leave |
| buildrequest / tryparserequest | request | Opcode 90 |
| builddefinitionidextra | `builddefinitionidextra(definitionId)` | Extra Enter |
| tryreaddefinitionid | parse extra | Guid définition |
| buildresult / tryparseresult | result | Opcode 91 |
| buildsnapshot / tryparsesnapshot | snapshot | Opcode 92 |
