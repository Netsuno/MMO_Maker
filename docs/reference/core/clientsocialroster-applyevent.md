# clientsocialroster-applyevent

← [Core](README.md) · [Référence](../README.md)

Applique un événement social poussé (opcode 83).

*Source : `ClientSocialRoster.ApplyEvent`* · Tip : `d6e59759`

**Signature :** `applyevent(ev)`

**Entrées :**
- `ev` (`SocialEventWire`) — type + kind + ids + message

**Sorties :**
- invitations en attente ajoutées / retirées
- présence / MOTD / disband selon type d’événement
- `StatusLine` = message ou `KindLabel`
