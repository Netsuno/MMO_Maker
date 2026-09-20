# ClientEconomyHub

← [Core](README.md) · Tip : `d6e59759` · #32 scaffolding

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

EmptyHints : HdV « Aucune enchère… » ; Courrier « Boîte… vide » ; Coffre selon présence guilde.
