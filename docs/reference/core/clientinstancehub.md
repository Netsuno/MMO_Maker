# ClientInstanceHub

← [Core](README.md) · Tip : `a6edd821` · #33 scaffolding

État client Instance — hors WinForms.

## Méthodes clés

| Fonction | Signature | Une ligne |
| --- | --- | --- |
| applysnapshot / applyresult | applique 92 / 91 | Catalogue + status |
| entries / allentries | listes | Par kind ou fusion |
| emptyhint | `emptyhint()` | Hint liste vide |
| queryextra / enterextra / leaveextra | builders | Extra wire |
| kindlabel / formatentry | helpers UI | Donjon / Raid |

**Exemple :**
```csharp
hub.ApplyResult(result);
var rows = hub.Entries(InstanceHubKind.Dungeon);
string hint = hub.EmptyHint();
```
