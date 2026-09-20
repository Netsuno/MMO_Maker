# clientsocialroster-tryparsetargetguid

← [Core](README.md) · [Référence](../README.md)

Parse un Guid personnage pour le champ HUD.

*Source : `ClientSocialRoster.TryParseTargetGuid`* · Tip : `d6e59759`

**Signature :** `tryparsetargetguid(text, out id, out error)`

**Entrées :**
- `text` (`string?`) — saisie UI

**Sorties :**
- `ok` (`bool`) — Guid non vide
- `id` (`Guid`) — cible
- `error` (`string`) — « Identifiant (Guid) invalide. » si échec
