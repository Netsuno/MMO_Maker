# ClientEconomyHub

← [Core](README.md) · Tip : `a6edd821` · #32 scaffolding

État client HdV / courrier / coffre — testable hors WinForms.

## Méthodes clés

| Fonction | Signature | Une ligne |
| --- | --- | --- |
| applysnapshot | `applysnapshot(snapshot)` | Stocke Auction / Mail / GuildBank |
| applyresult | `applyresult(result)` | Met à jour `StatusLine` |
| entries | `entries(kind)` | Liste courante (souvent vide) |
| emptyhint | `emptyhint(kind)` | Texte liste vide |
| queryextra | `queryextra()` | `[]` pour Query |
| kindlabel / formatentry | helpers UI | Libellés Courrier / HdV / Coffre |

**Exemple :**
```csharp
hub.ApplySnapshot(snapshot);
var rows = hub.Entries(EconomyHubKind.Auction); // souvent vide (MVP)
string hint = hub.EmptyHint(EconomyHubKind.Mail);
```

EmptyHints : HdV « Aucune enchère… » ; Courrier « Boîte… vide » ; Coffre selon présence guilde.
