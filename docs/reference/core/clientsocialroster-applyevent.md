# clientsocialroster-applyevent

← [Core](README.md) · [Référence](../README.md)

Applique un événement social poussé (opcode 83).

*Source : `ClientSocialRoster.ApplyEvent`* · Tip : `a6edd821`

**Signature :** `applyevent(ev)`

**Entrées :**
- `ev` (`SocialEventWire`) — type + kind + ids + message

**Sorties :**
- invitations en attente ajoutées / retirées
- présence / MOTD / disband selon type d’événement
- `StatusLine` = message ou `KindLabel`

**Exemple :**
```csharp
roster.ApplyEvent(ev); // pending / présence / MOTD
// Applique un événement social poussé (opcode 83)
// invitations en attente ajoutées / retirées
```
