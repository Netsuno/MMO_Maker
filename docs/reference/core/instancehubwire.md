# InstanceHubWire (opcodes 90–92)

← [Core](README.md) · Tip : `a6edd821` · #33 scaffolding

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

**Exemple :**
```csharp
byte[] extra = InstanceHubWire.BuildDefinitionIdExtra(definitionId);
byte[] body = InstanceHubWire.BuildRequest(kind, action: 2 /* Enter */, requestId, extra);
// Codec binaire donjon / raid. Version protocole 11
```
