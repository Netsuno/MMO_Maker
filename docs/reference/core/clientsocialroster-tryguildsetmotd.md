# clientsocialroster-tryguildsetmotd

← [Core](README.md) · [Référence](../README.md)

Construit SetMotd guilde depuis le texte saisi.

*Source : `ClientSocialRoster.TryGuildSetMotd`* · Tip : `a6edd821`

**Signature :** `tryguildsetmotd(motd, out request, out error)`

**Entrées :**
- `motd` (`string?`) — message du jour

**Sorties :**
- `ok` (`bool`) — true si OK
- `request` (`SocialClientRequest`) — Guild.SetMotd + UTF-8
- `error` (`string`) — « Message trop long. » si dépassement (`SocialProtocolLimits.MaxMotdUtf8Bytes`)

**Exemple :**
```csharp
if (!ClientSocialRoster.TryGuildSetMotd("Bienvenue !", out var req, out var error))
    return;
await client.SendSocialAsync(req.Kind, req.Action, Guid.NewGuid(), req.Extra);
```
